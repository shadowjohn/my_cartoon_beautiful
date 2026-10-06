#include "realesrgan.h"
#include <vector>
#include <cstdio>
int wmain(int argc,wchar_t** argv) {
 if(argc!=5)return 2;
 int scale=_wtoi(argv[2]);std::wstring dir=argv[1];
 std::wstring stem=dir+L"/realesr-animevideov3-x"+std::to_wstring(scale);
 ncnn::create_gpu_instance();
 {
  int gpu=ncnn::get_default_gpu_index();RealESRGAN engine(gpu,false);
  auto budget=ncnn::get_gpu_device(gpu)->get_heap_budget();
  engine.scale=scale;engine.tilesize=budget>1900?200:budget>550?100:budget>190?64:32;engine.prepadding=10;
  if(engine.load(stem+L".param",stem+L".bin"))return 3;
  std::vector<unsigned char> input(96*64*3),output(96*scale*64*scale*3);
  FILE* f=_wfopen(argv[3],L"rb");if(!f)return 4;fread(input.data(),1,input.size(),f);fclose(f);
  ncnn::Mat in(96,64,input.data(),size_t(3),3),out(96*scale,64*scale,output.data(),size_t(3),3);
  if(engine.process(in,out))return 5;
  f=_wfopen(argv[4],L"wb");if(!f)return 6;fwrite(output.data(),1,output.size(),f);fclose(f);
 }
 ncnn::destroy_gpu_instance();return 0;
}
