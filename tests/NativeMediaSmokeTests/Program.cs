using System;
using System.IO;
using System.Linq;
using utility_app;
internal static class Program
{
    static int Main(string[] args)
    {
        bool expectAbiFailure = args.Contains("--expect-abi-failure");
        try
        {
            string native = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("artifacts/native-runtime/ffmpeg");
            if (!Environment.Is64BitProcess) throw new Exception("Expected x64 process");
            var runtime = FfmpegRuntime.Load(native);
            if (string.IsNullOrWhiteSpace(runtime.Version)) throw new Exception("No FFmpeg version");
            bool missingRejected = false;
            try { FfmpegRuntime.Load(Path.Combine(native, "missing")); }
            catch (Exception e) { missingRejected = !(e is NotImplementedException); }
            if (!missingRejected) throw new Exception("Missing library directory accepted");
            string original = Environment.CurrentDirectory;
            try { Environment.CurrentDirectory = Path.GetTempPath(); FfmpegRuntime.Load(native); }
            finally { Environment.CurrentDirectory = original; }
            RuntimeTests.Run();
            if(args.Contains("--pipeline-stages")) PipelineTests.Run(new FfmpegMediaBackend(runtime));
            if(args.Contains("--upscale")) UpscaleTests.Run();
            if(args.Contains("--lifecycle")) LifecycleTests.Run();
            if(args.Contains("--mux")) MuxTests.Run(new FfmpegMediaBackend(runtime));
            if(args.Contains("--audio")) AudioTests.Run(new FfmpegMediaBackend(runtime));
            if(args.Contains("--decode")) DecodeTests.Run(new FfmpegMediaBackend(runtime));
            if(expectAbiFailure) throw new Exception("Wrong ABI accepted");
            Console.WriteLine("NativeMediaSmokeTests: PASS [runtime] " + runtime.Version);
            return 0;
        }
        catch (Exception e) { if(expectAbiFailure && e is NativeMediaException && e.Message.Contains("ABI mismatch")) { Console.WriteLine("NativeMediaSmokeTests: PASS [ABI rejection]"); return 0; } Console.Error.WriteLine(e); return 1; }
    }
}
