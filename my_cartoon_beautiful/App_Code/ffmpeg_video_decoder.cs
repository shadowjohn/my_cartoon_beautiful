using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using FFmpeg.AutoGen;
namespace utility_app
{
    internal sealed unsafe class FfmpegVideoDecoder : IDisposable
    {
        private AVFilterGraph* graph;
        private AVFilterContext* source;
        private AVFilterContext* sink;
        private AVFrame* filtered;
        private AVFrame* bgr;
        private SwsContext* scaler;
        private readonly FfmpegInput input;
        private readonly string directory;
        private readonly IProgress<MediaProgress> progress;
        private readonly CancellationToken token;
        private readonly FrameSequence result;
        private int inputWidth,inputHeight,inputFormat;
        internal FfmpegVideoDecoder(FfmpegInput input,string directory,IProgress<MediaProgress> progress,CancellationToken token)
        {this.input=input;this.directory=directory;this.progress=progress;this.token=token;result=new FrameSequence{DirectoryPath=directory};}
        internal FrameSequence Run()
        {
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new NativeMediaException("decode",-1,"Output frame directory must be empty");
            Directory.CreateDirectory(directory);
            input.ReadFrames(Accept);
            if(graph==null)throw new NativeMediaException("decode",-1,"No decoded video frames");
            FfmpegRuntime.Check(ffmpeg.av_buffersrc_close(source,input.EndTimestamp,0),"close fps input");
            Drain();
            if(result.Count==0)throw new NativeMediaException("decode",-1,"No output video frames");
            return result;
        }
        private void Accept(AVFrame* frame)
        {
            if(graph==null)Initialize(frame);
            if(frame->width!=inputWidth || frame->height!=inputHeight || frame->format!=inputFormat)
                throw new NativeMediaException("decode",-1,"Video format changed during decoding");
            FfmpegRuntime.Check(ffmpeg.av_buffersrc_add_frame_flags(source,frame,(int)AvBuffersrcFlag.AV_BUFFERSRC_FLAG_KEEP_REF),"feed fps filter");Drain();
        }
        private void Initialize(AVFrame* frame)
        {
            inputWidth=frame->width;inputHeight=frame->height;inputFormat=frame->format;
            graph=ffmpeg.avfilter_graph_alloc();filtered=ffmpeg.av_frame_alloc();
            if(graph==null||filtered==null)throw new OutOfMemoryException();
            var sar=ffmpeg.av_guess_sample_aspect_ratio(input.Format,input.Format->streams[input.StreamIndex],frame);
            if(sar.num<=0||sar.den<=0)sar=new AVRational{num=1,den=1};
            var rate=ffmpeg.av_guess_frame_rate(input.Format,input.Format->streams[input.StreamIndex],frame);
            string args=string.Format(CultureInfo.InvariantCulture,"video_size={0}x{1}:pix_fmt={2}:time_base={3}/{4}:pixel_aspect={5}/{6}",frame->width,frame->height,frame->format,input.TimeBase.num,input.TimeBase.den,sar.num,sar.den);
            if(rate.num>0&&rate.den>0)args+=string.Format(CultureInfo.InvariantCulture,":frame_rate={0}/{1}",rate.num,rate.den);
            AVFilterContext* first=null;AVFilterContext* fps=null;AVFilterContext* last=null;
            FfmpegRuntime.Check(ffmpeg.avfilter_graph_create_filter(&first,ffmpeg.avfilter_get_by_name("buffer"),"input",args,null,graph),"create buffer");source=first;
            // Match ffmpeg's default autorotation for display-matrix metadata.
            var par=input.Format->streams[input.StreamIndex]->codecpar;
            AVPacketSideData* side=ffmpeg.av_packet_side_data_get(par->coded_side_data,par->nb_coded_side_data,AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX);
            AVFrameSideData* frameSide=ffmpeg.av_frame_get_side_data(frame,AVFrameSideDataType.AV_FRAME_DATA_DISPLAYMATRIX);
            byte* matrix=frameSide!=null && frameSide->size>=9*sizeof(int)?frameSide->data:(side!=null && side->size>=9*sizeof(int)?side->data:null);
            if(matrix!=null)
            {
                double angle=-ffmpeg.av_display_rotation_get(*(int_array9*)matrix);
                int rotation=((int)Math.Round(angle)%360+360)%360;
                if(rotation==90||rotation==270)
                {
                    AVFilterContext* transpose=null;
                    FfmpegRuntime.Check(ffmpeg.avfilter_graph_create_filter(&transpose,ffmpeg.avfilter_get_by_name("transpose"),"rotate",rotation==90?"clock":"cclock",null,graph),"create rotation");
                    FfmpegRuntime.Check(ffmpeg.avfilter_link(first,0,transpose,0),"link rotation");first=transpose;
                }
                else if(rotation==180)
                {
                    foreach(string name in new[]{"hflip","vflip"})
                    {AVFilterContext* flip=null;FfmpegRuntime.Check(ffmpeg.avfilter_graph_create_filter(&flip,ffmpeg.avfilter_get_by_name(name),name,null,null,graph),"create rotation");FfmpegRuntime.Check(ffmpeg.avfilter_link(first,0,flip,0),"link rotation");first=flip;}
                }
                else if(rotation!=0)throw new NativeMediaException("decode",-1,"Unsupported non-right-angle rotation");
            }
            FfmpegRuntime.Check(ffmpeg.avfilter_graph_create_filter(&fps,ffmpeg.avfilter_get_by_name("fps"),"fps","fps=30",null,graph),"create fps filter");
            FfmpegRuntime.Check(ffmpeg.avfilter_graph_create_filter(&last,ffmpeg.avfilter_get_by_name("buffersink"),"output",null,null,graph),"create frame sink");sink=last;
            FfmpegRuntime.Check(ffmpeg.avfilter_link(first,0,fps,0),"link fps");FfmpegRuntime.Check(ffmpeg.avfilter_link(fps,0,last,0),"link output");
            FfmpegRuntime.Check(ffmpeg.avfilter_graph_config(graph,null),"configure filters");
        }
        private void Drain()
        {
            while(true)
            {
                token.ThrowIfCancellationRequested();int r=ffmpeg.av_buffersink_get_frame(sink,filtered);
                if(r==ffmpeg.AVERROR(ffmpeg.EAGAIN)||r==ffmpeg.AVERROR_EOF)return;FfmpegRuntime.Check(r,"fps output");
                try{Save(filtered);}finally{ffmpeg.av_frame_unref(filtered);}
            }
        }
        private void Save(AVFrame* frame)
        {
            if(bgr==null)
            {
                bgr=ffmpeg.av_frame_alloc();if(bgr==null)throw new OutOfMemoryException();
                bgr->width=frame->width;bgr->height=frame->height;bgr->format=(int)AVPixelFormat.AV_PIX_FMT_BGR24;
                FfmpegRuntime.Check(ffmpeg.av_frame_get_buffer(bgr,32),"allocate RGB frame");
                scaler=ffmpeg.sws_getContext(frame->width,frame->height,(AVPixelFormat)frame->format,frame->width,frame->height,AVPixelFormat.AV_PIX_FMT_BGR24,(int)SwsFlags.SWS_BICUBIC,null,null,null);
                if(scaler==null)throw new NativeMediaException("decode",-1,"Cannot convert frame to RGB");
                result.Width=frame->width;result.Height=frame->height;
                var tb=ffmpeg.av_buffersink_get_time_base(sink);
                result.SourceStartTime=TimeSpan.FromSeconds(frame->pts*ffmpeg.av_q2d(tb));
                result.SarNumerator=frame->sample_aspect_ratio.num>0?frame->sample_aspect_ratio.num:1;
                result.SarDenominator=frame->sample_aspect_ratio.den>0?frame->sample_aspect_ratio.den:1;
            }
            int colorspace=(int)frame->colorspace;
            if(colorspace==0||colorspace==2)colorspace=ffmpeg.SWS_CS_DEFAULT;
            int_array4 coefficients=*(int_array4*)ffmpeg.sws_getCoefficients(colorspace);
            FfmpegRuntime.Check(ffmpeg.sws_setColorspaceDetails(scaler,coefficients,frame->color_range==AVColorRange.AVCOL_RANGE_JPEG?1:0,coefficients,1,0,1<<16,1<<16),"set colorspace");
            FfmpegRuntime.Check(ffmpeg.av_frame_make_writable(bgr),"make RGB writable");
            int rows=ffmpeg.sws_scale(scaler,frame->data,frame->linesize,0,frame->height,bgr->data,bgr->linesize);
            if(rows!=frame->height)throw new NativeMediaException("decode",rows,"Incomplete RGB frame");
            string filename=Path.Combine(directory,(result.Count+1).ToString("D8",CultureInfo.InvariantCulture)+".png");
            using(var bitmap=new Bitmap(bgr->width,bgr->height,bgr->linesize[0],PixelFormat.Format24bppRgb,(IntPtr)bgr->data[0]))bitmap.Save(filename,ImageFormat.Png);
            result.Count++;
            if(progress!=null)progress.Report(new MediaProgress{Stage="decode",Completed=result.Count});
        }
        public void Dispose()
        {AVFrame* f=filtered;filtered=null;ffmpeg.av_frame_free(&f);f=bgr;bgr=null;ffmpeg.av_frame_free(&f);ffmpeg.sws_freeContext(scaler);scaler=null;AVFilterGraph* g=graph;graph=null;ffmpeg.avfilter_graph_free(&g);}
    }
}
