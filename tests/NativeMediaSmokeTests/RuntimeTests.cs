using System;
using FFmpeg.AutoGen;
using utility_app;
internal static unsafe class RuntimeTests
{
    public static void Run()
    {
        foreach(string name in new[]{"png","aac","libmp3lame","libvorbis","pcm_s16le","libopenh264"})
            if(ffmpeg.avcodec_find_encoder_by_name(name)==null) throw new Exception("Missing encoder " + name);
        AVCodecContext* encoder=null; AVCodecContext* decoder=null;
        AVFrame* input=null; AVFrame* output=null; AVPacket* packet=null;
        try
        {
            AVCodec* ec=ffmpeg.avcodec_find_encoder_by_name("libopenh264");
            encoder=ffmpeg.avcodec_alloc_context3(ec); decoder=ffmpeg.avcodec_alloc_context3(ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264));
            encoder->width=96; encoder->height=64; encoder->pix_fmt=AVPixelFormat.AV_PIX_FMT_YUV420P;
            encoder->time_base=new AVRational{num=1,den=30}; encoder->bit_rate=400000;
            FfmpegRuntime.Check(ffmpeg.avcodec_open2(encoder,ec,null), "open software encoder");
            FfmpegRuntime.Check(ffmpeg.avcodec_open2(decoder, null, null), "open decoder");
            input=ffmpeg.av_frame_alloc(); output=ffmpeg.av_frame_alloc(); packet=ffmpeg.av_packet_alloc();
            input->width=96;input->height=64;input->format=(int)encoder->pix_fmt;
            FfmpegRuntime.Check(ffmpeg.av_frame_get_buffer(input,32),"frame allocation");
            for(uint plane=0;plane<3;plane++)
                for(int y=0;y<(plane==0?64:32);y++)
                    for(int x=0;x<(plane==0?96:48);x++) input->data[plane][y*input->linesize[plane]+x]=(byte)(plane==0?76:128);
            FfmpegRuntime.Check(ffmpeg.avcodec_send_frame(encoder,input),"encode");
            FfmpegRuntime.Check(ffmpeg.avcodec_send_frame(encoder,null),"flush encoder");
            int count=0;
            while(true)
            {
                int r=ffmpeg.avcodec_receive_packet(encoder,packet); if(r==ffmpeg.AVERROR_EOF || r==ffmpeg.AVERROR(ffmpeg.EAGAIN)) break;
                FfmpegRuntime.Check(r,"receive encoded packet"); FfmpegRuntime.Check(ffmpeg.avcodec_send_packet(decoder,packet),"decode packet");
                ffmpeg.av_packet_unref(packet);
                while((r=ffmpeg.avcodec_receive_frame(decoder,output))>=0) { count++; if(output->width!=96||output->height!=64)throw new Exception("roundtrip dimensions");ffmpeg.av_frame_unref(output); }
                if(r!=ffmpeg.AVERROR_EOF&&r!=ffmpeg.AVERROR(ffmpeg.EAGAIN))FfmpegRuntime.Check(r,"receive decoded frame");
            }
            FfmpegRuntime.Check(ffmpeg.avcodec_send_packet(decoder,null),"flush decoder");
            while(ffmpeg.avcodec_receive_frame(decoder,output)>=0){count++;ffmpeg.av_frame_unref(output);}
            if(count!=1)throw new Exception("H264 roundtrip expected one frame, got "+count);
        }
        finally { ffmpeg.av_packet_free(&packet);ffmpeg.av_frame_free(&input);ffmpeg.av_frame_free(&output);ffmpeg.avcodec_free_context(&encoder);ffmpeg.avcodec_free_context(&decoder); }
    }
}
