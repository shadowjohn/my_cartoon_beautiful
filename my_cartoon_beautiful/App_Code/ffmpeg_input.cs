using System;
using System.Threading;
using FFmpeg.AutoGen;
namespace utility_app
{
    internal unsafe delegate void NativeFrameHandler(AVFrame* frame);
    internal sealed unsafe class FfmpegInput : IDisposable
    {
        internal AVFormatContext* Format;
        internal AVCodecContext* Decoder;
        internal int StreamIndex = -1;
        internal AVRational TimeBase;
        internal long EndTimestamp = ffmpeg.AV_NOPTS_VALUE;
        private readonly CancellationToken token;
        private readonly AVIOInterruptCB_callback interrupt;
        internal FfmpegInput(string path, CancellationToken cancellation, AVMediaType type = AVMediaType.AVMEDIA_TYPE_UNKNOWN)
        {
            token=cancellation; token.ThrowIfCancellationRequested();
            interrupt = delegate(void* opaque) { return token.IsCancellationRequested ? 1 : 0; };
            Format=ffmpeg.avformat_alloc_context();
            if(Format==null)throw new OutOfMemoryException();
            Format->interrupt_callback=new AVIOInterruptCB { callback=interrupt };
            try
            {
                AVFormatContext* f=Format;
                int r=ffmpeg.avformat_open_input(&f,path,null,null); Format=f; Check(r,"open input");
                Check(ffmpeg.avformat_find_stream_info(Format,null),"read media information");
                if(type!=AVMediaType.AVMEDIA_TYPE_UNKNOWN)
                {
                    AVCodec* codec=null;
                    StreamIndex=ffmpeg.av_find_best_stream(Format,type,-1,-1,&codec,0);
                    Check(StreamIndex,type==AVMediaType.AVMEDIA_TYPE_AUDIO?"no audio stream":"no video stream");
                    Decoder=ffmpeg.avcodec_alloc_context3(codec);if(Decoder==null)throw new OutOfMemoryException();
                    TimeBase=Format->streams[StreamIndex]->time_base;
                    Check(ffmpeg.avcodec_parameters_to_context(Decoder,Format->streams[StreamIndex]->codecpar),"decoder parameters");
                    Decoder->pkt_timebase=TimeBase;
                    Check(ffmpeg.avcodec_open2(Decoder,codec,null),"open decoder");
                }
            }
            catch { Dispose(); throw; }
        }
        internal void Check(int result,string stage) { token.ThrowIfCancellationRequested();FfmpegRuntime.Check(result,stage); }
        internal void ReadFrames(NativeFrameHandler visit)
        {
            AVPacket* packet=ffmpeg.av_packet_alloc();AVFrame* frame=ffmpeg.av_frame_alloc();
            if(packet==null||frame==null){ffmpeg.av_packet_free(&packet);ffmpeg.av_frame_free(&frame);throw new OutOfMemoryException();}
            try
            {
                while(true)
                {
                    token.ThrowIfCancellationRequested();
                    int r=ffmpeg.av_read_frame(Format,packet);
                    if(r==ffmpeg.AVERROR_EOF)break;
                    Check(r,"read packet");
                    try
                    {
                        if(packet->stream_index!=StreamIndex)continue;
                        Check(ffmpeg.avcodec_send_packet(Decoder,packet),"send packet");
                        Drain(frame,visit);
                    }
                    finally{ffmpeg.av_packet_unref(packet);}
                }
                Check(ffmpeg.avcodec_send_packet(Decoder,null),"flush decoder");Drain(frame,visit);
            }
            finally{ffmpeg.av_packet_free(&packet);ffmpeg.av_frame_free(&frame);}
        }
        private void Drain(AVFrame* frame,NativeFrameHandler visit)
        {
            while(true)
            {
                token.ThrowIfCancellationRequested();int r=ffmpeg.avcodec_receive_frame(Decoder,frame);
                if(r==ffmpeg.AVERROR(ffmpeg.EAGAIN)||r==ffmpeg.AVERROR_EOF)return;
                Check(r,"decode frame");
                try
                {
                    if(frame->best_effort_timestamp!=ffmpeg.AV_NOPTS_VALUE)frame->pts=frame->best_effort_timestamp;
                    if(frame->pts!=ffmpeg.AV_NOPTS_VALUE)
                    {
                        long duration=frame->duration;
                        if(duration<=0)
                        {
                            if(Decoder->codec_type==AVMediaType.AVMEDIA_TYPE_AUDIO && frame->sample_rate>0)duration=ffmpeg.av_rescale_q(frame->nb_samples,new AVRational{num=1,den=frame->sample_rate},TimeBase);
                            else {var rate=ffmpeg.av_guess_frame_rate(Format,Format->streams[StreamIndex],frame);if(rate.num>0)duration=ffmpeg.av_rescale_q(1,ffmpeg.av_inv_q(rate),TimeBase);}
                        }
                        EndTimestamp=frame->pts+duration;
                    }
                    visit(frame);
                }
                finally {ffmpeg.av_frame_unref(frame);}
            }
        }
        public void Dispose()
        {
            AVCodecContext* decoder=Decoder;Decoder=null;ffmpeg.avcodec_free_context(&decoder);
            AVFormatContext* format=Format;Format=null;ffmpeg.avformat_close_input(&format);GC.KeepAlive(interrupt);
        }
    }
}
