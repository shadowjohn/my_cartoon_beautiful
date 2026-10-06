using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using utility_app;
internal static class UpscaleTests
{
    public static string NativeRoot=Path.GetFullPath("artifacts/native-runtime/realesrgan");
    public static string Models=Path.GetFullPath("my_cartoon_beautiful/binary/realesrgan-ncnn-vulkan-v0.2.0-windows/models");
    private static void Require(bool ok,string message) { if(!ok) throw new Exception(message); }
    public static void Run()
    {
        string dir=Path.GetFullPath("artifacts/media-tests/upscale-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string input=Path.GetFullPath("artifacts/media-tests/baseline/frames-30fps/00000001.png");
        if(!File.Exists(input)) input=Path.GetFullPath("artifacts/media-tests/baseline/sync-30fps-frames/00000001.png");
        // Compare the exact legacy PNG supplied to the CLI baseline, avoiding decoder differences.
        if(!File.Exists(input)) throw new Exception("Locate legacy baseline first PNG: "+input);
        bool exact=true;
        for(int scale=2;scale<=4;scale++)
        using(var up=new RealEsrganUpscaler(NativeRoot,Models,new RealEsrganOptions{Scale=scale}))
        {
            string output=Path.Combine(dir,"x"+scale+".png");up.UpscalePng(input,output,null,CancellationToken.None);
            using(var bitmap=new Bitmap(output)) { Require(bitmap.Width==96*scale && bitmap.Height==64*scale,"upscale size"); }
            string baseline=Path.GetFullPath("artifacts/media-tests/baseline/esrgan-x"+scale+".png");
            exact &= Compare(output,baseline,4,0.12,"legacy");
            exact &= Compare(output,Path.GetFullPath("artifacts/media-tests/reference/x"+scale+".png"),0,0,"unmodified-core");
            up.UpscalePng(input,Path.Combine(dir,"reuse-x"+scale+".png"),null,CancellationToken.None);
        }
        bool missing=false;try { using(var up=new RealEsrganUpscaler(NativeRoot,Path.Combine(dir,"missing"),new RealEsrganOptions())){} }
        catch(NativeMediaException e){missing=e.NativeCode==-2;} Require(missing,"missing model error");
        using(var up=new RealEsrganUpscaler(NativeRoot,Models,new RealEsrganOptions{TileSize=32}))
        using(var source=new Bitmap(33,32,PixelFormat.Format24bppRgb))
        {
            using(var g=Graphics.FromImage(source))g.Clear(Color.FromArgb(210,20,30));
            Console.WriteLine("upscale padded RGB");
            using(var scaled=up.UpscaleBitmap(source,null,CancellationToken.None))
                Require(scaled.GetPixel(33,32).R>150 && scaled.GetPixel(33,32).B<80,"padded BGR channels");
            Console.WriteLine("upscale alpha");
            using(var alpha=new Bitmap(33,32,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(alpha))g.Clear(Color.FromArgb(96,210,20,30));
                using(var scaled=up.UpscaleBitmap(alpha,null,CancellationToken.None)) Require(Math.Abs(scaled.GetPixel(33,32).A-96)<=1,"BGRA alpha");
            }
            Console.WriteLine("upscale negative stride");
            int stride=100;IntPtr memory=Marshal.AllocHGlobal(stride*32);
            try {
                byte[] pixels=new byte[stride*32];for(int y=0;y<32;y++)for(int x=0;x<33;x++){pixels[y*stride+x*3]=(byte)(y<16?210:30);pixels[y*stride+x*3+1]=20;pixels[y*stride+x*3+2]=(byte)(y<16?30:210);}
                Marshal.Copy(pixels,0,memory,pixels.Length);
                using(var negative=new Bitmap(33,32,-stride,PixelFormat.Format24bppRgb,IntPtr.Add(memory,stride*31)))
                using(var scaled=up.UpscaleBitmap(negative,null,CancellationToken.None)) Require(scaled.GetPixel(33,16).R>150 && scaled.GetPixel(33,48).B>150,"negative stride row orientation");
            } finally {Marshal.FreeHGlobal(memory);}
        }
        using(var cancel=new CancellationTokenSource())
        using(var up=new RealEsrganUpscaler(NativeRoot,Models,new RealEsrganOptions{TileSize=32})) {
            Console.WriteLine("upscale cancellation");
            bool stopped=false;
            try{up.UpscalePng(input,Path.Combine(dir,"cancel.png"),new CancelProgress(cancel),cancel.Token);}catch(OperationCanceledException){stopped=true;}
            Require(stopped && !File.Exists(Path.Combine(dir,"cancel.png")),"tile cancel did not publish output");
        }
        string unicode=Path.Combine(dir,"中文 模型");Directory.CreateDirectory(unicode);
        foreach(string ext in new[]{"param","bin"})File.Copy(Path.Combine(Models,"realesr-animevideov3-x2."+ext),Path.Combine(unicode,"realesr-animevideov3-x2."+ext));
        using(var up=new RealEsrganUpscaler(NativeRoot,unicode,new RealEsrganOptions()))up.UpscalePng(input,Path.Combine(dir,"retry.png"),null,CancellationToken.None);
        Require(exact,"GPU baseline differs; investigate version/color/precision before acceptance");
        Console.WriteLine("NativeMediaSmokeTests: PASS [upscale]");
    }
    private sealed class CancelProgress:IProgress<MediaProgress> { readonly CancellationTokenSource c;public CancelProgress(CancellationTokenSource c){this.c=c;}public void Report(MediaProgress p){if(p.Completed>0)c.Cancel();} }
    static bool Compare(string output,string baseline,int allowedMax,double allowedMae,string label)
    {
        using(var actual=new Bitmap(output))using(var expected=new Bitmap(baseline))using(var diffImage=new Bitmap(actual.Width,actual.Height)) {
            Require(actual.Size==expected.Size,"baseline size");long sum=0;int max=0;
            for(int y=0;y<actual.Height;y++)for(int x=0;x<actual.Width;x++) {
                Color a=actual.GetPixel(x,y),b=expected.GetPixel(x,y);
                diffImage.SetPixel(x,y,Color.FromArgb(Math.Min(255,Math.Abs(a.R-b.R)*32),Math.Min(255,Math.Abs(a.G-b.G)*32),Math.Min(255,Math.Abs(a.B-b.B)*32)));
                foreach(int diff in new[]{Math.Abs(a.R-b.R),Math.Abs(a.G-b.G),Math.Abs(a.B-b.B)}){sum+=diff;max=Math.Max(max,diff);}
            }
            diffImage.Save(output+"-"+label+"-diff32.png",ImageFormat.Png);
            Console.WriteLine("ESRGAN "+label+" "+Path.GetFileName(output)+": MAE="+(double)sum/(actual.Width*actual.Height*3)+" max="+max);
            return max<=allowedMax && (double)sum/(actual.Width*actual.Height*3)<=allowedMae;
        }
    }
}
