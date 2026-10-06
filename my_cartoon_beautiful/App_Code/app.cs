using my_cartoon_beautiful;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
                //檢查工作目錄是否存在，不存就建立，已存在就移除
                if (theform.my.is_dir(workPath))
                {
                    theform.my.deltree(workPath);
                }
                theform.my.mkdir(workPath);
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
                if (!theform.IsDisposed && !theform.Disposing) MessageBox.Show(theform, ex.Message, "轉檔失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            string ffmpegBin = Path.Combine(theform.PWD, "binary", "ffmpeg.exe");
            if (!theform.my.is_file(ffmpegBin))
            {
                MessageBox.Show("轉檔工具 " + ffmpegBin + " 不存在...", "異常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("高解析度影像與聲音檔合併輸出...");
            }));

            string soundkind = theform.comboBox_soundKind.Text.Trim();
            string audioFile = Path.Combine(workPath, theform.my.mainname(targetFile) + ".mp3");

            switch (soundkind.ToUpper())
            {
                case "AAC":
                    audioFile = Path.Combine(workPath, theform.my.mainname(targetFile) + ".aac");
                    break;
                case "LIBMP3LAME":
                    audioFile = Path.Combine(workPath, theform.my.mainname(targetFile) + ".mp3");
                    break;
                case "OGG":
                    audioFile = Path.Combine(workPath, theform.my.mainname(targetFile) + ".ogg");
                    break;
                default:
                    // 原音
                    audioFile = Path.Combine(workPath, theform.my.mainname(targetFile) + ".wav");
                    break;
            }

            string aIPngPath = Path.Combine(workPath, "target");
            string progressFilePath = Path.Combine(workPath, "progress.txt");

            if (theform.my.is_file(progressFilePath))
            {
                theform.my.unlink(progressFilePath);
            }
            // 計算總圖片數量
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("計算有多少圖片需處理...");
            }));

            long totalsPngs = theform.my.glob(aIPngPath, "*.png").Count();
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("計算有多少圖片需處理..." + totalsPngs.ToString());
            }));
            await Task.Delay(1000); // 使用非阻塞的延遲

            string codec = (theform.my.checkNvenc(ffmpegBin)) ? "h264_nvenc" : "h264";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegBin,
                //-strict experimental
                // -hwaccel dxva2
                //libx264
                // -progress \"{progressFilePath}\" -loglevel quiet
                Arguments = $" -hwaccel auto -y -framerate 30 -i \"{aIPngPath}\\%08d.png\" -i \"{audioFile}\" -c:v \"{codec}\" -pix_fmt yuv420p -acodec copy \"{targetFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Console.WriteLine(startInfo.Arguments);

            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("高解析度影像與聲音合併中...");
            }));

            return await Task.Run(() =>
            {
                bool isCancel = false;
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;

                    /*process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Console.WriteLine($"Output: {e.Data}");
                        }
                    };
                    */

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data) && theform.my.is_string_like(e.Data, "frame=") && theform.my.is_string_like(e.Data, "fps="))
                        {
                            //Console.WriteLine($"Error: {e.Data}");
                            string frame = theform.my.get_between(e.Data, "frame= ", " fps=");
                            if (!string.IsNullOrEmpty(frame))
                            {
                                long frames = Convert.ToInt64(frame);
                                double p = theform.my.arduino_map(frames, 0, totalsPngs, ProgressStep5Start, ProgressStep5End);
                                p = (p >= ProgressStep5End) ? ProgressStep5End : p;
                                theform.Invoke((MethodInvoker)(() => theform.setProgress(p)));
                                /*if (frames >= totalsPngs - 1)
                                {
                                    isNeedStop = true;
                                }*/
                            }
                        }
                    };

                    try
                    {
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        // process.HasExited
                        Task.Delay(1000).Wait(); // 非阻塞的延遲
                        //Console.WriteLine("is file: " + theform.my.is_file(targetFile));
                        //Console.WriteLine("is file lock: " + theform.my.isFileLocked(targetFile));
                        //!theform.my.is_file(targetFile) || (theform.my.is_file(targetFile) && theform.my.isFileLocked(targetFile))
                        Int64 st = Convert.ToInt64(theform.my.strtotime(theform.my.grid_getRowValueFromNindNameAndCellName(theform.logDataGridView, "將 ai 轉的高解析度影像 與 聲音檔 合併輸出成 mp4", "開始時間")));
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill(); // 終止 ffmpeg 進程
                                    process.Dispose();
                                }
                                catch
                                {
                                }
                                isCancel = true;
                                cancellationToken.ThrowIfCancellationRequested();
                                break;
                            }
                            Int64 et = Convert.ToInt64(theform.my.strtotime(theform.my.date("Y-m-d H:i:s")));
                            Int64 duration = et - st;
                            theform.my.grid_updateRow(theform.logDataGridView, "將 ai 轉的高解析度影像 與 聲音檔 合併輸出成 mp4", "經過時間", duration + " 秒");
                            Task.Delay(1000).Wait(); // 非阻塞的延遲                            
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception 603: {ex.Message}");
                        return false;
                    }
                }

                theform.Invoke((MethodInvoker)(() =>
                {
                    theform.setProgress(ProgressStep5Done);
                    theform.setProgressTitle("高解析度影像與聲音合併完成...");
                }));
                if (isCancel)
                {
                    return false;
                }
                return true;
            }, cancellationToken);
        }
        public async Task<bool> step6_remove_workPath(string workPath, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    theform.Invoke((MethodInvoker)(() =>
                    {
                        theform.setProgressTitle("移除工作目錄區...");
                    }));
                    if (theform.my.is_dir(workPath))
                    {
                        theform.my.deltree(workPath);
                    }
                    theform.Invoke((MethodInvoker)(() =>
                    {
                        theform.setProgress(ProgressAllDone);
                        theform.setProgressTitle("高解析度影像與聲音合併完成...");
                    }));
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
