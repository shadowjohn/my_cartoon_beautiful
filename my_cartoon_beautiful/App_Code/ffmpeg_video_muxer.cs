using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using FFmpeg.AutoGen;
namespace utility_app
{
    internal sealed unsafe class FfmpegVideoMuxer : IDisposable
    {
        private readonly FrameSequence frames;
        private readonly AudioAsset audio;
        private readonly CancellationToken token;
        private readonly IProgress<MediaProgress> progress;
        private readonly AVIOInterruptCB_callback interrupt;
        private FfmpegInput audioInput;
        private int audioIndex;
        private AVFormatContext* output;
        private AVCodecContext* encoder;
        private AVStream* videoStream;
        private AVStream* audioStream;
        private AVFrame* frame;
        private AVPacket* videoPacket;
        private AVPacket* audioPacket;
        private SwsContext* scaler;
        private bool audioPending, audioEof;
        private long audioOffset;
        private long videoOffset;
        internal string EncoderName { get; private set; }
        internal FfmpegVideoMuxer(FrameSequence frames, AudioAsset audio, IProgress<MediaProgress> progress, CancellationToken token)
        {
            this.frames=frames;this.audio=audio;this.progress=progress;this.token=token;
            interrupt=delegate(void* opaque){return token.IsCancellationRequested?1:0;};
        }
        internal void Run(string destination, EncoderPreference preference)
        {
            token.ThrowIfCancellationRequested();
            if(frames==null || frames.Count<=0 || frames.Width<=0 || frames.Height<=0 || (frames.Width&1)!=0 || (frames.Height&1)!=0)
                throw new NativeMediaException("encode",-1,"H.264 yuv420p requires a nonempty sequence with even width and height");
            if(audio==null)throw new ArgumentNullException(nameof(audio));
            string final=Path.GetFullPath(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(final));
            string temporary=final+"."+Guid.NewGuid().ToString("N")+".partial.mp4";
            try {
                Open(temporary,preference);
                for(int i=0;i<frames.Count;i++) {
                    token.ThrowIfCancellationRequested();
                    Check(ffmpeg.av_frame_make_writable(frame),"writable video frame");
                    using(var bitmap=new Bitmap(PngFrameUpscaler.FramePath(frames.DirectoryPath,i+1))) {
                        if(bitmap.Width!=frames.Width || bitmap.Height!=frames.Height)throw new NativeMediaException("encode",-1,"PNG dimensions changed at frame "+(i+1));
                        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
                        try {
                            byte_ptrArray4 source=new byte_ptrArray4();int_array4 stride=new int_array4();source[0]=(byte*)data.Scan0;stride[0]=data.Stride;
                            int rows=ffmpeg.sws_scale(scaler,source,stride,0,frames.Height,frame->data,frame->linesize);
                            if(rows!=frames.Height)throw new NativeMediaException("encode",rows,"Incomplete PNG conversion");
                        } finally {bitmap.UnlockBits(data);}
                    }
                    frame->pts=i+videoOffset;
                    Check(ffmpeg.avcodec_send_frame(encoder,frame),"encode video");DrainVideo();
                    DrainAudio((i+videoOffset+1)/30.0);
                    progress?.Report(new MediaProgress{Stage="encode",Completed=i+1,Total=frames.Count});
                }
                Check(ffmpeg.avcodec_send_frame(encoder,null),"flush video encoder");DrainVideo();DrainAudio(double.PositiveInfinity);
                Check(ffmpeg.av_write_trailer(output),"MP4 trailer");
                Check(ffmpeg.avio_closep(&output->pb),"close MP4");
                Validate(temporary);
                token.ThrowIfCancellationRequested();
                if(File.Exists(final))File.Replace(temporary,final,null);else File.Move(temporary,final);
            } finally {Dispose();if(File.Exists(temporary))File.Delete(temporary);}
        }
        private void Open(string path,EncoderPreference preference)
        {
            AVFormatContext* format=null;Check(ffmpeg.avformat_alloc_output_context2(&format,null,"mp4",path),"allocate MP4");output=format;
            if(output==null)throw new OutOfMemoryException();
            output->interrupt_callback=new AVIOInterruptCB{callback=interrupt};
            // libavformat's automatic aac_adtstoasc filter carries ADTS extradata into MP4.
            output->flags|=ffmpeg.AVFMT_FLAG_AUTO_BSF;
            if(preference!=EncoderPreference.PreferNvenc || !TryEncoder("h264_nvenc")) {
                if(!TryEncoder("libopenh264"))throw new NativeMediaException("encode",-1,"Cannot initialize software H.264 encoder libopenh264");
            }
            videoStream=ffmpeg.avformat_new_stream(output,null);if(videoStream==null)throw new OutOfMemoryException();
            videoStream->time_base=encoder->time_base;videoStream->avg_frame_rate=encoder->framerate;videoStream->sample_aspect_ratio=encoder->sample_aspect_ratio;
            Check(ffmpeg.avcodec_parameters_from_context(videoStream->codecpar,encoder),"video stream parameters");
            audioInput=new FfmpegInput(audio.Path,token);
            audioIndex=ffmpeg.av_find_best_stream(audioInput.Format,AVMediaType.AVMEDIA_TYPE_AUDIO,-1,-1,null,0);Check(audioIndex,"find remux audio");
            AVStream* inputStream=audioInput.Format->streams[audioIndex];
            audioStream=ffmpeg.avformat_new_stream(output,null);if(audioStream==null)throw new OutOfMemoryException();
            Check(ffmpeg.avcodec_parameters_copy(audioStream->codecpar,inputStream->codecpar),"copy audio parameters");audioStream->codecpar->codec_tag=0;
            audioStream->time_base=inputStream->time_base;
            double origin=Math.Min(frames.SourceStartTime.TotalSeconds,audio.SourceStartTime.TotalSeconds);
            videoOffset=(long)Math.Round((frames.SourceStartTime.TotalSeconds-origin)*30);
            audioOffset=(long)Math.Round((audio.SourceStartTime.TotalSeconds-origin)/ffmpeg.av_q2d(inputStream->time_base));
            if(inputStream->start_time!=ffmpeg.AV_NOPTS_VALUE)audioOffset-=inputStream->start_time;
            Check(ffmpeg.avio_open2(&output->pb,path,ffmpeg.AVIO_FLAG_WRITE,&output->interrupt_callback,null),"open MP4 output");
            Check(ffmpeg.avformat_write_header(output,null),"MP4 header");
            frame=ffmpeg.av_frame_alloc();videoPacket=ffmpeg.av_packet_alloc();audioPacket=ffmpeg.av_packet_alloc();
            if(frame==null || videoPacket==null || audioPacket==null)throw new OutOfMemoryException();
            frame->format=(int)encoder->pix_fmt;frame->width=frames.Width;frame->height=frames.Height;frame->sample_aspect_ratio=encoder->sample_aspect_ratio;
            Check(ffmpeg.av_frame_get_buffer(frame,32),"video frame buffer");
            scaler=ffmpeg.sws_getContext(frames.Width,frames.Height,AVPixelFormat.AV_PIX_FMT_BGR24,frames.Width,frames.Height,encoder->pix_fmt,(int)SwsFlags.SWS_BICUBIC,null,null,null);
            if(scaler==null)throw new NativeMediaException("encode",-1,"Cannot create PNG color converter");
        }
        private bool TryEncoder(string name)
        {
            token.ThrowIfCancellationRequested();AVCodec* codec=ffmpeg.avcodec_find_encoder_by_name(name);if(codec==null)return false;
            AVCodecContext* candidate=ffmpeg.avcodec_alloc_context3(codec);if(candidate==null)throw new OutOfMemoryException();
            try {
                candidate->width=frames.Width;candidate->height=frames.Height;candidate->pix_fmt=AVPixelFormat.AV_PIX_FMT_YUV420P;
                candidate->time_base=new AVRational{num=1,den=30};candidate->framerate=new AVRational{num=30,den=1};
                candidate->sample_aspect_ratio=new AVRational{num=frames.SarNumerator,den=frames.SarDenominator};
                candidate->gop_size=60;candidate->max_b_frames=0;
                candidate->bit_rate=Math.Max(1000000,checked((long)frames.Width*frames.Height*6));
                candidate->flags|=ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
                int r=ffmpeg.avcodec_open2(candidate,codec,null);
                token.ThrowIfCancellationRequested();
                if(r<0)return false;
                encoder=candidate;candidate=null;EncoderName=name;return true;
            } finally {ffmpeg.avcodec_free_context(&candidate);}
        }
        private void DrainVideo()
        {
            while(true) {
                int r=ffmpeg.avcodec_receive_packet(encoder,videoPacket);
                if(r==ffmpeg.AVERROR(ffmpeg.EAGAIN)||r==ffmpeg.AVERROR_EOF)return;Check(r,"encoded video packet");
                try {ffmpeg.av_packet_rescale_ts(videoPacket,encoder->time_base,videoStream->time_base);videoPacket->stream_index=videoStream->index;videoPacket->pos=-1;
                    Check(ffmpeg.av_interleaved_write_frame(output,videoPacket),"write video packet");}
                finally {ffmpeg.av_packet_unref(videoPacket);}
            }
        }
        private void DrainAudio(double throughSeconds)
        {
            AVStream* inputStream=audioInput.Format->streams[audioIndex];
            while(!audioEof) {
                token.ThrowIfCancellationRequested();
                if(!audioPending) {
                    int r=ffmpeg.av_read_frame(audioInput.Format,audioPacket);
                    if(r==ffmpeg.AVERROR_EOF){audioEof=true;return;}Check(r,"read remux audio");
                    if(audioPacket->stream_index!=audioIndex){ffmpeg.av_packet_unref(audioPacket);continue;}
                    if(audioPacket->pts!=ffmpeg.AV_NOPTS_VALUE)audioPacket->pts+=audioOffset;
                    if(audioPacket->dts!=ffmpeg.AV_NOPTS_VALUE)audioPacket->dts+=audioOffset;
                    audioPending=true;
                }
                long ts=audioPacket->dts==ffmpeg.AV_NOPTS_VALUE?audioPacket->pts:audioPacket->dts;
                if(ts!=ffmpeg.AV_NOPTS_VALUE && ts*ffmpeg.av_q2d(inputStream->time_base)>throughSeconds)return;
                try {ffmpeg.av_packet_rescale_ts(audioPacket,inputStream->time_base,audioStream->time_base);audioPacket->stream_index=audioStream->index;audioPacket->pos=-1;
                    Check(ffmpeg.av_interleaved_write_frame(output,audioPacket),"write remux audio");}
                finally {ffmpeg.av_packet_unref(audioPacket);audioPending=false;}
            }
        }
        private void Validate(string path)
        {
            progress?.Report(new MediaProgress{Stage="validate",Completed=0});
            int count=0;
            using(var check=new FfmpegInput(path,token,AVMediaType.AVMEDIA_TYPE_VIDEO))check.ReadFrames(f=>{
                if(f->width!=frames.Width || f->height!=frames.Height)throw new NativeMediaException("validate",-1,"Unexpected output dimensions");count++;
            });
            if(count!=frames.Count)throw new NativeMediaException("validate",-1,"Incomplete MP4 frame count: "+count+" / "+frames.Count);
            long samples=0;using(var check=new FfmpegInput(path,token,AVMediaType.AVMEDIA_TYPE_AUDIO))check.ReadFrames(f=>samples+=f->nb_samples);
            if(samples<=0)throw new NativeMediaException("validate",-1,"MP4 audio is empty");
        }
        private void Check(int r,string stage){token.ThrowIfCancellationRequested();FfmpegRuntime.Check(r,stage);}
        public void Dispose()
        {
            AVFrame* f=frame;frame=null;ffmpeg.av_frame_free(&f);
            AVPacket* p=videoPacket;videoPacket=null;ffmpeg.av_packet_free(&p);p=audioPacket;audioPacket=null;ffmpeg.av_packet_free(&p);
            if(scaler!=null){ffmpeg.sws_freeContext(scaler);scaler=null;}
            AVCodecContext* c=encoder;encoder=null;ffmpeg.avcodec_free_context(&c);
            audioInput?.Dispose();audioInput=null;
            if(output!=null){if(output->pb!=null)ffmpeg.avio_closep(&output->pb);ffmpeg.avformat_free_context(output);output=null;}
            GC.KeepAlive(interrupt);
        }
    }
}
