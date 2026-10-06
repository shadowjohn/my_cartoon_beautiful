using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using utility_app;
using my_cartoon_beautiful;
internal static class ReleaseTests
{
    public static void Run(string package,string fixture,string result,bool gpu)
    {
        package=Path.GetFullPath(package);
        var setup=new AppDomainSetup{ApplicationBase=package,ConfigurationFile=Path.Combine(package,"my_cartoon_beautiful.exe.config")};
        var domain=AppDomain.CreateDomain("Isolated release probe",null,setup);
        try {
            var probe=(ReleaseProbe)domain.CreateInstanceFromAndUnwrap(Assembly.GetExecutingAssembly().Location,typeof(ReleaseProbe).FullName);
            probe.Load(package);probe.Run(package,Path.GetFullPath(fixture),Path.GetFullPath(result),gpu);
        } finally {
            var cleanup=(ReleaseProbe)domain.CreateInstanceFromAndUnwrap(Assembly.GetExecutingAssembly().Location,typeof(ReleaseProbe).FullName);
            try { cleanup.Shutdown(); } finally { AppDomain.Unload(domain); }
        }
    }
}
public sealed class ReleaseProbe:MarshalByRefObject
{
    public void Shutdown() { RealEsrganUpscaler.ShutdownRuntime(); }
    public void Load(string package)
    {
        foreach(string name in new[]{"System.Memory.dll","System.Buffers.dll","System.Numerics.Vectors.dll","System.Runtime.CompilerServices.Unsafe.dll","System.Resources.Extensions.dll","FFmpeg.AutoGen.dll","my_cartoon_beautiful.exe"}) {
            string path=Path.Combine(package,name);
            if(!File.Exists(path))throw new FileNotFoundException("Missing staged managed assembly",path);
            var assembly=Assembly.LoadFrom(path);
            if(!string.Equals(assembly.Location,path,StringComparison.OrdinalIgnoreCase))throw new Exception("Assembly resolved outside package: "+assembly.Location);
        }
    }
    public void Run(string package,string fixture,string result,bool gpu)
    {
        if(!string.Equals(typeof(FfmpegRuntime).Assembly.Location,Path.Combine(package,"my_cartoon_beautiful.exe"),StringComparison.OrdinalIgnoreCase))throw new Exception("Probe used developer app assembly");
        Directory.CreateDirectory(result);
        using(var form=new Form1()){form.Show();Application.DoEvents();form.Close();}
        var runtime=FfmpegRuntime.Load(Path.Combine(package,"binary","native","ffmpeg"));
        File.WriteAllText(Path.Combine(result,"runtime.txt"),runtime.Version+Environment.NewLine+runtime.License+Environment.NewLine+runtime.Configuration);
        var backend=new FfmpegMediaBackend(runtime);
        var source=backend.ExtractFrames(fixture,Path.Combine(result,"source"),null,CancellationToken.None);
        var audio=backend.ExtractAudio(fixture,Path.Combine(result,"audio"),AudioMode.Aac,null,CancellationToken.None);
        for(int scale=1;scale<=(gpu?2:1);scale++) {
            var frames=PngFrameUpscaler.Run(source,Path.Combine(result,"x"+scale),scale,Path.Combine(package,"binary","native","realesrgan"),Path.Combine(package,"binary","realesrgan-ncnn-vulkan-v0.2.0-windows","models"),null,CancellationToken.None);
            string target=Path.Combine(result,"output-x"+scale+".mp4");backend.EncodeMp4(frames,audio,target,EncoderPreference.SoftwareOnly,null,CancellationToken.None);
            var info=backend.Probe(target,CancellationToken.None);
            if(!info.HasAudio || !info.HasVideo || info.Width!=source.Width*scale || info.Height!=source.Height*scale)throw new Exception("Staged pipeline failed");
        }
        // Exercise the product's real stage adapter and UI progress dispatcher, using only staged dependencies.
        using(var form=new Form1()) {
            form.Show();Application.DoEvents();
            var job=RunProductStages(form,fixture,result,gpu?2:1);
            while(!job.IsCompleted){Application.DoEvents();Thread.Sleep(1);}
            job.GetAwaiter().GetResult();form.Close();
        }
        Console.WriteLine("NativeMediaSmokeTests: PASS [isolated package x1"+(gpu?"/x2":"; GPU NOT-RUN")+"] "+package);
    }
    private static async Task RunProductStages(Form1 form,string fixture,string result,int scale)
    {
        var app=(myApp)typeof(Form1).GetField("App",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
        form.comboBox_ImageScale.SelectedIndex=scale-1;
        string work=Path.Combine(result,"product-stages"),target=Path.Combine(result,"product-output.mp4");
        if(!app.step1_checkWorkPath(work) || !await app.step2_sourceFile_to_png(work,fixture,CancellationToken.None) ||
           !await app.step3_sourceFile_to_wav(work,fixture,target,CancellationToken.None) ||
           !await app.step4_sourcePng_to_aiPng(work,CancellationToken.None) ||
           !await app.step5_aiPng_to_mp4(work,target,CancellationToken.None))throw new Exception("Product stage pipeline failed");
        if(!Directory.Exists(Path.Combine(work,"source")) || !Directory.Exists(Path.Combine(work,"target")))throw new Exception("Keep-temp stage files missing");
        if(!await app.step6_remove_workPath(work,CancellationToken.None) || Directory.Exists(work))throw new Exception("Product cleanup failed");
    }

}
