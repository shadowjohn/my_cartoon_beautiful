using System;
using System.Threading;
using FFmpeg.AutoGen;
namespace utility_app
{
    public sealed unsafe class FfmpegMediaBackend : IMediaBackend
    {
        public FfmpegMediaBackend(FfmpegRuntime runtime) { if(runtime==null)throw new ArgumentNullException("runtime"); }
        public MediaInfo Probe(string input, CancellationToken token) { using(var media=new FfmpegInput(input,token))
            {
                var result=new MediaInfo();
                if(media.Format->duration!=ffmpeg.AV_NOPTS_VALUE)result.Duration=TimeSpan.FromSeconds((double)media.Format->duration/ffmpeg.AV_TIME_BASE);
                int video=ffmpeg.av_find_best_stream(media.Format,AVMediaType.AVMEDIA_TYPE_VIDEO,-1,-1,null,0);
                int audio=ffmpeg.av_find_best_stream(media.Format,AVMediaType.AVMEDIA_TYPE_AUDIO,-1,-1,null,0);
                result.HasVideo=video>=0;result.HasAudio=audio>=0;
                if(video>=0){var par=media.Format->streams[video]->codecpar;result.Width=par->width;result.Height=par->height;}
                return result;
            } }
        public FrameSequence ExtractFrames(string input,string directory,IProgress<MediaProgress> progress,CancellationToken token) { using(var media=new FfmpegInput(input,token,AVMediaType.AVMEDIA_TYPE_VIDEO))
            using(var decoder=new FfmpegVideoDecoder(media,directory,progress,token))return decoder.Run(); }
        public AudioAsset ExtractAudio(string input,string outputBase,AudioMode mode,IProgress<MediaProgress> progress,CancellationToken token) { using(var media=new FfmpegInput(input,token,AVMediaType.AVMEDIA_TYPE_AUDIO))
            using(var encoder=new FfmpegAudioTranscoder(media,outputBase,mode,progress,token))return encoder.Run(); }
        public string LastEncoderName { get; private set; }
        public void EncodeMp4(FrameSequence frames,AudioAsset audio,string output,EncoderPreference encoder,IProgress<MediaProgress> progress,CancellationToken token) { using(var muxer=new FfmpegVideoMuxer(frames,audio,progress,token)){muxer.Run(output,encoder);LastEncoderName=muxer.EncoderName;} }
    }
}
