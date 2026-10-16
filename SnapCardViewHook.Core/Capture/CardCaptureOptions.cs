using System;
using System.IO;

namespace SnapCardViewHook.Core.Capture
{
    internal enum CaptureSubject { Card, Deck }

    internal sealed class CardCaptureOptions
    {
        public CaptureSubject Subject { get; set; }
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1536;
        public float PaddingPercent { get; set; } = 10;
        public bool TransparentBackground { get; set; }
        public bool IncludeShadow { get; set; }
        public string OutputPath { get; set; }

        public CardCaptureOptions Snapshot() => Snapshot(".png");

        internal CardCaptureOptions Snapshot(string extension)
        {
            if (!Enum.IsDefined(typeof(CaptureSubject), Subject))
                throw new ArgumentOutOfRangeException(nameof(Subject));
            if (Width < 64 || Width > 4096 || Height < 64 || Height > 4096)
                throw new ArgumentOutOfRangeException(nameof(Width), "Image dimensions must be between 64 and 4096 pixels.");
            if (float.IsNaN(PaddingPercent) || PaddingPercent < 0 || PaddingPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(PaddingPercent));
            if (string.IsNullOrWhiteSpace(OutputPath))
                throw new ArgumentException("Choose an output path.", nameof(OutputPath));
            var path = Path.GetFullPath(OutputPath);
            if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The output file must have a " + extension + " extension.", nameof(OutputPath));
            if (File.Exists(path))
                throw new IOException("The output file already exists. Choose a new filename; captures never overwrite files.");
            return new CardCaptureOptions
            {
                Subject = Subject, Width = Width, Height = Height, PaddingPercent = PaddingPercent,
                TransparentBackground = TransparentBackground, IncludeShadow = IncludeShadow, OutputPath = path
            };
        }
    }

    internal enum CapturePixelLayout
    {
        StraightBgra,
        PremultipliedRgba,
        PremultipliedBgra
    }

    internal sealed class CapturedCardPixels
    {
        // Bottom-up rows. Layout is the requested final format; CPU fallback keeps raw RGBA
        // and optional mattes until a worker finishes processing them.
        public byte[] Data;
        public byte[] MatteBlackRgba;
        public byte[] MatteWhiteRgba;
        public bool NeedsCpuProcessing;
        public bool LinearColorSpace;
        public CapturePixelLayout Layout;
        public int Width;
        public int Height;
        public int RendererCount;
        public int CardCount;
        public string SourceCamera;
        public long CaptureTimestamp;
        public long ByteCount => (Data?.LongLength ?? 0) + (MatteBlackRgba?.LongLength ?? 0) +
            (MatteWhiteRgba?.LongLength ?? 0);
    }
}
