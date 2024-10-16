﻿using my_cartoon_beautiful;
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
        public async Task<bool> step2_sourceFile_to_png(string workPath, string sourceFile, CancellationToken cancellationToken)
        {
            string ffmpegBin = Path.Combine(theform.PWD, "binary", "ffmpeg.exe");
            if (!theform.my.is_file(ffmpegBin))
            {
                MessageBox.Show("轉檔工具 " + ffmpegBin + " 不存在...", "異常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("取得總幀數...");
            }));
            long totalsFrame = theform.my.getMovieTotalFrames(workPath, ffmpegBin, sourceFile);
            Console.WriteLine("總幀數: " + totalsFrame.ToString());
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("取得總幀數...: " + totalsFrame.ToString());
            }));

            string progressFilePath = Path.Combine(workPath, "progress.txt");
            string sp = Path.Combine(workPath, "source");
            if (theform.my.is_dir(sp))
            {
                theform.my.deltree(sp);
            }
            theform.my.mkdir(sp);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegBin,
                // -progress \"{progressFilePath}\"
                Arguments = $" -hwaccel auto -y -i \"{sourceFile}\" -vf \"fps=30\" -f image2  \"{workPath}\\source\\%08d.png\" ",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            theform.setProgressTitle("影像轉成 png : " + totalsFrame.ToString());

            return await Task.Run(() =>
            {
                using (Process process = Process.Start(startInfo))
                {
                    // 獲取進度
                    Int64 st = Convert.ToInt64(theform.my.strtotime(theform.my.grid_getRowValueFromNindNameAndCellName(theform.logDataGridView, "將 影片轉 png", "開始時間")));
                    try
                    {
                        bool isCancel = false;
                        long last_nowPngs = 0;
                        Int64 last_changeDatetime = theform.my.time64();
                        while (true) //!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    Console.WriteLine("Do kill ffmpeg...");
                                    isCancel = true;
                                    process.Kill(); // 終止 ffmpeg 進程
                                    process.Dispose();
                                    Console.WriteLine("ffmpeg process killed.");
                                    break;

                                }
                                catch (Exception ex)
                                {
                                    // 可能會因為進程已經退出而導致異常，忽略此類異常
                                    Console.WriteLine($"Exception while trying to kill ffmpeg: {ex.Message}");
                                }
                                isCancel = true;
                                cancellationToken.ThrowIfCancellationRequested();
                                break;
                            }
                            try
                            {
                                var nowPngs = Convert.ToInt64(theform.my.glob(Path.Combine(workPath, "source"), "*.png").Count());
                                if (nowPngs != last_nowPngs)
                                {
                                    last_nowPngs = nowPngs;
                                    last_changeDatetime = theform.my.time64();
                                }
                                double p = theform.my.arduino_map(nowPngs, 0, totalsFrame, 0.0, 15.0);
                                p = (p >= 15) ? 15.0 : p;
                                theform.Invoke((MethodInvoker)(() =>
                                {
                                    if (nowPngs >= totalsFrame)
                                    {
                                        nowPngs = totalsFrame;
                                    }
                                    theform.setProgressTitle("影像轉成 png: " + nowPngs.ToString() + " / " + totalsFrame.ToString());
                                    theform.setProgress(p);
                                }));
                                if (Math.Abs(totalsFrame - nowPngs) <= 5)
                                {
                                    Task.Delay(1000).Wait(); // 非阻塞的延遲
                                    theform.Invoke((MethodInvoker)(() =>
                                    {
                                        if (nowPngs >= totalsFrame)
                                        {
                                            nowPngs = totalsFrame;
                                        }
                                        theform.setProgressTitle("影像轉成 png: " + nowPngs.ToString() + " / " + totalsFrame.ToString());
                                        theform.setProgress(p);
                                    }));
                                    break;
                                }
                                // 如果進度大於 nowPngs / totalsFrame * 100，98% ，且超過30秒沒有變化，則認定正常完成
                                if ((Convert.ToDouble(nowPngs) / Convert.ToDouble(totalsFrame)) * 100.0 >= 98.0 && theform.my.time64() - last_changeDatetime > 30)
                                {
                                    Task.Delay(1000).Wait(); // 非阻塞的延遲
                                    theform.Invoke((MethodInvoker)(() =>
                                    {
                                        if (nowPngs >= totalsFrame)
                                        {
                                            nowPngs = totalsFrame;
                                        }
                                        theform.setProgressTitle("影像轉成 png: " + nowPngs.ToString() + " / " + totalsFrame.ToString());
                                        theform.setProgress(p);
                                    }));
                                    break;
                                }

                                Int64 et = Convert.ToInt64(theform.my.strtotime(theform.my.date("Y-m-d H:i:s")));
                                Int64 duration = et - st;
                                theform.my.grid_updateRow(theform.logDataGridView, "將 影片轉 png", "經過時間", duration + " 秒");
                                Task.Delay(1000).Wait(); // 非阻塞的延遲
                            }
                            catch
                            {
                                Task.Delay(1000).Wait(); // 非阻塞的延遲
                            }
                        }; // while
                        if (isCancel)
                        {
                            return false;
                        }
                        theform.Invoke((MethodInvoker)(() => theform.setProgress(15)));
                    }
                    catch
                    {
                        return false;
                    }
                }
                theform.Invoke((MethodInvoker)(() =>
                {
                    theform.setProgressTitle("影像轉成 png: " + totalsFrame.ToString() + " / " + totalsFrame.ToString());
                }));
                return true;
            }, cancellationToken);
        }
        public async Task<bool> step3_sourceFile_to_wav(string workPath, string sourceFile, string targetFile, CancellationToken cancellationToken)
        {
            string ffmpegBin = Path.Combine(theform.PWD, "binary", "ffmpeg.exe");
            if (!theform.my.is_file(ffmpegBin))
            {
                MessageBox.Show("轉檔工具 " + ffmpegBin + " 不存在...", "異常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("影片分離聲音...");
            }));

            string soundkind = theform.comboBox_soundKind.Text.Trim();
            Console.WriteLine("soundkind: " + soundkind);
            string wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".aac");
            string sound_param = "";
            switch (soundkind.ToUpper())
            {
                case "AAC":
                    wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".aac");
                    sound_param = "-c:a aac -b:a 192k";
                    break;
                case "LIBMP3LAME":
                    wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".mp3");
                    sound_param = "-c:a libmp3lame -q:a 4";
                    break;
                case "OGG":
                    wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".ogg");
                    sound_param = "-c:a libvorbis -q:a 4";
                    break;
                default:
                    // 原音
                    wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".wav");
                    break;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegBin,
                Arguments = $" -hwaccel auto -y -i \"{sourceFile}\" -vn {sound_param} \"{wav_file}\"",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("開始將影片分離聲音...");
            }));
            return await Task.Run(() =>
            {
                bool isCancel = false;
                using (Process process = Process.Start(startInfo))
                {
                    try
                    {
                        Int64 st = Convert.ToInt64(theform.my.strtotime(theform.my.grid_getRowValueFromNindNameAndCellName(theform.logDataGridView, "將 影片分離聲音", "開始時間")));
                        // 獲取進度
                        //記錄檔案最後的變化，如果連 30 秒都沒變，也中斷
                        Int64 last_change_datetime = theform.my.time64();
                        Int64 last_file_size = 0;
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                for (int i = 0; i < 5; i++)
                                {
                                    try
                                    {
                                        process.Kill(); // 終止 ffmpeg 進程
                                        process.Dispose();
                                    }
                                    catch
                                    {
                                    }
                                }

                                isCancel = true;
                                cancellationToken.ThrowIfCancellationRequested();
                                break;

                            }
                            Int64 et = Convert.ToInt64(theform.my.strtotime(theform.my.date("Y-m-d H:i:s")));
                            Int64 duration = et - st;
                            theform.my.grid_updateRow(theform.logDataGridView, "將 影片分離聲音", "經過時間", duration + " 秒");

                            if (theform.my.is_file(wav_file))
                            {
                                Int64 fSize = theform.my.filesize(wav_file);
                                if (fSize != last_file_size)
                                {
                                    last_file_size = fSize;
                                    last_change_datetime = theform.my.time64();
                                }
                                if (fSize == last_file_size && theform.my.time64() - last_change_datetime > 30)
                                {
                                    break;
                                }
                            }

                            Task.Delay(1000).Wait(); // 非阻塞的延遲
                        }

                        //if (File.Exists(progressFilePath))
                        {
                            //string progressText = File.ReadAllText(progressFilePath);
                            //Console.WriteLine("PPPPPPPPPPPPPPPPPPPPPPPPP Done:");
                            //Console.WriteLine(progressText);
                            theform.Invoke((MethodInvoker)(() =>
                            {
                                theform.setProgress(18);
                                theform.setProgressTitle("影片分離出聲音完成");
                            }));
                        }
                        //string error = process.StandardError.ReadToEnd();
                        //Console.WriteLine("Error: " + error);
                        if (isCancel)
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
                if (isCancel)
                {
                    return false;
                }
                return true;
            }, cancellationToken);
        }
        public async Task<bool> step4_sourcePng_to_aiPng(string workPath, CancellationToken cancellationToken)
        {
            string aiRNVBin = Path.Combine(theform.PWD, "binary", "realesrgan-ncnn-vulkan-v0.2.0-windows", "realesrgan-ncnn-vulkan.exe");
            if (!theform.my.is_file(aiRNVBin))
            {
                MessageBox.Show("AI 轉檔工具 " + aiRNVBin + " 不存在...", "異常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("原影像圖片轉成高解析度圖片...");
            }));

            string sourcePath = Path.Combine(workPath, "source");
            string targetPath = Path.Combine(workPath, "target");
            string imageScale = Convert.ToInt32(theform.my.explode("x ", theform.comboBox_ImageScale.Text.Trim())[1]).ToString();

            // 計算總圖片數量
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("計算有多少圖片需處理...");
            }));


            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("計算刪除重複影像後數量...");
            }));
            //2024-08-04 找出重複的圖片，我希望只處理一次
            var sourcePngs = theform.my.glob(sourcePath, "*.png");
            Dictionary<string, List<string>> fileMd5 = new Dictionary<string, List<string>>();

            //原始檔名，只記 BN
            //只記與他相同的檔案名稱
            var sourcePngsLists = new Dictionary<string, string>();
            int step = 0;
            int total = sourcePngs.Count();
            foreach (string png in sourcePngs)
            {
                string md5 = theform.my.md5_file(png);
                string bn = theform.my.basename(png);
                if (!fileMd5.ContainsKey(md5))
                {
                    fileMd5[md5] = new List<string>();
                    fileMd5[md5].Add(bn);
                    sourcePngsLists[bn] = bn;
                }
                else
                {
                    fileMd5[md5].Add(bn);
                    sourcePngsLists[bn] = fileMd5[md5][0];
                    //刪除這張
                    theform.my.unlink(png);
                }
                if (step % 10 == 0)
                {
                    theform.Invoke((MethodInvoker)(() =>
                    {
                        theform.setProgressTitle("計算刪除重複影像後數量..." + step.ToString() + " / " + total.ToString());
                        theform.setProgress(total == 0 ? 22 : theform.my.arduino_map(step, 0, total, 18.0, 22.0));
                    }));
                }
                step++;
            }
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("計算刪除重複影像後數量..." + theform.my.glob(sourcePath, "*.png").Count().ToString());
            }));

            if (theform.my.is_dir(targetPath))
            {
                theform.my.deltree(targetPath);
            }
            theform.my.mkdir(targetPath);
            await Task.Delay(1000); // 使用非阻塞的延遲

            long totalsPngs = theform.my.glob(sourcePath, "*.png").Count();
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("計算有多少圖片需處理..." + totalsPngs.ToString());
            }));


            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = aiRNVBin,
                Arguments = $" -i \"{sourcePath}\" -o \"{targetPath}\" -s " + imageScale + " -f png",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("高解析度影像轉檔...");
            }));

            return await Task.Run(() =>
            {
                bool isCancel = false;
                Int64 st = Convert.ToInt64(theform.my.strtotime(theform.my.grid_getRowValueFromNindNameAndCellName(theform.logDataGridView, "將 原影像 png 用 ai 轉成高解析度", "開始時間")));
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    /*
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Console.WriteLine($"Output: {e.Data}");
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Console.WriteLine($"Error: {e.Data}");
                        }
                    };
                    */

                    try
                    {
                        process.Start();
                        //process.BeginOutputReadLine();
                        //process.BeginErrorReadLine();

                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                for (int i = 0; i < 5; i++)
                                {
                                    try
                                    {
                                        process.Kill(); // 終止 ffmpeg 進程
                                        process.Dispose();
                                    }
                                    catch
                                    {
                                    }
                                }
                                isCancel = true;
                                cancellationToken.ThrowIfCancellationRequested();
                                break;
                            }

                            long nowPngs = theform.my.glob(targetPath, "*.png").Count();

                            theform.Invoke((MethodInvoker)(() =>
                            {
                                theform.setProgressTitle($"高解析度影像轉檔... {nowPngs} / {totalsPngs}");
                                double percentComplete = (double)nowPngs / totalsPngs * 100.0;
                                double showPercent = theform.my.arduino_map(percentComplete, 0, 100.0, 22.0, 87.0);
                                theform.setProgress(showPercent);
                            }));
                            Int64 et = Convert.ToInt64(theform.my.strtotime(theform.my.date("Y-m-d H:i:s")));
                            Int64 duration = et - st;
                            theform.my.grid_updateRow(theform.logDataGridView, "將 原影像 png 用 ai 轉成高解析度", "經過時間", duration + " 秒");
                            Task.Delay(1000).Wait(); // 非阻塞的延遲
                        }
                        /*
                        string error = process.StandardError.ReadToEnd();
                        if (!string.IsNullOrEmpty(error))
                        {
                            Console.WriteLine($"Error: {error}");
                        }
                        */
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception 436: {ex.Message}");
                        isCancel = true;
                        return false;
                    }
                }
                if (isCancel)
                {
                    return false;
                }

                //2024-08-04 針對 sourcePngsLists 補上相同的圖片
                var fp_target = theform.my.glob(targetPath, "*.png");
                var bn_target = new Dictionary<string, string>();
                foreach (var p in fp_target)
                {
                    bn_target[theform.my.basename(p)] = "";
                }
                foreach (var p in sourcePngsLists)
                {
                    if (!bn_target.ContainsKey(p.Key))
                    {
                        theform.my.copy(Path.Combine(targetPath, p.Value), Path.Combine(targetPath, p.Key));
                    }
                }
                Int64 _et = Convert.ToInt64(theform.my.strtotime(theform.my.date("Y-m-d H:i:s")));
                Int64 _duration = _et - st;
                theform.my.grid_updateRow(theform.logDataGridView, "將 原影像 png 用 ai 轉成高解析度", "經過時間", _duration + " 秒");
                Task.Delay(1000).Wait(); // 非阻塞的延遲

                return true;
            }, cancellationToken);
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
            string wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".mp3");

            switch (soundkind.ToUpper())
            {
                case "AAC":
                    wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".aac");
                    break;
                case "LIBMP3LAME":
                    wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".mp3");
                    break;
                case "OGG":
                    wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".ogg");
                    break;
                default:
                    // 原音
                    wav_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".wav");
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
                Arguments = $" -hwaccel auto -y -framerate 30 -i \"{aIPngPath}\\%08d.png\" -i \"{wav_file}\" -c:v \"{codec}\" -pix_fmt yuv420p -acodec copy \"{targetFile}\"",
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
                                double p = theform.my.arduino_map(frames, 0, totalsPngs, 88.0, 97.0);
                                p = (p >= 97.0) ? 97.0 : p;
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
                    theform.setProgress(98);
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
                        theform.setProgress(100.0);
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
