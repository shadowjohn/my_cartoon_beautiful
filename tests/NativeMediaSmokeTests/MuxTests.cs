using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using utility_app;
internal static class MuxTests
{
    private sealed class CancelProgress:IProgress<MediaProgress> { readonly CancellationTokenSource c;public CancelProgress(CancellationTokenSource c){this.c=c;}public void Report(MediaProgress p){if(p.Completed>=2)c.Cancel();} }
    public static void Run(IMediaBackend backend)
    {
        string dir=Path.GetFullPath("artifacts/media-tests/mux-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var frames=backend.ExtractFrames("artifacts/media-tests/fixtures/sync-30fps.mp4",Path.Combine(dir,"frames"),null,CancellationToken.None);
        AudioAsset aac=null;
        foreach(AudioMode mode in Enum.GetValues(typeof(AudioMode))) {
            var audio=backend.ExtractAudio("artifacts/media-tests/fixtures/sync-30fps.mp4",Path.Combine(dir,mode.ToString()),mode,null,CancellationToken.None);
            if(mode==AudioMode.Aac)aac=audio;
            string output=Path.Combine(dir,mode+".mp4");backend.EncodeMp4(frames,audio,output,EncoderPreference.SoftwareOnly,null,CancellationToken.None);
            var info=backend.Probe(output,CancellationToken.None);
            if(!info.HasAudio || !info.HasVideo || info.Width!=96 || info.Height!=64 || !info.Duration.HasValue || Math.Abs(info.Duration.Value.TotalSeconds-2)>1.0/30)throw new Exception("MP4 streams/duration "+mode+" "+info.Duration);
            var decoded=backend.ExtractFrames(output,Path.Combine(dir,"decoded-"+mode),null,CancellationToken.None);
            if(decoded.Count!=60)throw new Exception("MP4 frame flush "+decoded.Count);
            using(var white=new Bitmap(Path.Combine(decoded.DirectoryPath,"00000031.png")))if(white.GetPixel(20,20).R<245)throw new Exception("flash timestamp");
            var pcm=backend.ExtractAudio(output,Path.Combine(dir,"pcm-"+mode),AudioMode.PcmWav,null,CancellationToken.None);
            AudioTests.MeasureWave(pcm.Path,mode);
        }
        string existing=Path.Combine(dir,"existing.mp4");byte[] original={1,2,3,4};File.WriteAllBytes(existing,original);
        bool failed=false;
        var broken=new FrameSequence{DirectoryPath=Path.Combine(dir,"missing"),Count=60,Width=96,Height=64};
        try{backend.EncodeMp4(broken,aac,existing,EncoderPreference.SoftwareOnly,null,CancellationToken.None);}catch(Exception){failed=true;}
        if(!failed || !File.ReadAllBytes(existing).SequenceEqual(original))throw new Exception("failed mux replaced existing target");
        using(var cancel=new CancellationTokenSource()) {
            bool cancelled=false;try{backend.EncodeMp4(frames,aac,existing,EncoderPreference.SoftwareOnly,new CancelProgress(cancel),cancel.Token);}catch(OperationCanceledException){cancelled=true;}
            if(!cancelled || !File.ReadAllBytes(existing).SequenceEqual(original))throw new Exception("cancelled mux replaced existing target");
        }
        backend.EncodeMp4(frames,aac,existing,EncoderPreference.PreferNvenc,null,CancellationToken.None);
        if(File.ReadAllBytes(existing).SequenceEqual(original))throw new Exception("successful mux did not replace target");
        Console.WriteLine("Fallback encoder: "+((FfmpegMediaBackend)backend).LastEncoderName);
        for(int scale=1;scale<=4;scale++) {
            var scaled=PngFrameUpscaler.Run(frames,Path.Combine(dir,"x"+scale),scale,UpscaleTests.NativeRoot,UpscaleTests.Models,null,CancellationToken.None);
            string path=Path.Combine(dir,"pipeline-x"+scale+".mp4");
            backend.EncodeMp4(scaled,aac,path,EncoderPreference.PreferNvenc,null,CancellationToken.None);
            var check=backend.ExtractFrames(path,Path.Combine(dir,"roundtrip-x"+scale),null,CancellationToken.None);
            if(check.Count!=60 || check.Width!=96*scale || check.Height!=64*scale)throw new Exception("pipeline dimensions/count x"+scale);
            using(var white=new Bitmap(Path.Combine(check.DirectoryPath,"00000031.png")))if(white.GetPixel(20*scale,20*scale).R<240)throw new Exception("pipeline flash x"+scale);
            Console.WriteLine("Full pipeline x"+scale+": "+((FfmpegMediaBackend)backend).LastEncoderName+" 60 frames");
        }
        Console.WriteLine("NativeMediaSmokeTests: PASS [mux]");
    }
}
