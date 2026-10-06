#include "rs_api.h"
#include <cstdio>
#include <vector>
#include <stdexcept>
#include <string>
#include <thread>
#include <atomic>
#include <chrono>
static void require(bool ok, const char* message) { if (!ok) throw std::runtime_error(message); }
static void __cdecl cancel(void* h,int32_t done,int32_t) { if(done) rs_request_cancel(h); }
struct Pause { std::atomic<bool> entered{false}, resume{false}; };
static void __cdecl pause_tile(void* arg,int32_t,int32_t) {
 auto* p=static_cast<Pause*>(arg); p->entered=true;
 while(!p->resume.load()) std::this_thread::sleep_for(std::chrono::milliseconds(1));
}
int wmain(int argc,wchar_t** argv) {
 try {
  char error[1024]={}; void* h=(void*)1;
  require(rs_abi_version()==1,"ABI 1");
  require(rs_create(L"missing.param",L"missing.bin",-1,2,0,0,&h,error,sizeof(error))==-2 && !h,"missing model/null handle");
  require(rs_create(L"missing",L"missing",-1,1,0,0,&h,error,sizeof(error))==-1 && !h,"invalid scale");
  require(rs_process(nullptr,nullptr,0,0,0,3,0,nullptr,0,0,nullptr,nullptr,error,sizeof(error))==-1,"invalid process");
  rs_request_cancel(nullptr); rs_destroy(nullptr);
  if(argc>1) {
   std::wstring dir=argv[1]; auto param=dir+L"/realesr-animevideov3-x2.param"; auto model=dir+L"/realesr-animevideov3-x2.bin";
   if(argc>2) {
    int tile=_wtoi(argv[2]);
    require(rs_create(param.c_str(),model.c_str(),-1,2,tile,0,&h,error,sizeof(error))==0,error);
    std::vector<uint8_t> in(96*64*3,127),out(192*128*3,0);
    int code=rs_process(h,in.data(),in.size(),96,64,3,288,out.data(),out.size(),576,nullptr,nullptr,error,sizeof(error));
    fprintf(stderr,"single tile=%d code=%d first=%u center=%u error=%s\n",tile,code,out[0],out[(64*192+96)*3],error);
    rs_destroy(h);return code;
   }
   require(rs_create(param.c_str(),model.c_str(),9999,2,32,0,&h,error,sizeof(error))==-3 && !h,"invalid gpu");
   for(const char* content : {"", "7767517\n2 2\nInput data 0 1 data\nReLU broken 1 1 data", "7767517\n2 2\nInput data 0 1 data\nNoSuchLayer broken 1 1 data out\n"}) {
    FILE* bad=_wfopen(L"invalid-param.param",L"wb");fputs(content,bad);fclose(bad);
    require(rs_create(L"invalid-param.param",model.c_str(),-1,2,32,0,&h,error,sizeof(error))==-2 && !h,"partial param load");
   }
   _wremove(L"invalid-param.param");
   FILE* invalid=_wfopen(L"invalid-model.bin",L"wb"); require(invalid!=nullptr,"negative fixture"); fclose(invalid);
   require(rs_create(param.c_str(),L"invalid-model.bin",-1,2,32,0,&h,error,sizeof(error))==-2 && !h,"partial model load");
   _wremove(L"invalid-model.bin");
   require(rs_create(param.c_str(),model.c_str(),-1,2,32,0,&h,error,sizeof(error))==0 && h,error);
   void* second=nullptr;
   require(rs_create(param.c_str(),model.c_str(),-1,2,32,0,&second,error,sizeof(error))==-5 && !second,"second session busy");
   std::vector<uint8_t> input(96*64*3,127),output(192*128*3,0);
   require(rs_process(h,input.data(),input.size(),96,64,3,287,output.data(),output.size(),576,nullptr,nullptr,error,sizeof(error))==-1,"short stride");
   require(rs_process(h,input.data(),input.size()-1,96,64,3,288,output.data(),output.size(),576,nullptr,nullptr,error,sizeof(error))==-1,"short capacity");
   require(rs_process(h,input.data(),input.size(),INT32_MAX,64,3,288,output.data(),output.size(),576,nullptr,nullptr,error,sizeof(error))==-1,"overflow width");
   require(rs_process(h,input.data(),input.size(),96,64,3,288,output.data(),output.size(),576,cancel,h,error,sizeof(error))==1,"tile cancellation");
   require(output[0]==0,"cancel did not copy partial output");
   rs_destroy(h); h=nullptr;
   require(rs_create(param.c_str(),model.c_str(),-1,2,32,0,&h,error,sizeof(error))==0 && h,error);
   require(rs_process(h,input.data(),input.size(),96,64,3,288,output.data(),output.size(),576,nullptr,nullptr,error,sizeof(error))==0,error);
   fprintf(stderr,"inference first=%u center=%u\n",output[0],output[(64*192+96)*3]);
   require(output[(64*192+96)*3]>100 && output[(64*192+96)*3]<160,"inference center pixels");
   Pause pause; int threaded=-99; char thread_error[1024]={};
   std::thread worker([&]{threaded=rs_process(h,input.data(),input.size(),96,64,3,288,output.data(),output.size(),576,pause_tile,&pause,thread_error,sizeof(thread_error));});
   auto deadline=std::chrono::steady_clock::now()+std::chrono::seconds(30);
   while(!pause.entered && std::chrono::steady_clock::now()<deadline) std::this_thread::sleep_for(std::chrono::milliseconds(1));
   if(!pause.entered) { pause.resume=true;worker.join();throw std::runtime_error("worker callback timeout"); }
   int concurrent=rs_process(h,input.data(),input.size(),96,64,3,288,output.data(),output.size(),576,nullptr,nullptr,error,sizeof(error));
   rs_destroy(h); h=nullptr;
   int during_destroy=rs_create(param.c_str(),model.c_str(),-1,2,32,0,&second,error,sizeof(error));
   pause.resume=true;worker.join();
   require(concurrent==-5 && during_destroy==-5 && threaded==1,"process/destroy lifetime and busy guard");
   require(rs_create(param.c_str(),model.c_str(),-1,2,32,0,&h,error,sizeof(error))==0 && h,error);rs_destroy(h);
   require(rs_create(param.c_str(),model.c_str(),-1,3,32,0,&h,error,sizeof(error))==0,error);
   std::vector<uint8_t> wrongScale(288*192*3,0);
   require(rs_process(h,input.data(),input.size(),96,64,3,288,wrongScale.data(),wrongScale.size(),864,nullptr,nullptr,error,sizeof(error))==-4,"mismatched model scale");rs_destroy(h);
  }
  puts("RealESRGAN C ABI: PASS"); return 0;
 } catch(const std::exception& e) { fprintf(stderr,"FAIL: %s\n",e.what());return 1; }
}
