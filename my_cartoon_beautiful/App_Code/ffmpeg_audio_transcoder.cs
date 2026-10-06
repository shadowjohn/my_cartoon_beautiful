using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FFmpeg.AutoGen;
namespace utility_app
{
    internal sealed unsafe class FfmpegAudioTranscoder : IDisposable
    {
        private readonly FfmpegInput input;
        private readonly CancellationToken token;
        private readonly IProgress<MediaProgress> progress;
        private readonly AudioAsset result;
        private AVCodecContext* encoder;
        private AVFormatContext* output;
        private AVStream* stream;
        private AVFilterGraph* graph;
        private AVFilterContext* source;
        private AVFilterContext* sink;
        private AVFrame* filtered;
        private AVPacket* packet;
        private long origin=ffmpeg.AV_NOPTS_VALUE;
        private long samples;
        private int inputRate,inputFormat,inputChannels;
        internal FfmpegAudioTranscoder(FfmpegInput input,string outputBase,AudioMode mode,IProgress<MediaProgress> progress,CancellationToken token)
        {
            this.input=input;this.progress=progress;this.token=token;
            string[] extensions={".aac",".mp3",".ogg",".wav"};
            if((int)mode<0||(int)mode>=extensions.Length)throw new ArgumentOutOfRangeException("mode");
            result=new AudioAsset{Path=outputBase+extensions[(int)mode],Mode=mode};
            try{Open(mode);}catch{Dispose();throw;}
        }
        private void Open(AudioMode mode)
        {
            string[] names={"aac","libmp3lame","libvorbis","pcm_s16le"};
            AVCodec* codec=ffmpeg.avcodec_find_encoder_by_name(names[(int)mode]);
            if(codec==null)throw new NativeMediaException("audio",-1,"Missing encoder "+names[(int)mode]);
            encoder=ffmpeg.avcodec_alloc_context3(codec);if(encoder==null)throw new OutOfMemoryException();
            encoder->sample_rate=input.Decoder->sample_rate;
            if(encoder->sample_rate<=0)throw new NativeMediaException("audio",-1,"Invalid sample rate");
            void* supported=null;int count=0;
            FfmpegRuntime.Check(ffmpeg.avcodec_get_supported_config(null,codec,AVCodecConfig.AV_CODEC_CONFIG_SAMPLE_RATE,0,&supported,&count),"audio supported rates");
            if(supported!=null && count>0)
            {
                int* rates=(int*)supported;int closest=rates[0];
                for(int i=0;i<count;i++)if(Math.Abs(rates[i]-encoder->sample_rate)<Math.Abs(closest-encoder->sample_rate))closest=rates[i];
                encoder->sample_rate=closest;
            }
            encoder->time_base=new AVRational{num=1,den=encoder->sample_rate};
            supported=null;count=0;
            FfmpegRuntime.Check(ffmpeg.avcodec_get_supported_config(null,codec,AVCodecConfig.AV_CODEC_CONFIG_SAMPLE_FORMAT,0,&supported,&count),"audio supported formats");
            if(supported==null||count==0)throw new NativeMediaException("audio",-1,"No sample formats");
            encoder->sample_fmt=((AVSampleFormat*)supported)[0];
            if(input.Decoder->ch_layout.nb_channels<=0)throw new NativeMediaException("audio",-1,"Missing channel layout");
            if(input.Decoder->ch_layout.order==AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC)
                ffmpeg.av_channel_layout_default(&encoder->ch_layout,input.Decoder->ch_layout.nb_channels);
            else FfmpegRuntime.Check(ffmpeg.av_channel_layout_copy(&encoder->ch_layout,&input.Decoder->ch_layout),"copy audio layout");
            supported=null;count=0;
            FfmpegRuntime.Check(ffmpeg.avcodec_get_supported_config(null,codec,AVCodecConfig.AV_CODEC_CONFIG_CHANNEL_LAYOUT,0,&supported,&count),"audio supported layouts");
            if(supported!=null&&count>0)
            {
                AVChannelLayout* layouts=(AVChannelLayout*)supported;int selected=-1;
                for(int i=0;i<count;i++)if(ffmpeg.av_channel_layout_compare(&encoder->ch_layout,&layouts[i])==0)selected=i;
                if(selected<0){ffmpeg.av_channel_layout_uninit(&encoder->ch_layout);FfmpegRuntime.Check(ffmpeg.av_channel_layout_copy(&encoder->ch_layout,&layouts[0]),"select audio layout");}
            }
            AVFormatContext* format=null;
            FfmpegRuntime.Check(ffmpeg.avformat_alloc_output_context2(&format,null,null,result.Path),"allocate audio muxer");output=format;
            if(output==null)throw new OutOfMemoryException();
            output->interrupt_callback=input.Format->interrupt_callback;
            if((output->oformat->flags&ffmpeg.AVFMT_GLOBALHEADER)!=0)encoder->flags|=ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
            if(mode==AudioMode.Aac)encoder->bit_rate=192000;
            if(mode==AudioMode.Mp3||mode==AudioMode.Vorbis){encoder->flags|=ffmpeg.AV_CODEC_FLAG_QSCALE;encoder->global_quality=4*ffmpeg.FF_QP2LAMBDA;}
            FfmpegRuntime.Check(ffmpeg.avcodec_open2(encoder,codec,null),"open audio encoder");
            stream=ffmpeg.avformat_new_stream(output,null);if(stream==null)throw new OutOfMemoryException();
            stream->time_base=encoder->time_base;
            FfmpegRuntime.Check(ffmpeg.avcodec_parameters_from_context(stream->codecpar,encoder),"audio stream parameters");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(result.Path)));
            if((output->oformat->flags&ffmpeg.AVFMT_NOFILE)==0)Check(ffmpeg.avio_open2(&output->pb,result.Path,ffmpeg.AVIO_FLAG_WRITE,&output->interrupt_callback,null),"open audio output");
            Check(ffmpeg.avformat_write_header(output,null),"audio header");
            packet=ffmpeg.av_packet_alloc();filtered=ffmpeg.av_frame_alloc();if(packet==null||filtered==null)throw new OutOfMemoryException();
        }
        internal AudioAsset Run()
        {
            input.ReadFrames(Accept);
            if(graph==null)throw new NativeMediaException("audio",-1,"No decoded audio samples");
            Check(ffmpeg.av_buffersrc_add_frame_flags(source,null,0),"flush audio resampler");DrainFilter();
            Check(ffmpeg.avcodec_send_frame(encoder,null),"flush audio encoder");DrainPackets();
            Check(ffmpeg.av_write_trailer(output),"audio trailer");
            if(output->pb!=null)Check(ffmpeg.avio_closep(&output->pb),"close audio output");
            if(samples==0)throw new NativeMediaException("audio",-1,"No output audio samples");
            result.Duration=TimeSpan.FromSeconds((double)samples/encoder->sample_rate);
            return result;
        }
        private void Check(int r,string stage){token.ThrowIfCancellationRequested();FfmpegRuntime.Check(r,stage);}
        private void Accept(AVFrame* frame)
        {
            if(graph==null)InitializeFilter(frame);
            if(frame->sample_rate!=inputRate||frame->format!=inputFormat||frame->ch_layout.nb_channels!=inputChannels)
                throw new NativeMediaException("audio",-1,"Audio format changed during decoding");
            if(origin==ffmpeg.AV_NOPTS_VALUE)
            {
                origin=frame->pts==ffmpeg.AV_NOPTS_VALUE?0:frame->pts;
                result.SourceStartTime=TimeSpan.FromSeconds(origin*ffmpeg.av_q2d(input.TimeBase));
            }
            frame->pts=frame->pts==ffmpeg.AV_NOPTS_VALUE?ffmpeg.AV_NOPTS_VALUE:ffmpeg.av_rescale_q(frame->pts-origin,input.TimeBase,new AVRational{num=1,den=frame->sample_rate});
            Check(ffmpeg.av_buffersrc_add_frame_flags(source,frame,(int)AvBuffersrcFlag.AV_BUFFERSRC_FLAG_KEEP_REF),"resample audio");DrainFilter();
        }
        private static string LayoutName(AVChannelLayout* layout)
        {
            byte* buffer=stackalloc byte[256];FfmpegRuntime.Check(ffmpeg.av_channel_layout_describe(layout,buffer,256),"audio layout name");
            return Marshal.PtrToStringAnsi((IntPtr)buffer);
        }
        private void InitializeFilter(AVFrame* frame)
        {
            inputRate=frame->sample_rate;inputFormat=frame->format;inputChannels=frame->ch_layout.nb_channels;
            if(frame->ch_layout.order==AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC)ffmpeg.av_channel_layout_default(&frame->ch_layout,inputChannels);
            graph=ffmpeg.avfilter_graph_alloc();if(graph==null)throw new OutOfMemoryException();
            AVFilterContext* a=null;AVFilterContext* b=null;AVFilterContext* c=null;
            string args=string.Format(CultureInfo.InvariantCulture,"time_base=1/{0}:sample_rate={0}:sample_fmt={1}:channel_layout={2}",frame->sample_rate,ffmpeg.av_get_sample_fmt_name((AVSampleFormat)frame->format),LayoutName(&frame->ch_layout));
            Check(ffmpeg.avfilter_graph_create_filter(&a,ffmpeg.avfilter_get_by_name("abuffer"),"input",args,null,graph),"audio buffer");source=a;
            args=string.Format(CultureInfo.InvariantCulture,"sample_fmts={0}:sample_rates={1}:channel_layouts={2}",ffmpeg.av_get_sample_fmt_name(encoder->sample_fmt),encoder->sample_rate,LayoutName(&encoder->ch_layout));
            Check(ffmpeg.avfilter_graph_create_filter(&b,ffmpeg.avfilter_get_by_name("aformat"),"format",args,null,graph),"audio conversion");
            Check(ffmpeg.avfilter_graph_create_filter(&c,ffmpeg.avfilter_get_by_name("abuffersink"),"output",null,null,graph),"audio sink");sink=c;
            Check(ffmpeg.avfilter_link(a,0,b,0),"link audio");Check(ffmpeg.avfilter_link(b,0,c,0),"link audio sink");
            Check(ffmpeg.avfilter_graph_config(graph,null),"configure audio resampler");
            if(encoder->frame_size>0)ffmpeg.av_buffersink_set_frame_size(sink,(uint)encoder->frame_size);
        }
        private void DrainFilter()
        {
            while(true)
            {
                token.ThrowIfCancellationRequested();int r=ffmpeg.av_buffersink_get_frame(sink,filtered);
                if(r==ffmpeg.AVERROR(ffmpeg.EAGAIN)||r==ffmpeg.AVERROR_EOF)return;Check(r,"audio resampler output");
                try
                {
                    filtered->pts=ffmpeg.av_rescale_q(filtered->pts,ffmpeg.av_buffersink_get_time_base(sink),encoder->time_base);
                    Check(ffmpeg.avcodec_send_frame(encoder,filtered),"encode audio");samples+=filtered->nb_samples;DrainPackets();
                    if(progress!=null)progress.Report(new MediaProgress{Stage="audio",Completed=samples});
                }
                finally{ffmpeg.av_frame_unref(filtered);}
            }
        }
        private void DrainPackets()
        {
            while(true)
            {
                int r=ffmpeg.avcodec_receive_packet(encoder,packet);if(r==ffmpeg.AVERROR(ffmpeg.EAGAIN)||r==ffmpeg.AVERROR_EOF)return;Check(r,"audio encoded packet");
                try{ffmpeg.av_packet_rescale_ts(packet,encoder->time_base,stream->time_base);packet->stream_index=stream->index;packet->pos=-1;Check(ffmpeg.av_interleaved_write_frame(output,packet),"write audio packet");}
                finally{ffmpeg.av_packet_unref(packet);}
            }
        }
        public void Dispose()
        {
            AVPacket* p=packet;packet=null;ffmpeg.av_packet_free(&p);AVFrame* f=filtered;filtered=null;ffmpeg.av_frame_free(&f);
            AVFilterGraph* g=graph;graph=null;ffmpeg.avfilter_graph_free(&g);AVCodecContext* c=encoder;encoder=null;ffmpeg.avcodec_free_context(&c);
            if(output!=null){if(output->pb!=null)ffmpeg.avio_closep(&output->pb);ffmpeg.avformat_free_context(output);output=null;}
        }
    }
}
