using System;
using System.Threading;

namespace SnapCardViewHook.Core.Capture
{
    internal static class CaptureAlphaReconstruction
    {
        private static readonly double[] LinearValues = CreateValues(true);
        private static readonly double[] GammaValues = CreateValues(false);
        
        public static void Apply(byte[] color, byte[] matteBlack, byte[] matteWhite,
            bool linearColorSpace, CancellationToken cancellation)
        {
            ApplyCore(color, matteBlack, matteWhite, linearColorSpace, false, cancellation);
        }
        
        public static void ApplyPremultipliedVideo(byte[] color, byte[] matteBlack, byte[] matteWhite,
            bool linearColorSpace, CancellationToken cancellation)
        {
            ApplyCore(color, matteBlack, matteWhite, linearColorSpace, true, cancellation);
        }

        private static void ApplyCore(byte[] color, byte[] matteBlack, byte[] matteWhite,
            bool linearColorSpace, bool premultipliedVideo, CancellationToken cancellation)
        {
            if (color == null || color.Length == 0 || color.Length % 4 != 0 ||
                matteBlack == null || matteWhite == null || matteBlack.Length != color.Length || matteWhite.Length != color.Length)
                throw new ArgumentException("Transparent capture requires matching color, black and white RGBA buffers.");
            var values = linearColorSpace ? LinearValues : GammaValues;
            for (var i = 0; i < color.Length; i += 4)
            {
                if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
                
                var transmission = Math.Max(values[matteWhite[i]] - values[matteBlack[i]],
                    Math.Max(values[matteWhite[i + 1]] - values[matteBlack[i + 1]],
                        values[matteWhite[i + 2]] - values[matteBlack[i + 2]]));
                var coverage = Clamp01(1 - transmission);

                if (premultipliedVideo)
                {
                    var encodedPeak = Math.Max(color[i], Math.Max(color[i + 1], color[i + 2]));
                    var coverageByte = (byte)Math.Ceiling(coverage * 255);
                    color[i + 3] = (byte)Math.Max(coverageByte, encodedPeak);
                    continue;
                }

                var r = values[color[i]];
                var g = values[color[i + 1]];
                var b = values[color[i + 2]];
                
                var alpha = Math.Max(coverage, Math.Max(r, Math.Max(g, b)));
                var alphaByte = (byte)Math.Ceiling(Clamp01(alpha) * 255);
                color[i + 3] = alphaByte;
                if (alphaByte == 0)
                {
                    color[i] = color[i + 1] = color[i + 2] = 0;
                }
                else if (alphaByte != 255)
                {
                    var inverseAlpha = 255.0 / alphaByte;
                    color[i] = Encode(r * inverseAlpha, linearColorSpace);
                    color[i + 1] = Encode(g * inverseAlpha, linearColorSpace);
                    color[i + 2] = Encode(b * inverseAlpha, linearColorSpace);
                }
            }
            cancellation.ThrowIfCancellationRequested();
        }

        private static double[] CreateValues(bool linear)
        {
            var values = new double[256];
            for (var i = 0; i < values.Length; i++)
            {
                var value = i / 255.0;
                values[i] = !linear ? value : value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }
            return values;
        }

        private static byte Encode(double value, bool linear)
        {
            value = Clamp01(value);
            if (linear) value = value <= 0.0031308 ? value * 12.92 : 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
            return (byte)Math.Round(Clamp01(value) * 255, MidpointRounding.AwayFromZero);
        }

        private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
    }
}
