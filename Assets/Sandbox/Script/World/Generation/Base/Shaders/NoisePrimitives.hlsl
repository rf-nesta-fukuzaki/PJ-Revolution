#ifndef SANDBOX_NOISE_PRIMITIVES_HLSL
#define SANDBOX_NOISE_PRIMITIVES_HLSL

// Stefan Gustavson / Ashima Arts: simplex 3D noise (public domain).
// 自己完結 / 外部依存なし。

float3 _SbxMod289_3(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 _SbxMod289_4(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 _SbxPermute(float4 x)  { return _SbxMod289_4(((x * 34.0) + 1.0) * x); }
float4 _SbxTaylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

float SimplexNoise3D(float3 v)
{
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
    const float4 D = float4(0.0, 0.5, 1.0, 2.0);

    float3 i  = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    float3 g  = step(x0.yzx, x0.xyz);
    float3 l  = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - D.yyy;

    i = _SbxMod289_3(i);
    float4 p = _SbxPermute(_SbxPermute(_SbxPermute(
                 i.z + float4(0.0, i1.z, i2.z, 1.0))
               + i.y + float4(0.0, i1.y, i2.y, 1.0))
               + i.x + float4(0.0, i1.x, i2.x, 1.0));

    float  n_ = 0.142857142857;
    float3 ns = n_ * D.wyz - D.xzx;

    float4 j  = p - 49.0 * floor(p * ns.z * ns.z);
    float4 x_ = floor(j * ns.z);
    float4 y_ = floor(j - 7.0 * x_);

    float4 x = x_ * ns.x + ns.yyyy;
    float4 y = y_ * ns.x + ns.yyyy;
    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, float4(0, 0, 0, 0));

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 p0 = float3(a0.xy, h.x);
    float3 p1 = float3(a0.zw, h.y);
    float3 p2 = float3(a1.xy, h.z);
    float3 p3 = float3(a1.zw, h.w);

    float4 norm = _SbxTaylorInvSqrt(float4(dot(p0, p0), dot(p1, p1),
                                           dot(p2, p2), dot(p3, p3)));
    p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w;

    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1),
                                dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    return 42.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1),
                                    dot(p2, x2), dot(p3, x3)));
}

// 軽量 FBM (mountain mask 用)
float FBM3D(float3 p, int octaves, float lacunarity, float gain)
{
    float a    = 1.0;
    float f    = 1.0;
    float sum  = 0.0;
    float norm = 0.0;
    [loop] for (int i = 0; i < octaves; i++)
    {
        sum  += a * SimplexNoise3D(p * f);
        norm += a;
        f    *= lacunarity;
        a    *= gain;
    }
    return sum / max(norm, 1e-5);
}

// Curl noise (2D divergence-free) - domain warp 用
float2 CurlNoise2D(float3 p)
{
    const float eps = 0.1;
    float n1 = SimplexNoise3D(p + float3(eps, 0, 0));
    float n2 = SimplexNoise3D(p - float3(eps, 0, 0));
    float n3 = SimplexNoise3D(p + float3(0, 0, eps));
    float n4 = SimplexNoise3D(p - float3(0, 0, eps));
    float dx = (n1 - n2) / (2.0 * eps);
    float dz = (n3 - n4) / (2.0 * eps);
    return float2(dz, -dx);
}

// Ridged Multifractal (Step 2 spec)
float RidgedMultifractal3D(float3 p, int octaves, float H, float lacunarity,
                           float offset, float gain)
{
    float result    = 0.0;
    float frequency = 1.0;
    float weight    = 1.0;
    [loop] for (int i = 0; i < octaves; i++)
    {
        float signal = SimplexNoise3D(p * frequency);
        signal = offset - abs(signal);
        signal = signal * signal;
        signal *= saturate(weight);

        result    += signal * pow(abs(frequency), -H); // frequency は常に正だがコンパイラ警告回避に abs
        weight     = signal * gain;
        frequency *= lacunarity;
    }
    return result;
}

#endif // SANDBOX_NOISE_PRIMITIVES_HLSL
