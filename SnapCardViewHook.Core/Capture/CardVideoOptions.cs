using System;
using System.IO;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class CardVideoOptions
    {
        public static string DefaultFfmpegPath => Path.Combine(
            Path.GetDirectoryName(typeof(CardVideoOptions).Assembly.Location), "ffmpeg.exe");
        public CardCaptureOptions Frames { get; set; }
        public string FfmpegPath { get; set; } = DefaultFfmpegPath;
        public int FramesPerSecond { get; set; } = 30;
        public int DurationSeconds { get; set; } = 10;
        public int ProResQuantizer { get; set; } = 4; // 0 = automatic, lower positive values = higher quality.
        public int RamLimitMegabytes { get; set; } = 2048;
        public long BytesPerCapturedFrame => checked((long)Frames.Width * Frames.Height * 4 * (Frames.TransparentBackground ? 3 : 1));

        public CardVideoOptions Snapshot()
        {
            if (Frames == null) throw new ArgumentException("Choose video capture settings.");
            var frames = Frames.Snapshot(".mov");
            if (FramesPerSecond < 1 || FramesPerSecond > 60)
                throw new ArgumentOutOfRangeException(nameof(FramesPerSecond), "Video frame rate must be between 1 and 60.");
            if (DurationSeconds < 1 || DurationSeconds > 600)
                throw new ArgumentOutOfRangeException(nameof(DurationSeconds), "Video duration must be between 1 and 600 seconds.");
            if (ProResQuantizer < 0 || ProResQuantizer > 32)
                throw new ArgumentOutOfRangeException(nameof(ProResQuantizer), "ProRes quality must be 0 (auto) or 1-32 (lower is higher quality).");
            if (RamLimitMegabytes < 64 || RamLimitMegabytes > 65536 || BytesPerCapturedFrame > RamLimitMegabytes * 1024L * 1024)
                throw new ArgumentOutOfRangeException(nameof(RamLimitMegabytes), "Choose a RAM limit between 64 and 65536 MB, large enough for at least one frame.");
            if (string.IsNullOrWhiteSpace(FfmpegPath))
                throw new ArgumentException("Browse to a local ffmpeg.exe. Nothing is downloaded automatically.");
            var executable = Path.GetFullPath(FfmpegPath.Trim());
            if (!File.Exists(executable) || !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Place ffmpeg.exe beside SnapCardViewHook.Core.dll, or browse to an existing executable with the prores_ks encoder.", executable);
            return new CardVideoOptions
            {
                Frames = frames, FfmpegPath = executable, FramesPerSecond = FramesPerSecond, DurationSeconds = DurationSeconds,
                ProResQuantizer = ProResQuantizer, RamLimitMegabytes = RamLimitMegabytes
            };
        }
    }

    internal sealed class CardVideoProgress
    {
        public int CapturedFrames, EncodedFrames, RepeatedFrames;
        public double VideoSeconds;
        public long BufferedBytes;
        public bool Encoding, Finalizing;
    }

    internal sealed class CardVideoResult
    {
        public string OutputPath;
        public int CapturedFrames, EncodedFrames, RepeatedFrames;
        public double VideoSeconds;
        public string Warning;
    }
}
