using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using utility_app;
internal static class EnduranceTests
{
    public static void Run(IMediaBackend backend,bool shortOnly=false)
    {
        string dir=Path.GetFullPath("artifacts/acceptance/endurance-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        using(var log=new StreamWriter(Path.Combine(dir,"jobs.csv"))) {
            log.AutoFlush=true;log.WriteLine("job,frames,unique,seconds,privateBytes,handles");
            Console.WriteLine("ENDURANCE pid="+Process.GetCurrentProcess().Id+" report="+dir);
            for(int i=1;i<=(shortOnly?5:6);i++) {
                string job=Path.Combine(dir,"job"+i);Directory.CreateDirectory(job);var watch=Stopwatch.StartNew();
                Console.WriteLine("ENDURANCE job="+i+" start");
                string source=i<=5?"artifacts/media-tests/fixtures/sync-30fps.mp4":"artifacts/media-tests/fixtures/endurance-10min.mp4";
                var frames=backend.ExtractFrames(source,Path.Combine(job,"source"),null,CancellationToken.None);
                using(var snapshot=Process.GetCurrentProcess())Console.WriteLine("HANDLES decode "+snapshot.HandleCount);
                var audio=backend.ExtractAudio(source,Path.Combine(job,"audio"),AudioMode.Aac,null,CancellationToken.None);
                using(var snapshot=Process.GetCurrentProcess())Console.WriteLine("HANDLES audio "+snapshot.HandleCount);
                var output=PngFrameUpscaler.Run(frames,Path.Combine(job,"target"),2,UpscaleTests.NativeRoot,UpscaleTests.Models,null,CancellationToken.None);
                using(var snapshot=Process.GetCurrentProcess())Console.WriteLine("HANDLES upscale "+snapshot.HandleCount);
                backend.EncodeMp4(output,audio,Path.Combine(job,"output.mp4"),EncoderPreference.PreferNvenc,null,CancellationToken.None);
                using(var snapshot=Process.GetCurrentProcess())Console.WriteLine("HANDLES mux "+snapshot.HandleCount);
                if(frames.Count!=(i<=5?60:18000))throw new Exception("Endurance incomplete frames");
                watch.Stop();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Thread.Sleep(1000);
                using(var process=Process.GetCurrentProcess())log.WriteLine(i+","+frames.Count+","+output.UniqueFrameCount+","+watch.Elapsed.TotalSeconds+","+process.PrivateMemorySize64+","+process.HandleCount);
                Console.WriteLine("ENDURANCE job="+i+" idle frames="+frames.Count+" unique="+output.UniqueFrameCount);
                Thread.Sleep(2000);
            }
        }
        if(shortOnly)for(int i=0;i<12;i++){Thread.Sleep(5000);using(var snapshot=Process.GetCurrentProcess())Console.WriteLine("DELAYED IDLE "+(i+1)*5+"s handles="+snapshot.HandleCount);}
        Console.WriteLine("NativeMediaSmokeTests: PASS [endurance]");
    }
}
