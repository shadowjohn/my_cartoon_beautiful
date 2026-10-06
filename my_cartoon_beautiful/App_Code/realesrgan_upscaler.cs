using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
namespace utility_app
{
    public sealed class RealEsrganOptions { public int Scale=2, GpuId=-1, TileSize=0; public bool TtaMode=false; }
    public sealed class RealEsrganUpscaler : IDisposable
    {
        private readonly object gate=new object();
        private readonly int scale;
        private RealEsrganHandle handle;
        public RealEsrganUpscaler(string nativeDirectory,string modelDirectory,RealEsrganOptions options)
        {
            if(options==null)throw new ArgumentNullException(nameof(options));
            RealEsrganNative.Load(nativeDirectory);scale=options.Scale;
            string model=Path.Combine(Path.GetFullPath(modelDirectory),"realesr-animevideov3-x"+scale);
            byte[] error=new byte[2048];IntPtr value;
            int code=RealEsrganNative.Create(model+".param",model+".bin",options.GpuId,scale,options.TileSize,options.TtaMode?1:0,out value,error,(uint)error.Length);
            if(code!=0)throw RealEsrganNative.Error(code,error);
            handle=new RealEsrganHandle(value);
        }
        public void UpscalePng(string input,string output,IProgress<MediaProgress> progress,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string destination=Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temporary=destination+".partial-"+Guid.NewGuid().ToString("N")+".png";
            long total=1;
            var tiles=new InlineProgress(p=>{total=p.Total??1;if(p.Completed<total)progress?.Report(p);});
            try {
                using(var bitmap=new Bitmap(input))using(var result=UpscaleBitmap(bitmap,tiles,token))result.Save(temporary,ImageFormat.Png);
                token.ThrowIfCancellationRequested();
                if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);
                progress?.Report(new MediaProgress{Stage="upscale",Completed=total,Total=total});
            } finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
        public Bitmap UpscaleBitmap(Bitmap input,IProgress<MediaProgress> progress,CancellationToken token)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            lock(gate) {
                if(handle==null || handle.IsClosed)throw new ObjectDisposedException(nameof(RealEsrganUpscaler));
                token.ThrowIfCancellationRequested();
                // GDI+ converts palette and premultiplied formats to straight BGRA before inference.
                Bitmap converted=null;
                Bitmap source=input;
                if(input.PixelFormat!=PixelFormat.Format24bppRgb && input.PixelFormat!=PixelFormat.Format32bppArgb) {
                    converted=new Bitmap(input.Width,input.Height,PixelFormat.Format32bppArgb);
                    using(var g=Graphics.FromImage(converted)){g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.DrawImage(input,0,0,input.Width,input.Height);}
                    source=converted;
                }
                try {
                    int channels=source.PixelFormat==PixelFormat.Format24bppRgb?3:4;
                    int row=checked(source.Width*channels), outWidth=checked(source.Width*scale),outHeight=checked(source.Height*scale),outRow=checked(row*scale);
                    byte[] pixels=new byte[checked(row*source.Height)], output=new byte[checked(outRow*outHeight)];
                    var data=source.LockBits(new Rectangle(0,0,source.Width,source.Height),ImageLockMode.ReadOnly,source.PixelFormat);
                    try { for(int y=0;y<source.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,checked(y*data.Stride)),pixels,y*row,row); }
                    finally {source.UnlockBits(data);}
                    byte[] error=new byte[2048];Exception callbackError=null;
                    RealEsrganNative.Progress callback=(user,done,total)=>{
                        try {progress?.Report(new MediaProgress{Stage="upscale",Completed=done,Total=total});}
                        catch(Exception e){callbackError=e;RealEsrganNative.Cancel(handle);}
                    };
                    int code;
                    // Dispose waits for the operation lock; registration.Dispose waits for a running cancel callback.
                    using(token.Register(()=>RealEsrganNative.Cancel(handle)))
                        code=RealEsrganNative.Process(handle,pixels,(ulong)pixels.Length,source.Width,source.Height,channels,row,
                            output,(ulong)output.Length,outRow,callback,IntPtr.Zero,error,(uint)error.Length);
                    GC.KeepAlive(callback);
                    if(callbackError!=null)ExceptionDispatchInfo.Capture(callbackError).Throw();
                    token.ThrowIfCancellationRequested();
                    if(code==1)throw new OperationCanceledException("Real-ESRGAN session cancelled",token);
                    if(code!=0)throw RealEsrganNative.Error(code,error);
                    var result=new Bitmap(outWidth,outHeight,source.PixelFormat);
                    try {
                        var resultData=result.LockBits(new Rectangle(0,0,outWidth,outHeight),ImageLockMode.WriteOnly,result.PixelFormat);
                        try {for(int y=0;y<outHeight;y++)Marshal.Copy(output,y*outRow,IntPtr.Add(resultData.Scan0,checked(y*resultData.Stride)),outRow);}
                        finally {result.UnlockBits(resultData);}
                        return result;
                    } catch {result.Dispose();throw;}
                } finally {converted?.Dispose();}
            }
        }
        private sealed class InlineProgress:IProgress<MediaProgress> { readonly Action<MediaProgress> report;internal InlineProgress(Action<MediaProgress> report){this.report=report;}public void Report(MediaProgress p){report(p);} }
        public static void ShutdownRuntime() { RealEsrganNative.Shutdown(); }
        public void Dispose() {lock(gate){handle?.Dispose();handle=null;}}
    }
}
