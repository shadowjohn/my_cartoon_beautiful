using System;
using System.Drawing;
using System.IO;
using System.Threading;
using utility_app;
internal static class DecodeTests
{
    private sealed class CancelProgress : IProgress<MediaProgress> { readonly CancellationTokenSource source; public CancelProgress(CancellationTokenSource s){source=s;} public void Report(MediaProgress p){if(p.Completed>=10)source.Cancel();} }
    public static void Run(IMediaBackend backend)
    {
        string fixtures=Path.GetFullPath("artifacts/media-tests/fixtures");
        foreach(string name in new[]{"sync-30fps","sync-24fps","sync-vfr","sync-no-audio"})
        {
            string input=Path.Combine(fixtures,name+".mp4");
            var info=backend.Probe(input,CancellationToken.None);
            if(!info.HasVideo || info.Width!=96 || info.Height!=64)throw new Exception("probe dimensions");
            if(info.HasAudio==(name=="sync-no-audio"))throw new Exception("probe audio flag");
            string output=Path.GetFullPath("artifacts/media-tests/dll-"+name+"-"+Guid.NewGuid().ToString("N"));
            var frames=backend.ExtractFrames(input,output,null,CancellationToken.None);
            int expected=name=="sync-vfr"?59:60;
            if(frames.Count!=expected || frames.Width!=96 || frames.Height!=64)throw new Exception(name+" count expected "+expected+", got "+frames.Count);
            if(!File.Exists(Path.Combine(output,expected.ToString("D8")+".png")))throw new Exception("last PNG missing");
            if(name=="sync-30fps")using(var bitmap=new Bitmap(Path.Combine(output,"00000031.png")))
            { Color color=bitmap.GetPixel(40,30); if(color.R<245||color.G<245||color.B<245)throw new Exception("flash frame color"); }
        }
        using(var cts=new CancellationTokenSource())
        {
            cts.Cancel(); bool cancelled=false;
            try{backend.ExtractFrames(Path.Combine(fixtures,"sync-30fps.mp4"),"artifacts/media-tests/cancel",null,cts.Token);}
            catch(OperationCanceledException){cancelled=true;}
            if(!cancelled)throw new Exception("pre-cancel ignored");
        }
        foreach(string variant in new[]{"rotate","offset","bframes","sar"})
        {
            var f=backend.ExtractFrames(Path.Combine(fixtures,"sync-"+variant+".mp4"),Path.GetFullPath("artifacts/media-tests/variant-"+Guid.NewGuid().ToString("N")),null,CancellationToken.None);
            if(f.Count!=60)throw new Exception(variant+" frame count "+f.Count);
            if(variant=="rotate" && (f.Width!=64||f.Height!=96))throw new Exception("rotation dimensions");
            if(variant=="sar" && f.SarNumerator*3!=f.SarDenominator*4)throw new Exception("sample aspect ratio lost");
            if(variant=="offset" && Math.Abs(f.SourceStartTime.TotalSeconds-2)>1.0/30)throw new Exception("source timeline lost");
        }
        string special=Path.GetFullPath("artifacts/media-tests/中文 路徑"); Directory.CreateDirectory(special);
        string copied=Path.Combine(special,"來源 影片.mp4");File.Copy(Path.Combine(fixtures,"sync-30fps.mp4"),copied,true);
        var unicodeFrames=backend.ExtractFrames(copied,Path.Combine(special,Guid.NewGuid().ToString("N")),null,CancellationToken.None);
        if(unicodeFrames.Count!=60)throw new Exception("Unicode path");
        string corrupt=Path.Combine(special,"broken.mp4");File.WriteAllText(corrupt,"not a video");
        bool rejected=false;try{backend.Probe(corrupt,CancellationToken.None);}catch(NativeMediaException){rejected=true;}
        if(!rejected)throw new Exception("corrupt media accepted");
        using(var cts=new CancellationTokenSource())
        {
            bool stopped=false; try{backend.ExtractFrames(copied,Path.Combine(special,Guid.NewGuid().ToString("N")),new CancelProgress(cts),cts.Token);}catch(OperationCanceledException){stopped=true;}
            if(!stopped)throw new Exception("running decode ignored cancellation");
        }
        Console.WriteLine("NativeMediaSmokeTests: PASS [decode]");
    }
}
