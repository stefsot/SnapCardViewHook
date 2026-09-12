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
        private static readonly LookupTables LinearLookup = new LookupTables(LinearValues, true);
        private static readonly LookupTables GammaLookup = new LookupTables(GammaValues, false);
        
        public static void Apply(byte[] color, byte[] matteBlack, byte[] matteWhite,
            bool linearColorSpace, CancellationToken cancellation)
        {
            ApplyCore(color, matteBlack, matteWhite, linearColorSpace, cancellation);
        }
        
        public static unsafe void ApplyPremultipliedVideo(byte[] color, byte[] matteBlack, byte[] matteWhite,
            bool linearColorSpace, CancellationToken cancellation)
        {
            ValidateBuffers(color, matteBlack, matteWhite);
            var lookup = linearColorSpace ? LinearLookup : GammaLookup;
            fixed (byte* rgba = color, black = matteBlack, white = matteWhite,
                coverage = lookup.Coverage, minimumAlpha = lookup.MinimumAlpha, premultiplied = lookup.Premultiplied)
            {
                for (var i = 0; i < color.Length; i += 4)
                {
                    if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
                    *(uint*)(rgba + i) = PremultipliedPixel(rgba + i, black + i, white + i,
                        coverage, minimumAlpha, premultiplied);
                }
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
            var lookup = pixels.LinearColorSpace ? LinearLookup : GammaLookup;
            fixed (byte* color = pixels.Rgba, black = pixels.MatteBlackRgba, white = pixels.MatteWhiteRgba,
                coverage = lookup.Coverage, minimumAlpha = lookup.MinimumAlpha, premultiplied = lookup.Premultiplied)
            {
                for (var y = 0; y < pixels.Height; y++)
                {
                    var row = (byte*)destination + checked(y * stride);
                    var source = (pixels.Height - 1 - y) * rowBytes;
                    var x = 0;
                    for (; x <= rowBytes - 16; x += 16, source += 16)
                        WritePremultipliedBgra4(color + source, black + source, white + source, row + x,
                            coverage, minimumAlpha, premultiplied);
                    for (; x < rowBytes; x += 4, source += 4)
                    {
                        var rgba = PremultipliedPixel(color + source, black + source, white + source,
                            coverage, minimumAlpha, premultiplied);
                        *(uint*)(row + x) = ToBgra(rgba);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WritePremultipliedBgra4(byte* color, byte* black, byte* white,
            byte* destination, byte* coverage, byte* minimumAlpha, byte* premultiplied)
        {
            // Each SIMD lane is one pixel. Table reads stay scalar; min/max uses exact byte values.
            var matte = Vector4.Min(ReadCoverage4(black, white, coverage),
                Vector4.Min(ReadCoverage4(black + 1, white + 1, coverage), ReadCoverage4(black + 2, white + 2, coverage)));
            var alpha = matte;
            if (matte != new Vector4(255))
            {
                var peak = Vector4.Max(ReadChannel4(color), Vector4.Max(ReadChannel4(color + 1), ReadChannel4(color + 2)));
                // MinimumAlpha is monotonic, so only the brightest channel needs a lookup.
                alpha = Vector4.Max(matte, new Vector4(minimumAlpha[(int)peak.X], minimumAlpha[(int)peak.Y],
                    minimumAlpha[(int)peak.Z], minimumAlpha[(int)peak.W]));
            }
            *(uint*)destination = ToBgra(PackPremultipliedPixel(color, (int)alpha.X, premultiplied));
            *(uint*)(destination + 4) = ToBgra(PackPremultipliedPixel(color + 4, (int)alpha.Y, premultiplied));
            *(uint*)(destination + 8) = ToBgra(PackPremultipliedPixel(color + 8, (int)alpha.Z, premultiplied));
            *(uint*)(destination + 12) = ToBgra(PackPremultipliedPixel(color + 12, (int)alpha.W, premultiplied));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe Vector4 ReadCoverage4(byte* black, byte* white, byte* coverage) => new Vector4(
            coverage[(white[0] << 8) | black[0]], coverage[(white[4] << 8) | black[4]],
            coverage[(white[8] << 8) | black[8]], coverage[(white[12] << 8) | black[12]]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe Vector4 ReadChannel4(byte* color) => new Vector4(color[0], color[4], color[8], color[12]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ToBgra(uint rgba) => ((rgba & 0xFF) << 16) | (rgba & 0xFF00FF00) | ((rgba >> 16) & 0xFF);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint PremultipliedPixel(byte* color, byte* black, byte* white,
            byte* coverage, byte* minimumAlpha, byte* premultiplied)
        {
            var matte = Math.Min(coverage[(white[0] << 8) | black[0]],
                Math.Min(coverage[(white[1] << 8) | black[1]], coverage[(white[2] << 8) | black[2]]));
            if (matte == 255) return *(uint*)color | 0xFF000000u;
            var peak = minimumAlpha[Math.Max(color[0], Math.Max(color[1], color[2]))];
            return PackPremultipliedPixel(color, Math.Max(matte, peak), premultiplied);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint PackPremultipliedPixel(byte* color, int alpha, byte* premultiplied)
        {
            if (alpha == 0) return 0;
            if (alpha == 255) return *(uint*)color | 0xFF000000u;
            var offset = alpha << 8;
            return (uint)(premultiplied[offset | color[0]] | (premultiplied[offset | color[1]] << 8) |
                (premultiplied[offset | color[2]] << 16)) | ((uint)alpha << 24);
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
                var alphaByte = ToAlpha(alpha);
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

        private static byte ToAlpha(double value) => (byte)Math.Ceiling(Clamp01(value) * 255);
        private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

        private sealed class LookupTables
        {
            public readonly byte[] Coverage = new byte[256 * 256];
            public readonly byte[] MinimumAlpha = new byte[256];
            public readonly byte[] Premultiplied = new byte[256 * 256];

            public LookupTables(double[] values, bool linear)
            {
                // Match PNG reconstruction and its rounding; calculate these values only once.
                for (var channel = 0; channel < 256; channel++)
                    MinimumAlpha[channel] = ToAlpha(values[channel]);

                for (var white = 0; white < 256; white++)
                    for (var black = 0; black < 256; black++)
                        Coverage[(white << 8) | black] = ToAlpha(1 - (values[white] - values[black]));

                // Alpha zero keeps the default zero RGB; opaque pixels retain their original RGB.
                for (var alpha = 1; alpha < 256; alpha++)
                {
                    var inverseAlpha = 255.0 / alpha;
                    for (var channel = 0; channel < 256; channel++)
                    {
                        var straight = alpha == 255 ? (byte)channel : Encode(values[channel] * inverseAlpha, linear);
                        Premultiplied[(alpha << 8) | channel] = (byte)((straight * alpha + 127) / 255);
                    }
                }
            }
        }
    }
}
