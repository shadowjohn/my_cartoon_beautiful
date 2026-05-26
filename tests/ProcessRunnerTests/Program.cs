using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using utility_app;

internal static class Program
{
    private static int Main()
    {
        try
        {
            RunAsync().GetAwaiter().GetResult();
            Console.WriteLine("ProcessRunnerTests: PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static async Task RunAsync()
    {
        await CapturesExitCodeAndOutputAsync();
        await CancelsRunningProcessAsync();
        await WritesLogFileAsync();
    }

    private static async Task CapturesExitCodeAndOutputAsync()
    {
        ProcessRunResult result = await ProcessRunner.RunAsync(
            Cmd("/c \"echo out&&echo err 1>&2&&exit /b 7\""),
            CancellationToken.None,
            0,
            null);

        AssertEqual(7, result.ExitCode, "exit code");
        AssertContains(result.StandardOutput, "out", "stdout");
        AssertContains(result.StandardError, "err", "stderr");
        AssertFalse(result.Success, "non-zero exit should not be success");
    }

    private static async Task CancelsRunningProcessAsync()
    {
        using (CancellationTokenSource cts = new CancellationTokenSource())
        {
            cts.CancelAfter(200);
            ProcessRunResult result = await ProcessRunner.RunAsync(
                Pwsh("-NoProfile -Command \"Start-Sleep -Seconds 5\""),
                cts.Token,
                0,
                null);

            AssertTrue(result.Cancelled, "cancelled flag");
            AssertFalse(result.Success, "cancelled process should not be success");
        }
    }

    private static async Task WritesLogFileAsync()
    {
        string logFile = Path.Combine(Path.GetTempPath(), "process-runner-test-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            ProcessRunResult result = await ProcessRunner.RunAsync(
                Cmd("/c \"echo log-ok\""),
                CancellationToken.None,
                0,
                logFile);

            AssertTrue(result.Success, "zero exit should be success");
            string log = File.ReadAllText(logFile);
            AssertContains(log, "Command:", "log command");
            AssertContains(log, "log-ok", "log stdout");
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    private static ProcessStartInfo Cmd(string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = arguments
        };
    }

    private static ProcessStartInfo Pwsh(string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = arguments
        };
    }

    private static void AssertTrue(bool value, string name)
    {
        if (!value)
        {
            throw new InvalidOperationException("AssertTrue failed: " + name);
        }
    }

    private static void AssertFalse(bool value, string name)
    {
        if (value)
        {
            throw new InvalidOperationException("AssertFalse failed: " + name);
        }
    }

    private static void AssertEqual(int expected, int actual, string name)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }
    }

    private static void AssertContains(string text, string expected, string name)
    {
        if (text == null || text.IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0)
        {
            throw new InvalidOperationException(name + " should contain '" + expected + "'. Actual: " + text);
        }
    }
}
