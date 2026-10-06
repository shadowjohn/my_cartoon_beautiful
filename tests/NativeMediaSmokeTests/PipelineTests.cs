using System;
using System.IO;
using System.Linq;
using System.Threading;
using utility_app;
internal static class PipelineTests
{
    public static void Run(IMediaBackend backend)
    {
        string dir=Path.GetFullPath("artifacts/media-tests/pipeline-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var frames=backend.ExtractFrames("artifacts/media-tests/fixtures/sync-30fps.mp4",Path.Combine(dir,"source"),null,CancellationToken.None);
        var x1=PngFrameUpscaler.Run(frames,Path.Combine(dir,"x1"),1,"missing native","missing models",null,CancellationToken.None);
        if(x1.Count!=60 || !File.ReadAllBytes(Path.Combine(x1.DirectoryPath,"00000060.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(frames.DirectoryPath,"00000060.png"))))throw new Exception("x1 copy bypass");
        File.Copy(Path.Combine(frames.DirectoryPath,"00000001.png"),Path.Combine(frames.DirectoryPath,"00000002.png"),true);
        var x2=PngFrameUpscaler.Run(frames,Path.Combine(dir,"x2"),2,UpscaleTests.NativeRoot,UpscaleTests.Models,null,CancellationToken.None);
        if(x2.Count!=60 || x2.Width!=192 || Directory.GetFiles(x2.DirectoryPath,"*.png").Length!=60 || x2.UniqueFrameCount<=0 || x2.UniqueFrameCount>=60)throw new Exception("unique inference/frame restoration");
        Console.WriteLine("Pipeline SHA256 unique "+x2.UniqueFrameCount+"/"+x2.Count);
        bool missing=false;try{PngFrameUpscaler.Run(frames,Path.Combine(dir,"missing"),2,UpscaleTests.NativeRoot,Path.Combine(dir,"models-missing"),null,CancellationToken.None);}catch(NativeMediaException){missing=true;}
        if(!missing)throw new Exception("pipeline missing model accepted");
        Console.WriteLine("NativeMediaSmokeTests: PASS [pipeline-stages]");
    }
}
