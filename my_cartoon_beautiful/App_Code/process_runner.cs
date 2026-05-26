using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace utility_app
{
    public sealed class ProcessRunResult
    {
        public string FileName { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public int ExitCode { get; set; }
        public bool Cancelled { get; set; }
        public bool TimedOut { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public TimeSpan Duration { get; set; }
        public string LogFilePath { get; set; }

        public bool Success
        {
            get { return !Cancelled && !TimedOut && ExitCode == 0; }
        }

        public string CommandLine
        {
            get { return (FileName + " " + Arguments).Trim(); }
        }

        public string GetErrorSummary(int maxChars)
        {
            string text = string.IsNullOrWhiteSpace(StandardError) ? StandardOutput : StandardError;
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }
            text = text.Trim();
            if (maxChars > 0 && text.Length > maxChars)
            {
                return text.Substring(text.Length - maxChars);
            }
            return text;
        }
    }

    public static class ProcessRunner
    {
        private const int MaxCapturedChars = 256 * 1024;

        public static async Task<ProcessRunResult> RunAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken,
            int timeoutMilliseconds,
            string logFilePath,
            Action<string> onOutput = null,
            Action<string> onError = null)
        {
            if (startInfo == null)
            {
                throw new ArgumentNullException(nameof(startInfo));
            }
            if (string.IsNullOrWhiteSpace(startInfo.FileName))
            {
                throw new ArgumentException("ProcessStartInfo.FileName is required.", nameof(startInfo));
            }

            ProcessRunResult result = CreateBaseResult(startInfo, logFilePath);
            if (cancellationToken.IsCancellationRequested)
            {
                result.Cancelled = true;
                result.ExitCode = -1;
                result.Duration = TimeSpan.Zero;
                WriteLogFile(result);
                return result;
            }

            LimitedTextBuffer stdout = new LimitedTextBuffer(MaxCapturedChars);
            LimitedTextBuffer stderr = new LimitedTextBuffer(MaxCapturedChars);
            DateTime startedAt = DateTime.Now;
            TaskCompletionSource<bool> exited = new TaskCompletionSource<bool>();
            bool cancelled = false;
            bool timedOut = false;

            using (Process process = new Process())
            using (CancellationTokenSource timeoutCts = timeoutMilliseconds > 0 ? new CancellationTokenSource() : null)
            {
                PrepareStartInfo(startInfo);
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;
                process.Exited += delegate
                {
                    exited.TrySetResult(true);
                };
                process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    AppendLine(stdout, e.Data, onOutput);
                };
                process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    AppendLine(stderr, e.Data, onError);
                };

                CancellationTokenRegistration cancelRegistration = default(CancellationTokenRegistration);
                CancellationTokenRegistration timeoutRegistration = default(CancellationTokenRegistration);
                try
                {
                    if (timeoutCts != null)
                    {
                        timeoutRegistration = timeoutCts.Token.Register(delegate
                        {
                            timedOut = true;
                            TryKill(process);
                        });
                        timeoutCts.CancelAfter(timeoutMilliseconds);
                    }
                    cancelRegistration = cancellationToken.Register(delegate
                    {
                        cancelled = true;
                        TryKill(process);
                    });

                    try
                    {
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                    }
                    catch (Exception ex)
                    {
                        stderr.AppendLine(ex.ToString());
                        result.ExitCode = -1;
                        result.StandardOutput = stdout.ToString();
                        result.StandardError = stderr.ToString();
                        result.Duration = DateTime.Now - startedAt;
                        WriteLogFile(result);
                        return result;
                    }

                    await exited.Task.ConfigureAwait(false);
                    try
                    {
                        // 等待 async output/error event 收尾，避免 log 少最後幾行。
                        process.WaitForExit();
                    }
                    catch
                    {
                    }

                    try
                    {
                        result.ExitCode = process.ExitCode;
                    }
                    catch
                    {
                        result.ExitCode = -1;
                    }
                }
                finally
                {
                    cancelRegistration.Dispose();
                    timeoutRegistration.Dispose();
                }
            }

            result.Cancelled = cancelled || cancellationToken.IsCancellationRequested;
            result.TimedOut = timedOut;
            result.StandardOutput = stdout.ToString();
            result.StandardError = stderr.ToString();
            result.Duration = DateTime.Now - startedAt;
            WriteLogFile(result);
            return result;
        }

        private static ProcessRunResult CreateBaseResult(ProcessStartInfo startInfo, string logFilePath)
        {
            return new ProcessRunResult
            {
                FileName = startInfo.FileName,
                Arguments = startInfo.Arguments,
                WorkingDirectory = startInfo.WorkingDirectory,
                ExitCode = -1,
                StandardOutput = "",
                StandardError = "",
                LogFilePath = logFilePath
            };
        }

        private static void PrepareStartInfo(ProcessStartInfo startInfo)
        {
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
        }

        private static void AppendLine(LimitedTextBuffer buffer, string line, Action<string> callback)
        {
            if (line == null)
            {
                return;
            }
            buffer.AppendLine(line);
            if (callback != null)
            {
                callback(line);
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }

        private static void WriteLogFile(ProcessRunResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.LogFilePath))
            {
                return;
            }
            try
            {
                string dir = Path.GetDirectoryName(result.LogFilePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                StringBuilder log = new StringBuilder();
                log.AppendLine("Command: " + result.CommandLine);
                log.AppendLine("WorkingDirectory: " + (result.WorkingDirectory ?? ""));
                log.AppendLine("ExitCode: " + result.ExitCode);
                log.AppendLine("Cancelled: " + result.Cancelled);
                log.AppendLine("TimedOut: " + result.TimedOut);
                log.AppendLine("DurationSeconds: " + result.Duration.TotalSeconds.ToString("0.000"));
                log.AppendLine("");
                log.AppendLine("[stdout]");
                log.AppendLine(result.StandardOutput ?? "");
                log.AppendLine("");
                log.AppendLine("[stderr]");
                log.AppendLine(result.StandardError ?? "");
                File.WriteAllText(result.LogFilePath, log.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private sealed class LimitedTextBuffer
        {
            private readonly int maxChars;
            private readonly StringBuilder builder = new StringBuilder();
            private readonly object syncRoot = new object();

            public LimitedTextBuffer(int maxChars)
            {
                this.maxChars = maxChars;
            }

            public void AppendLine(string line)
            {
                lock (syncRoot)
                {
                    builder.AppendLine(line);
                    TrimIfNeeded();
                }
            }

            public override string ToString()
            {
                lock (syncRoot)
                {
                    return builder.ToString();
                }
            }

            private void TrimIfNeeded()
            {
                if (builder.Length <= maxChars)
                {
                    return;
                }
                int removeLength = builder.Length - maxChars;
                builder.Remove(0, removeLength);
            }
        }
    }
}
