using my_cartoon_beautiful;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace utility_app
{
    public class myApp
    {
        Form1 theform;
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
            long totalsFrame = theform.my.getMovieTotalFrames(ffmpegBin, sourceFile);
            Console.WriteLine("總幀數: " + totalsFrame.ToString());
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("取得總幀數...:" + totalsFrame.ToString());
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
                Arguments = $" -y -i \"{sourceFile}\" -vf \"fps=30\" -progress \"{progressFilePath}\" -f image2  \"{workPath}\\source\\%08d.png\" ",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            theform.setProgressTitle("開始將影像轉成 png 圖片...");

            return await Task.Run(() =>
            {
                using (Process process = Process.Start(startInfo))
                {
                    // 獲取進度
                    try
                    {
                        string last_progressText = null;
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill(); // 終止 ffmpeg 進程
                                }
                                catch
                                {
                                    // 可能會因為進程已經退出而導致異常，忽略此類異常
                                }
                                cancellationToken.ThrowIfCancellationRequested();
                            }

                            if (File.Exists(progressFilePath))
                            {
                                string progressText = theform.my.readFileWithRetry(progressFilePath, 50, 100).Replace("\r", "").Trim();

                                if (progressText == "")
                                {
                                    Task.Delay(1000).Wait(); // 非阻塞的延遲
                                    continue;
                                }
                                bool isEnd = false;
                                var m = theform.my.explode("\n", progressText);
                                if (last_progressText == null)
                                {
                                    last_progressText = progressText;
                                }
                                else
                                {
                                    if (last_progressText == progressText)
                                    {
                                        isEnd = true;
                                    }
                                }

                                for (int i = m.Count() - 1; i >= 0; i--)
                                {
                                    Console.WriteLine(m[i]);
                                    if (m[i].Trim() == "progress=end")
                                    {
                                        isEnd = true;
                                    }
                                    if (theform.my.is_string_like_new(m[i], "frame=%"))
                                    {
                                        double d = Convert.ToDouble(theform.my.explode("frame=", m[i].Trim())[1]);
                                        double p = theform.my.arduino_map(d, 0, totalsFrame, 0.0, 15.0);
                                        p = (p >= 15) ? 15.0 : p;
                                        theform.Invoke((MethodInvoker)(() => theform.setProgress(p)));
                                        break;
                                    }
                                }

                                if (isEnd)
                                {
                                    break;
                                }
                                last_progressText = progressText;
                            }
                            Task.Delay(1000).Wait(); // 非阻塞的延遲
                        }; // while

                        //if (File.Exists(progressFilePath))
                        {
                            //string progressText = File.ReadAllText(progressFilePath);
                            //Console.WriteLine("PPPPPPPPPPPPPPPPPPPPPPPPP Done:");
                            //Console.WriteLine(progressText);
                            theform.Invoke((MethodInvoker)(() => theform.setProgress(15)));
                        }

                        //string error = process.StandardError.ReadToEnd();
                        //Console.WriteLine("Error: " + error);
                    }
                    catch
                    {
                        return false;
                    }
                }
                return true;
            }, cancellationToken);
        }
        public async Task<bool> step3_sourceFile_to_mp3(string workPath, string sourceFile, string targetFile, CancellationToken cancellationToken)
        {
            string ffmpegBin = Path.Combine(theform.PWD, "binary", "ffmpeg.exe");
            if (!theform.my.is_file(ffmpegBin))
            {
                MessageBox.Show("轉檔工具 " + ffmpegBin + " 不存在...", "異常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("影片轉出聲音檔...");
            }));
            string mp3_file = Path.Combine(workPath, theform.my.mainname(targetFile) + ".mp3");
            string progressFilePath = Path.Combine(workPath, "progress.txt");
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegBin,
                Arguments = $" -y -i \"{sourceFile}\" -progress \"{progressFilePath}\" -vn  \"{mp3_file}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("開始將影片轉出聲音檔...");
            }));
            return await Task.Run(() =>
            {
                using (Process process = Process.Start(startInfo))
                {
                    try
                    {
                        // 獲取進度
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill(); // 終止 ffmpeg 進程
                                }
                                catch
                                {
                                    // 可能會因為進程已經退出而導致異常，忽略此類異常
                                }

                                cancellationToken.ThrowIfCancellationRequested();

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
                                theform.setProgressTitle("影片轉出聲音檔完成");
                            }));
                        }
                        //string error = process.StandardError.ReadToEnd();
                        //Console.WriteLine("Error: " + error);
                    }
                    catch
                    {
                        return false;
                    }
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

            long totalsPngs = theform.my.glob(sourcePath, "*.png").Count();
            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("計算有多少圖片需處理..." + totalsPngs.ToString());
            }));

            if (theform.my.is_dir(targetPath))
            {
                theform.my.deltree(targetPath);
            }
            theform.my.mkdir(targetPath);
            await Task.Delay(1000); // 使用非阻塞的延遲

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = aiRNVBin,
                Arguments = $" -i \"{sourcePath}\" -o \"{targetPath}\" -s " + imageScale + " -f png",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("高解析度影像轉檔...");
            }));

            return await Task.Run(() =>
            {
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;

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

                    try
                    {
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();


                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill();
                                }
                                catch
                                {
                                    // 忽略因進程已經退出而產生的異常
                                }

                                cancellationToken.ThrowIfCancellationRequested();

                            }

                            long nowPngs = theform.my.glob(targetPath, "*.png").Count();

                            theform.Invoke((MethodInvoker)(() =>
                            {
                                theform.setProgressTitle($"高解析度影像轉檔... {nowPngs} / {totalsPngs}");
                                double percentComplete = (double)nowPngs / totalsPngs * 100.0;
                                double showPercent = theform.my.arduino_map(percentComplete, 0, 100.0, 18.0, 87.0);
                                theform.setProgress(showPercent);
                            }));

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
                        Console.WriteLine($"Exception: {ex.Message}");
                        return false;
                    }
                }

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
            string mp3File = Path.Combine(workPath, theform.my.mainname(targetFile) + ".mp3");
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

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegBin,
                Arguments = $" -y -framerate 30 -i \"{aIPngPath}\\%08d.png\" -i \"{mp3File}\" -progress \"{progressFilePath}\" -c:v libx264 -pix_fmt yuv420p -c:a aac -strict experimental \"{targetFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            theform.Invoke((MethodInvoker)(() =>
            {
                theform.setProgressTitle("高解析度影像與聲音合併中...");
            }));

            return await Task.Run(() =>
            {
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;

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

                    try
                    {
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        string last_progressText = null;

                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill();
                                }
                                catch
                                {
                                    // 忽略因進程已經退出而產生的異常
                                }
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            if (File.Exists(progressFilePath))
                            {
                                string progressText = theform.my.readFileWithRetry(progressFilePath, 50, 100).Replace("\r", "").Trim();

                                if (progressText == "")
                                {
                                    Task.Delay(1000).Wait(); // 非阻塞的延遲
                                    continue;
                                }

                                bool isEnd = false;
                                var m = theform.my.explode("\n", progressText);
                                if (last_progressText == null)
                                {
                                    last_progressText = progressText;
                                }
                                else
                                {
                                    if (last_progressText == progressText)
                                    {
                                        isEnd = true;
                                    }
                                }
                                for (int i = m.Count() - 1; i >= 0; i--)
                                {
                                    if (m[i].Trim() == "progress=end")
                                    {
                                        isEnd = true;
                                    }
                                    if (theform.my.is_string_like_new(m[i], "frame=%"))
                                    {
                                        double d = Convert.ToDouble(theform.my.explode("frame=", m[i].Trim())[1]);
                                        double p = theform.my.arduino_map(d, 0, totalsPngs, 88, 97.0);
                                        p = (p >= 97) ? 97.0 : p;
                                        theform.Invoke((MethodInvoker)(() => theform.setProgress(p)));
                                        break;
                                    }
                                }
                                if (isEnd)
                                {
                                    break;
                                }
                                last_progressText = progressText;
                            }
                            Task.Delay(1000).Wait(); // 非阻塞的延遲

                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex.Message}");
                        return false;
                    }
                }

                theform.Invoke((MethodInvoker)(() =>
                {
                    theform.setProgress(98);
                    theform.setProgressTitle("高解析度影像與聲音合併完成...");
                }));
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
