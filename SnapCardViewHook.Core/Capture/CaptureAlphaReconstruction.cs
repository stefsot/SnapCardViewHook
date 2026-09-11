using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SnapCardViewHook.Core.Capture
{
    internal static class CaptureAlphaReconstruction
    {
        private static readonly double[] LinearValues = CreateValues(true);
        private static readonly double[] GammaValues = CreateValues(false);
        private static readonly byte[] LinearCoverage = CreateCoverage(LinearValues);
        private static readonly byte[] GammaCoverage = CreateCoverage(GammaValues);
        
        public static void Apply(byte[] color, byte[] matteBlack, byte[] matteWhite,
            bool linearColorSpace, CancellationToken cancellation)
        {
            ApplyCore(color, matteBlack, matteWhite, linearColorSpace, cancellation);
        }
        
        public static void ApplyPremultipliedVideo(byte[] color, byte[] matteBlack, byte[] matteWhite,
            bool linearColorSpace, CancellationToken cancellation)
        {
            Apply(color, matteBlack, matteWhite, linearColorSpace, cancellation);
            // Premultiply the reconstructed sRGB samples for video export, preserving PNG's alpha.
            for (var i = 0; i < color.Length; i += 4)
            {
                if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
                var alpha = color[i + 3];
                color[i] = (byte)((color[i] * alpha + 127) / 255);
                color[i + 1] = (byte)((color[i + 1] * alpha + 127) / 255);
                color[i + 2] = (byte)((color[i + 2] * alpha + 127) / 255);
            }
            cancellation.ThrowIfCancellationRequested();
        }

        internal static unsafe void WritePremultipliedBgra(CapturedCardPixels pixels, IntPtr destination, int stride)
        {
            if (pixels == null || pixels.Width <= 0 || pixels.Height <= 0)
                throw new ArgumentException("Invalid preview dimensions.", nameof(pixels));
            var rowBytes = checked(pixels.Width * 4);
            var length = checked(rowBytes * pixels.Height);
            if (destination == IntPtr.Zero || Math.Abs((long)stride) < rowBytes ||
                pixels.Rgba?.Length != length || pixels.MatteBlackRgba?.Length != length || pixels.MatteWhiteRgba?.Length != length)
                throw new ArgumentException("Invalid preview buffers.", nameof(pixels));
            fixed (byte* color = pixels.Rgba, black = pixels.MatteBlackRgba, white = pixels.MatteWhiteRgba,
                coverage = pixels.LinearColorSpace ? LinearCoverage : GammaCoverage)
            {
                for (var y = 0; y < pixels.Height; y++)
                {
                    var row = (byte*)destination + checked(y * stride);
                    var source = (pixels.Height - 1 - y) * rowBytes;
                    var x = 0;
                    for (; x <= rowBytes - 16; x += 16, source += 16)
                    {
                        var alpha = PremultipliedAlpha4(color + source, black + source, white + source, coverage);
                        WriteBgra(color + source, row + x, (byte)alpha.X);
                        WriteBgra(color + source + 4, row + x + 4, (byte)alpha.Y);
                        WriteBgra(color + source + 8, row + x + 8, (byte)alpha.Z);
                        WriteBgra(color + source + 12, row + x + 12, (byte)alpha.W);
                    }
                    for (; x < rowBytes; x += 4, source += 4)
                        WriteBgra(color + source, row + x,
                            PremultipliedAlpha(color + source, black + source, white + source, coverage));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe Vector4 PremultipliedAlpha4(byte* color, byte* black, byte* white, byte* coverage)
        {
            var r = new Vector4(coverage[(white[0] << 8) | black[0]], coverage[(white[4] << 8) | black[4]],
                coverage[(white[8] << 8) | black[8]], coverage[(white[12] << 8) | black[12]]);
            var g = new Vector4(coverage[(white[1] << 8) | black[1]], coverage[(white[5] << 8) | black[5]],
                coverage[(white[9] << 8) | black[9]], coverage[(white[13] << 8) | black[13]]);
            var b = new Vector4(coverage[(white[2] << 8) | black[2]], coverage[(white[6] << 8) | black[6]],
                coverage[(white[10] << 8) | black[10]], coverage[(white[14] << 8) | black[14]]);
            var peak = Vector4.Max(new Vector4(color[0], color[4], color[8], color[12]),
                Vector4.Max(new Vector4(color[1], color[5], color[9], color[13]),
                    new Vector4(color[2], color[6], color[10], color[14])));
            return Vector4.Max(Vector4.Min(r, Vector4.Min(g, b)), peak);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe byte PremultipliedAlpha(byte* color, byte* black, byte* white, byte* coverage)
        {
            var matte = Math.Min(coverage[(white[0] << 8) | black[0]],
                Math.Min(coverage[(white[1] << 8) | black[1]], coverage[(white[2] << 8) | black[2]]));
            return Math.Max(matte, Math.Max(color[0], Math.Max(color[1], color[2])));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteBgra(byte* color, byte* destination, byte alpha)
        {
            var rgba = *(uint*)color;
            *(uint*)destination = ((rgba & 0xFF) << 16) | (rgba & 0xFF00) |
                ((rgba >> 16) & 0xFF) | ((uint)alpha << 24);
        }

        private static void ApplyCore(byte[] color, byte[] matteBlack, byte[] matteWhite,
            bool linearColorSpace, CancellationToken cancellation)
        {
            ValidateBuffers(color, matteBlack, matteWhite);
            var values = linearColorSpace ? LinearValues : GammaValues;
            for (var i = 0; i < color.Length; i += 4)
            {
                if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
                
                var transmission = Math.Max(values[matteWhite[i]] - values[matteBlack[i]],
                    Math.Max(values[matteWhite[i + 1]] - values[matteBlack[i + 1]],
                        values[matteWhite[i + 2]] - values[matteBlack[i + 2]]));
                var coverage = Clamp01(1 - transmission);

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

        private static void ValidateBuffers(byte[] color, byte[] matteBlack, byte[] matteWhite)
        {
            if (color == null || color.Length == 0 || color.Length % 4 != 0 ||
                matteBlack == null || matteWhite == null || matteBlack.Length != color.Length || matteWhite.Length != color.Length)
                throw new ArgumentException("Transparent capture requires matching color, black and white RGBA buffers.");
        }

        private static byte[] CreateCoverage(double[] values)
        {
            var coverage = new byte[256 * 256];
            for (var white = 0; white < 256; white++)
                for (var black = 0; black < 256; black++)
                    coverage[(white << 8) | black] = (byte)Math.Ceiling(Clamp01(1 - (values[white] - values[black])) * 255);
            // The original coverage of the maximum RGB transmission equals the minimum channel coverage.
            return coverage;
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
