using System;
using System.IO;
using System.Threading;
using utility_app;
internal static class AudioTests
{
    private sealed class CancelProgress:IProgress<MediaProgress>{readonly CancellationTokenSource cts;public CancelProgress(CancellationTokenSource c){cts=c;}public void Report(MediaProgress p){if(p.Completed>0)cts.Cancel();}}
    internal static void MeasureWave(string path,AudioMode mode)
    {
        using(var reader=new BinaryReader(File.OpenRead(path)))
        {
            reader.ReadBytes(12); int rate=0,channels=0,bits=0; byte[] data=null;
            while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
            {
                string id=new string(reader.ReadChars(4));int size=reader.ReadInt32();long next=reader.BaseStream.Position+size+(size&1);
                if(size<0||next>reader.BaseStream.Length)throw new Exception("invalid WAV chunk");
                if(id=="fmt "){reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadUInt16();bits=reader.ReadUInt16();}
                if(id=="data")data=reader.ReadBytes(size);
                reader.BaseStream.Position=next;
            }
            if(data==null||rate!=48000||channels!=1||bits!=16)throw new Exception("decoded PCM format");
            double duration=data.Length/2.0/rate;int onset=-1;
            for(int i=0;i<data.Length/2;i++)if(Math.Abs((int)BitConverter.ToInt16(data,i*2))>=6553){onset=i;break;}
            double seconds=(double)onset/rate;
            if(Math.Abs(duration-2)>0.05||onset<0||Math.Abs(seconds-1)>1.0/30)throw new Exception(mode+" PCM timing "+duration+" click "+seconds);
            Console.WriteLine("Audio "+mode+": duration="+duration+" click="+seconds);
        }
    }
    public static void Run(IMediaBackend backend)
    {
        string input=Path.GetFullPath("artifacts/media-tests/fixtures/sync-30fps.mp4");
        foreach(AudioMode mode in Enum.GetValues(typeof(AudioMode)))
        {
            string output=Path.GetFullPath("artifacts/media-tests/audio-"+Guid.NewGuid().ToString("N"));
            var asset=backend.ExtractAudio(input,output,mode,null,CancellationToken.None);
            if(!File.Exists(asset.Path)||new FileInfo(asset.Path).Length==0)throw new Exception("empty audio "+mode);
            var info=backend.Probe(asset.Path,CancellationToken.None);
            if(!info.HasAudio||info.HasVideo)throw new Exception("wrong streams "+mode);
            if(Math.Abs(asset.Duration.TotalSeconds-2)>0.05)throw new Exception("encoded sample duration "+mode);
            var pcm=backend.ExtractAudio(asset.Path,output+"-decoded",AudioMode.PcmWav,null,CancellationToken.None);
            MeasureWave(pcm.Path,mode);
        }
        bool rejected=false;
        try{backend.ExtractAudio("artifacts/media-tests/fixtures/sync-no-audio.mp4","artifacts/media-tests/no-audio",AudioMode.Aac,null,CancellationToken.None);}
        catch(NativeMediaException){rejected=true;}
        if(!rejected)throw new Exception("no audio accepted");
        using(var cts=new CancellationTokenSource())
        {
            cts.Cancel();bool cancelled=false;
            try{backend.ExtractAudio(input,"artifacts/media-tests/cancel-audio",AudioMode.Aac,null,cts.Token);}catch(OperationCanceledException){cancelled=true;}
            if(!cancelled)throw new Exception("audio cancellation ignored");
        }
        using(var cts=new CancellationTokenSource())
        {
            bool cancelled=false;try{backend.ExtractAudio(input,"artifacts/media-tests/cancel-running",AudioMode.Aac,new CancelProgress(cts),cts.Token);}catch(OperationCanceledException){cancelled=true;}
            if(!cancelled)throw new Exception("running audio cancellation ignored");
        }
        backend.ExtractAudio(input,"artifacts/media-tests/retry-audio",AudioMode.PcmWav,null,CancellationToken.None);
        Console.WriteLine("NativeMediaSmokeTests: PASS [audio]");
    }
}
