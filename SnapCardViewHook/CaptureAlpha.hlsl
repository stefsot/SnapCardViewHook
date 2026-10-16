// Byte-exact lookup tables preserve the existing capture's sRGB/alpha rounding.
// Compiled into SnapCardView.Hook.dll at build time; no runtime shader compiler.
Texture2D<float4> Color : register(t0);
Texture2D<float4> MatteBlack : register(t1);
Texture2D<float4> MatteWhite : register(t2);
Texture2D<uint4> Lookup : register(t3);
RWTexture2D<float4> Output : register(u0);

cbuffer CaptureSettings : register(b0)
{
    uint Width;
    uint Height;
    uint Flags;
    uint Padding;
};

static const uint Transparent = 1;
static const uint Premultiplied = 2;
static const uint SwapRedBlue = 4;
static const uint HardwareSrgb = 8;

uint3 ReadBytes(float3 value)
{
    // Typeless textures use raw UNORM views. A typed sRGB resource cannot use
    // such a view, so undo its automatic sampling conversion before rounding.
    if (Flags & HardwareSrgb)
        value = lerp(1.055 * pow(saturate(value), 1.0 / 2.4) - 0.055,
                     value * 12.92, value <= 0.0031308);
    return (uint3)floor(saturate(value) * 255.0 + 0.5);
}

[numthreads(8, 8, 1)]
void Reconstruct(uint3 thread : SV_DispatchThreadID)
{
    if (thread.x >= Width || thread.y >= Height) return;
    int3 pixel = int3(thread.xy, 0);
    uint3 color = ReadBytes(Color.Load(pixel).rgb);
    uint alpha = 255;
    if (Flags & Transparent)
    {
        uint3 black = ReadBytes(MatteBlack.Load(pixel).rgb);
        uint3 white = ReadBytes(MatteWhite.Load(pixel).rgb);
        uint3 coverage = uint3(Lookup.Load(int3(black.r, white.r, 0)).r,
                              Lookup.Load(int3(black.g, white.g, 0)).r,
                              Lookup.Load(int3(black.b, white.b, 0)).r);
        uint peak = max(color.r, max(color.g, color.b));
        alpha = max(min(coverage.r, min(coverage.g, coverage.b)), Lookup.Load(int3(peak, 0, 0)).g);
        uint4 r = Lookup.Load(int3(color.r, alpha, 0));
        uint4 g = Lookup.Load(int3(color.g, alpha, 0));
        uint4 b = Lookup.Load(int3(color.b, alpha, 0));
        color = (Flags & Premultiplied) ? uint3(r.a, g.a, b.a) : uint3(r.b, g.b, b.b);
    }
    if (Flags & SwapRedBlue) color = color.bgr;
    Output[thread.xy] = float4(color, alpha) / 255.0;
}
