using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
namespace utility_app
{
    public static class PngFrameUpscaler
    {
        public static FrameSequence Run(FrameSequence source,string target,int scale,string nativeDirectory,string modelDirectory,IProgress<MediaProgress> progress,CancellationToken token)
        {
            if(source==null || source.Count<=0 || scale<1 || scale>4)throw new ArgumentException("Invalid frame sequence or scale");
            token.ThrowIfCancellationRequested();
            if(Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length!=0)throw new IOException("Target PNG directory must be empty");
            Directory.CreateDirectory(target);
            var result=new FrameSequence{DirectoryPath=Path.GetFullPath(target),Count=source.Count,Width=checked(source.Width*scale),Height=checked(source.Height*scale),
                SarNumerator=source.SarNumerator,SarDenominator=source.SarDenominator,SourceStartTime=source.SourceStartTime};
            if(scale==1) {
                for(int i=1;i<=source.Count;i++) {token.ThrowIfCancellationRequested();File.Copy(FramePath(source.DirectoryPath,i),FramePath(target,i));progress?.Report(new MediaProgress{Stage="copy",Completed=i,Total=source.Count});}
                result.UniqueFrameCount=source.Count;return result;
            }
            var hashes=new Dictionary<string,int>(StringComparer.Ordinal);
            var originals=new int[source.Count];
            using(var sha=SHA256.Create())for(int i=1;i<=source.Count;i++) {
                token.ThrowIfCancellationRequested();string hash;
                using(var stream=File.OpenRead(FramePath(source.DirectoryPath,i)))hash=Convert.ToBase64String(sha.ComputeHash(stream));
                int original;if(!hashes.TryGetValue(hash,out original)){original=i;hashes.Add(hash,i);}
                originals[i-1]=original;progress?.Report(new MediaProgress{Stage="hash",Completed=i,Total=source.Count});
            }
            result.UniqueFrameCount=hashes.Count;
            using(var upscaler=new RealEsrganUpscaler(nativeDirectory,modelDirectory,new RealEsrganOptions{Scale=scale})) {
                int done=0;
                for(int i=1;i<=source.Count;i++) {
                    token.ThrowIfCancellationRequested();
                    if(originals[i-1]!=i) {File.Copy(FramePath(target,originals[i-1]),FramePath(target,i));continue;}
                    var tiles=new InlineProgress(p=>progress?.Report(new MediaProgress{Stage="upscale",Completed=done*10000L+(p.Total>0?p.Completed*10000L/p.Total.Value:0),Total=hashes.Count*10000L}));
                    upscaler.UpscalePng(FramePath(source.DirectoryPath,i),FramePath(target,i),tiles,token);done++;
                }
            }
            token.ThrowIfCancellationRequested();return result;
        }
        internal static string FramePath(string directory,int index) {return Path.Combine(directory,index.ToString("D8")+".png");}
        private sealed class InlineProgress:IProgress<MediaProgress> {readonly Action<MediaProgress> action;public InlineProgress(Action<MediaProgress> action){this.action=action;}public void Report(MediaProgress p){action(p);}}
    }
}
