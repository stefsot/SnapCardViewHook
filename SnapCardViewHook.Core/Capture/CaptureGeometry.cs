using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SnapCardViewHook.Core.Capture
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct CaptureVector3
    {
        public float X, Y, Z;
        public CaptureVector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static CaptureVector3 operator +(CaptureVector3 a, CaptureVector3 b) => new CaptureVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static CaptureVector3 operator -(CaptureVector3 a, CaptureVector3 b) => new CaptureVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static CaptureVector3 operator *(CaptureVector3 a, float b) => new CaptureVector3(a.X * b, a.Y * b, a.Z * b);
        public static float Dot(CaptureVector3 a, CaptureVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CaptureQuaternion { public float X, Y, Z, W; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CaptureBounds
    {
        public CaptureVector3 Center, Extents;
        public bool IsValid => Center.IsFinite && Extents.IsFinite && Extents.X >= 0 && Extents.Y >= 0 && Extents.Z >= 0;
        public IEnumerable<CaptureVector3> Corners()
        {
            for (var i = 0; i < 8; i++)
                yield return Center + new CaptureVector3(
                    (i & 1) == 0 ? -Extents.X : Extents.X,
                    (i & 2) == 0 ? -Extents.Y : Extents.Y,
                    (i & 4) == 0 ? -Extents.Z : Extents.Z);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CaptureRect
    {
        public float X, Y, Width, Height;
        public CaptureRect(float width, float height) { X = 0; Y = 0; Width = width; Height = height; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CaptureColor
    {
        public float R, G, B, A;
        public CaptureColor(float alpha) { R = 0; G = 0; B = 0; A = alpha; }
        public CaptureColor(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }
    }

    internal sealed class CaptureFraming
    {
        public CaptureVector3 Position;
        public float OrthographicSize, Near, Far;

        public CaptureFraming Copy() => new CaptureFraming
        {
            Position = Position, OrthographicSize = OrthographicSize, Near = Near, Far = Far
        };

        public static CaptureFraming FitCard(IReadOnlyList<CaptureVector3> cardCorners, CaptureVector3 right,
            CaptureVector3 up, CaptureVector3 forward, CaptureVector3 originalPosition,
            bool orthographic, float fieldOfView, CardCaptureOptions options)
        {
            if (cardCorners == null || cardCorners.Count == 0)
                throw new InvalidOperationException("The card has no geometry for framing.");
            if (!right.IsFinite || !up.IsFinite || !forward.IsFinite || !originalPosition.IsFinite)
                throw new InvalidOperationException("The source camera has invalid transforms.");
            var min = new CaptureVector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new CaptureVector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var point in cardCorners)
            {
                if (!point.IsFinite) throw new InvalidOperationException("The card has invalid framing geometry.");
                var x = CaptureVector3.Dot(point, right);
                var y = CaptureVector3.Dot(point, up);
                var z = CaptureVector3.Dot(point, forward);
                min.X = Math.Min(min.X, x); min.Y = Math.Min(min.Y, y); min.Z = Math.Min(min.Z, z);
                max.X = Math.Max(max.X, x); max.Y = Math.Max(max.Y, y); max.Z = Math.Max(max.Z, z);
            }
            var middle = (min + max) * 0.5f;
            var half = (max - min) * 0.5f;
            var center = right * middle.X + up * middle.Y + forward * middle.Z;
            var halfHeight = Math.Max(half.Y, half.X * options.Height / options.Width) * (1 + 2 * options.PaddingPercent / 100);
            if (!CaptureVector3.Finite(halfHeight) || halfHeight < 0.000001f)
                throw new InvalidOperationException("The card has empty or invalid bounds.");
            var clearance = Math.Max(0.001f, halfHeight * 0.01f);
            float distance;
            if (orthographic)
                distance = Math.Max(CaptureVector3.Dot(center - originalPosition, forward), half.Z + halfHeight + clearance);
            else
            {
                if (!CaptureVector3.Finite(fieldOfView) || fieldOfView <= 1 || fieldOfView >= 179)
                    throw new InvalidOperationException("The source camera has an unsupported field of view.");
                distance = half.Z + halfHeight / (float)Math.Tan(fieldOfView * Math.PI / 360) + clearance;
            }
            var result = new CaptureFraming
            {
                Position = center - forward * distance, OrthographicSize = halfHeight,
                Near = Math.Max(0.0001f, (distance - half.Z) * 0.01f), Far = distance + half.Z + clearance * 10
            };
            if (!result.Position.IsFinite || !CaptureVector3.Finite(result.Far) || result.Far <= result.Near)
                throw new InvalidOperationException("Cannot frame this card safely.");
            return result;
        }

        public void IncludeDepthBounds(IReadOnlyList<CaptureBounds> renderBounds, CaptureVector3 forward)
        {
            foreach (var bounds in renderBounds)
            {
                if (!bounds.IsValid) continue;
                var centerDepth = CaptureVector3.Dot(bounds.Center - Position, forward);
                var halfDepth = Math.Abs(forward.X) * bounds.Extents.X +
                    Math.Abs(forward.Y) * bounds.Extents.Y + Math.Abs(forward.Z) * bounds.Extents.Z;
                var front = centerDepth - halfDepth;
                var back = centerDepth + halfDepth;
                if (!CaptureVector3.Finite(front) || !CaptureVector3.Finite(back) || back <= 0) continue;
                var clearance = Math.Max(0.001f, Math.Abs(back) * 0.001f);
                var far = back + clearance;
                if (!CaptureVector3.Finite(far)) continue;
                Near = Math.Min(Near, Math.Max(0.0001f, front - clearance));
                Far = Math.Max(Far, far);
            }
        }
    }
}
