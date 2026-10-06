using System;
using System.Threading;
namespace utility_app
{
    public enum AudioMode { Aac, Mp3, Vorbis, PcmWav }
    public enum EncoderPreference { PreferNvenc, SoftwareOnly }
    public sealed class MediaInfo { public TimeSpan? Duration; public int Width, Height; public bool HasVideo, HasAudio; }
    public sealed class FrameSequence { public string DirectoryPath; public int Count, Width, Height, UniqueFrameCount; public int SarNumerator=1, SarDenominator=1; public TimeSpan SourceStartTime; }
    public sealed class AudioAsset { public string Path; public AudioMode Mode; public TimeSpan Duration, SourceStartTime; }
    public sealed class MediaProgress { public string Stage; public long Completed; public long? Total; }
    public sealed class NativeMediaException : Exception
    {
        public string Stage { get; private set; }
        public int NativeCode { get; private set; }
        public NativeMediaException(string stage, int code, string message, Exception inner = null)
            : base(stage + ": " + message + " (" + code + ")", inner) { Stage = stage; NativeCode = code; }
    }
    public interface IMediaBackend
    {
        MediaInfo Probe(string input, CancellationToken token);
        FrameSequence ExtractFrames(string input, string directory, IProgress<MediaProgress> progress, CancellationToken token);
        AudioAsset ExtractAudio(string input, string outputBase, AudioMode mode, IProgress<MediaProgress> progress, CancellationToken token);
        void EncodeMp4(FrameSequence frames, AudioAsset audio, string output, EncoderPreference encoder, IProgress<MediaProgress> progress, CancellationToken token);
    }
}
