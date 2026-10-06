using my_cartoon_beautiful;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace utility_app
{
    public class myApp
    {
        private const double ProgressStep2Start = 0.0;
        private const double ProgressStep2End = 15.0;
        private const double ProgressStep3End = 18.0;
        private const double ProgressStep4PrepareEnd = 22.0;
        private const double ProgressStep4End = 87.0;
        private const double ProgressStep5Start = 88.0;
        private const double ProgressStep5End = 97.0;
        private const double ProgressStep5Done = 98.0;
        private const double ProgressAllDone = 100.0;

        Form1 theform;
        //private bool isNeedStop = false;
        public myApp(Form1 f)
        {
            theform = f;
        }
        public bool step1_checkWorkPath(string workPath) //檢查工作目錄是否存在，不存就建立，已存在就移除
        {
            try
            {
                if (Directory.Exists(workPath)) throw new IOException("Work directory already exists: " + workPath);
                Directory.CreateDirectory(workPath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("轉檔異常...\r\n" + ex.Message);
                return false;
            }
        }
        private IMediaBackend backend;
        private MediaInfo mediaInfo;
        private FrameSequence frames;
        private AudioAsset audio;
        private static string BinaryPath(params string[] parts)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "binary");
            foreach (string part in parts) path = Path.Combine(path, part);
            return path;
        }
        private IMediaBackend Backend { get { return backend ?? (backend = new FfmpegMediaBackend(FfmpegRuntime.Load(BinaryPath("native", "ffmpeg")))); } }
        private async Task<bool> RunNativeStage(Action action, CancellationToken token)
        {
            try { await Task.Run(action, token); token.ThrowIfCancellationRequested(); return true; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) {
                Console.Error.WriteLine(ex);
                if (!theform.IsDisposed && !theform.Disposing && !theform.ClosingAfterJob) MessageBox.Show(theform, ex.Message, "轉檔失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        private sealed class StageProgress : IProgress<MediaProgress>
        {
            private readonly Form1 form;
            private readonly Action<MediaProgress> update;
            private readonly Stopwatch clock = Stopwatch.StartNew();
            private long last = -100;
            internal StageProgress(Form1 form, Action<MediaProgress> update) { this.form = form; this.update = update; }
            public void Report(MediaProgress p) {
                if (clock.ElapsedMilliseconds - last < 100) return;
                last = clock.ElapsedMilliseconds;
                if (form.IsDisposed || form.Disposing || !form.IsHandleCreated) return;
                try { form.Invoke((MethodInvoker)(() => { if (!form.IsDisposed && !form.Disposing) update(p); })); }
                catch (InvalidOperationException) { if (!form.IsDisposed && !form.Disposing) throw; }
            }
        }
        public async Task<bool> step2_sourceFile_to_png(string workPath, string sourceFile, CancellationToken cancellationToken)
        {
            frames = null; audio = null; mediaInfo = null;
            theform.setProgressTitle("讀取影片並拆成 PNG...");
            var progress = new StageProgress(theform, p => {
                double total = mediaInfo?.Duration?.TotalSeconds * 30 ?? 0;
                theform.setProgressTitle("影像轉成 PNG: " + p.Completed);
                theform.setProgress(total > 0 ? Math.Min(ProgressStep2End, p.Completed / total * ProgressStep2End) : ProgressStep2Start);
            });
            bool ok = await RunNativeStage(() => {
                mediaInfo = Backend.Probe(sourceFile, cancellationToken);
                frames = Backend.ExtractFrames(sourceFile, Path.Combine(workPath, "source"), progress, cancellationToken);
            }, cancellationToken);
            if (ok) { theform.setProgress(ProgressStep2End); theform.setProgressTitle("拆幀完成: " + frames.Count); }
            return ok;
        }
        public async Task<bool> step3_sourceFile_to_wav(string workPath, string sourceFile, string targetFile, CancellationToken cancellationToken)
        {
            string kind = theform.comboBox_soundKind.Text.Trim().ToUpperInvariant();
            AudioMode mode = kind == "AAC" ? AudioMode.Aac : kind == "LIBMP3LAME" ? AudioMode.Mp3 : kind == "OGG" ? AudioMode.Vorbis : AudioMode.PcmWav;
            theform.setProgressTitle("影片分離聲音...");
            bool ok = await RunNativeStage(() => audio = Backend.ExtractAudio(sourceFile, Path.Combine(workPath, Path.GetFileNameWithoutExtension(targetFile)), mode, null, cancellationToken), cancellationToken);
            if (ok) { theform.setProgress(ProgressStep3End); theform.setProgressTitle("影片分離聲音完成"); }
            return ok;
        }
        public async Task<bool> step4_sourcePng_to_aiPng(string workPath, CancellationToken cancellationToken)
        {
            int scale = Convert.ToInt32(theform.my.explode("x ", theform.comboBox_ImageScale.Text.Trim())[1]);
            var progress = new StageProgress(theform, p => {
                double fraction = p.Total > 0 ? (double)p.Completed / p.Total.Value : 0;
                double start = p.Stage == "hash" || p.Stage == "copy" ? ProgressStep3End : ProgressStep4PrepareEnd;
                double end = p.Stage == "hash" ? ProgressStep4PrepareEnd : ProgressStep4End;
                theform.setProgress(start + Math.Min(1, fraction) * (end - start));
                theform.setProgressTitle(p.Stage == "hash" ? "比對重複影格..." : scale == 1 ? "複製原始圖片..." : "AI 放大影格...");
            });
            bool ok = await RunNativeStage(() => frames = PngFrameUpscaler.Run(frames, Path.Combine(workPath, "target"), scale,
                BinaryPath("native", "realesrgan"), BinaryPath("realesrgan-ncnn-vulkan-v0.2.0-windows", "models"), progress, cancellationToken), cancellationToken);
            if (ok) { theform.setProgress(ProgressStep4End); theform.setProgressTitle("影格處理完成: " + frames.Count); }
            return ok;
        }
        public async Task<bool> step5_aiPng_to_mp4(string workPath, string targetFile, CancellationToken cancellationToken)
        {
            var progress = new StageProgress(theform, p => {
                theform.setProgressTitle(p.Stage == "validate" ? "檢查輸出影音..." : "合成 MP4: " + p.Completed + " / " + p.Total);
                if (p.Total > 0) theform.setProgress(ProgressStep5Start + (double)p.Completed / p.Total.Value * (ProgressStep5End - ProgressStep5Start));
            });
            bool ok = await RunNativeStage(() => Backend.EncodeMp4(frames, audio, targetFile, EncoderPreference.PreferNvenc, progress, cancellationToken), cancellationToken);
            if (ok) { theform.setProgress(ProgressStep5Done); theform.setProgressTitle("MP4 輸出完成"); }
            return ok;
        }
        public async Task<bool> step6_remove_workPath(string workPath, CancellationToken cancellationToken)
        {
            theform.setProgressTitle("清理工作目錄...");
            bool ok = await RunNativeStage(() => { if (Directory.Exists(workPath)) Directory.Delete(workPath, true); }, cancellationToken);
            if (ok) theform.setProgress(ProgressAllDone);
            return ok;
        }
    }
}
