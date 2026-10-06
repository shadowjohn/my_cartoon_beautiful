#include "rs_api.h"
#include "upstream/realesrgan.h"
#include <atomic>
#include <climits>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <vector>
#include <exception>
#include <cmath>

namespace {
std::mutex registry;
bool gpu_busy = false;
struct Session {
    bool gpu_initialized = false;
    int scale = 2;
    std::unique_ptr<RealESRGAN> engine;
    std::atomic<bool> cancelled{false};
    std::mutex processing;
    ~Session() {
        // Keep the global GPU reservation until every process reference has returned.
        engine.reset();
        std::lock_guard<std::mutex> lock(registry);
        if (gpu_initialized) ncnn::destroy_gpu_instance();
        gpu_busy = false;
    }
};
std::shared_ptr<Session> current;
int fail(int code, const char* message, char* error, uint32_t capacity) {
    if (error && capacity) { size_t n = std::min<size_t>(strlen(message), capacity - 1); memcpy(error,message,n); error[n]=0; }
    return code;
}
std::shared_ptr<Session> lookup(void* handle) {
    std::lock_guard<std::mutex> lock(registry);
    return handle && current.get() == handle ? current : nullptr;
}
bool exists(const wchar_t* path) {
    FILE* f=_wfopen(path,L"rb"); if(!f) return false; fclose(f); return true;
}
bool geometry(int w,int h,int channels,int stride,uint64_t bytes) {
    if(w<=0 || h<=0 || (channels!=3 && channels!=4) || stride<=0) return false;
    uint64_t row=uint64_t(w)*channels;
    // Upstream uses signed int pixel offsets; reject dimensions outside that domain.
    return row<=uint64_t(stride) && uint64_t(stride)*h<=INT_MAX && uint64_t(stride)*h<=bytes;
}
}
uint32_t rs_abi_version() { return 1; }
int32_t rs_create(const wchar_t* param,const wchar_t* model,int32_t gpu,int32_t scale,
                 int32_t tile,int32_t tta,void** handle,char* error,uint32_t capacity) {
    if(handle) *handle=nullptr;
    if(!handle || !param || !model || scale<2 || scale>4 || gpu< -1 ||
       (tile!=0 && (tile<32 || tile>1024)) || (tta!=0 && tta!=1))
        return fail(-1,"Invalid session parameters",error,capacity);
    if(!exists(param) || !exists(model)) return fail(-2,"Model file not found or unreadable",error,capacity);
    std::shared_ptr<Session> session;
    try {
        { std::lock_guard<std::mutex> lock(registry);
          if(gpu_busy) return fail(-5,"Another Real-ESRGAN session is active",error,capacity);
          // Allocate before reserving; a failed allocation cannot leave a reservation behind.
          session=std::make_shared<Session>(); gpu_busy=true;
        }
        session->gpu_initialized=true;
        if(ncnn::create_gpu_instance()!=0 || ncnn::get_gpu_count()==0)
            return fail(-3,"Vulkan GPU initialization failed",error,capacity);
        if(gpu==-1) gpu=ncnn::get_default_gpu_index();
        if(gpu<0 || gpu>=ncnn::get_gpu_count()) return fail(-3,"Requested Vulkan GPU is unavailable",error,capacity);
        if(!tile) { uint32_t budget=ncnn::get_gpu_device(gpu)->get_heap_budget();
            tile=budget>1900?200:budget>550?100:budget>190?64:32; }
        session->scale=scale;
        session->engine.reset(new RealESRGAN(gpu,tta!=0));
        session->engine->scale=scale; session->engine->tilesize=tile; session->engine->prepadding=10;
        int code=session->engine->load(param,model);
        if(code) return fail(code,code==-2?"Model loading failed":"GPU pipeline creation failed",error,capacity);
        { std::lock_guard<std::mutex> lock(registry); current=session; *handle=session.get(); }
        return fail(0,"",error,capacity);
    } catch(const std::exception& e) { return fail(-4,e.what(),error,capacity); }
      catch(...) { return fail(-4,"Unexpected native initialization error",error,capacity); }
}
int32_t rs_process(void* handle,const uint8_t* input,uint64_t input_bytes,int32_t width,
    int32_t height,int32_t channels,int32_t input_stride,uint8_t* output,uint64_t output_bytes,
    int32_t output_stride,rs_progress progress,void* user,char* error,uint32_t capacity) {
    try {
        auto session=lookup(handle);
        if(!session || !input || !output || width<11 || height<11 || !geometry(width,height,channels,input_stride,input_bytes) ||
           width>INT_MAX/session->scale || height>INT_MAX/session->scale ||
           !geometry(width*session->scale,height*session->scale,channels,output_stride,output_bytes))
            return fail(-1,"Invalid handle, dimensions (minimum 11x11), stride or buffer capacity",error,capacity);
        std::unique_lock<std::mutex> operation(session->processing,std::try_to_lock);
        if(!operation.owns_lock()) return fail(-5,"This session is already processing",error,capacity);
        if(session->cancelled.load()) return fail(1,"Cancelled",error,capacity);
        const size_t in_row=size_t(width)*3, out_row=in_row*session->scale;
        std::vector<uint8_t> packed_in(in_row*height),packed_out(out_row*height*session->scale);
        ncnn::Mat alpha, scaled_alpha;
        if(channels==4) { alpha.create(width,height,1,size_t(4),1); if(alpha.empty())return fail(-4,"Alpha allocation failed",error,capacity); }
        for(int y=0;y<height;y++) {
            const uint8_t* source=input+size_t(y)*input_stride;
            if(channels==3) memcpy(packed_in.data()+y*in_row,source,in_row);
            else for(int x=0;x<width;x++) {
                memcpy(packed_in.data()+y*in_row+x*3,source+x*4,3);
                alpha.row(y)[x]=source[x*4+3];
            }
        }
        // Pinned GPU Interp alpha can lose the Vulkan device on this driver (also in the old CLI).
        // Keep neural RGB inference identical and resize straight alpha with ncnn's CPU bicubic.
        ncnn::Mat in(width,height,packed_in.data(),size_t(3),3);
        ncnn::Mat out(width*session->scale,height*session->scale,packed_out.data(),size_t(3),3);
        int code=session->engine->process(in,out,session->cancelled,progress,user);
        if(code) return fail(code,code==1?"Cancelled":"GPU inference failed",error,capacity);
        if(session->cancelled.load()) return fail(1,"Cancelled",error,capacity);
        if(channels==4) {
            ncnn::Option option;option.num_threads=1;option.use_vulkan_compute=false;option.use_packing_layout=false;
            ncnn::resize_bicubic(alpha,scaled_alpha,width*session->scale,height*session->scale,option);
            if(scaled_alpha.empty())return fail(-4,"Alpha resize failed",error,capacity);
            if(session->cancelled.load())return fail(1,"Cancelled",error,capacity);
        }
        for(int y=0;y<height*session->scale;y++) {
            uint8_t* destination=output+size_t(y)*output_stride;
            if(channels==3) memcpy(destination,packed_out.data()+y*out_row,out_row);
            else for(int x=0;x<width*session->scale;x++) {
                memcpy(destination+x*4,packed_out.data()+y*out_row+x*3,3);
                destination[x*4+3]=uint8_t(std::min(255.f,std::max(0.f,std::floor(scaled_alpha.row(y)[x]+0.5f))));
            }
        }
        return fail(0,"",error,capacity);
    } catch(const std::exception& e) { return fail(-4,e.what(),error,capacity); }
      catch(...) { return fail(-4,"Unexpected native inference error",error,capacity); }
}
void rs_request_cancel(void* handle) {
    try { auto session=lookup(handle); if(session) session->cancelled.store(true); } catch(...) {}
}
void rs_destroy(void* handle) {
    try {
        std::shared_ptr<Session> releasing;
        { std::lock_guard<std::mutex> lock(registry);
          if(handle && current.get()==handle) { current->cancelled.store(true); releasing=std::move(current); }
        }
        // Destruction outside registry mutex; process retains shared ownership when active.
    } catch(...) {}
}
