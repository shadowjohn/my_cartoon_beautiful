using System;

namespace utility_app
{
    public sealed class FfmpegProgressState
    {
        public long Frame { get; set; }
        public string Progress { get; set; }
    }

    public static class FfmpegProgressParser
    {
        public static bool TryApplyLine(FfmpegProgressState state, string line)
        {
            if (state == null || string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            int pos = line.IndexOf('=');
            if (pos <= 0)
            {
                return false;
            }

            string key = line.Substring(0, pos).Trim();
            string value = line.Substring(pos + 1).Trim();
            if (key.Equals("frame", StringComparison.OrdinalIgnoreCase))
            {
                long frame;
                if (long.TryParse(value, out frame))
                {
                    state.Frame = frame;
                    return true;
                }
                return false;
            }
            if (key.Equals("progress", StringComparison.OrdinalIgnoreCase))
            {
                state.Progress = value;
                return true;
            }
            return false;
        }
    }
}
