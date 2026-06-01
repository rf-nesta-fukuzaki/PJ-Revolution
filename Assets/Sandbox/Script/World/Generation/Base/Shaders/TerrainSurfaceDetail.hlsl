#ifndef SANDBOX_TERRAIN_SURFACE_DETAIL_INCLUDED
#define SANDBOX_TERRAIN_SURFACE_DETAIL_INCLUDED

// ============================================================================
//  Sandbox/TerrainSurfaceDetail.hlsl
//  外部テクスチャ無しで AAA 級の地表ディテールを得るための手続き的ノイズ群。
//  - value noise / fBm （アルベド変調・岩の層理・草のパッチ）
//  - 有限差分による法線摂動（マイクロバンプ）
//  - 斜度に応じた top/side 射影ブレンドで崖にも破綻しない
//  すべてワールド座標基準なのでチャンク跨ぎでシームレス。
// ============================================================================

// ── hash ──────────────────────────────────────────────────────────────────
float  sdHash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

// ── value noise 2D（smoothstep 補間）────────────────────────────────────────
float sdValueNoise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = sdHash21(i + float2(0.0, 0.0));
    float b = sdHash21(i + float2(1.0, 0.0));
    float c = sdHash21(i + float2(0.0, 1.0));
    float d = sdHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// ── fBm（0..~1 に概ね正規化）────────────────────────────────────────────────
float sdFbm2(float2 p, int octaves, float lacunarity, float gain)
{
    float amp = 0.5;
    float sum = 0.0;
    float norm = 0.0;
    [loop] for (int i = 0; i < octaves; i++)
    {
        sum  += amp * sdValueNoise2(p);
        norm += amp;
        p    *= lacunarity;
        amp  *= gain;
    }
    return sum / max(norm, 1e-4);
}

// ── ridged fBm（尾根状・岩の層理感を出す）──────────────────────────────────
float sdRidged2(float2 p, int octaves, float lacunarity, float gain)
{
    float amp = 0.5;
    float sum = 0.0;
    float norm = 0.0;
    [loop] for (int i = 0; i < octaves; i++)
    {
        float n = 1.0 - abs(sdValueNoise2(p) * 2.0 - 1.0);
        sum  += amp * n * n;
        norm += amp;
        p    *= lacunarity;
        amp  *= gain;
    }
    return sum / max(norm, 1e-4);
}

// ── 三面（top/side）ブレンドのマイクロ高さ場 ────────────────────────────────
//  slope01: 0=水平 1=垂直。水平面は XZ 射影、崖は XY/ZY 射影をブレンド。
float sdSurfaceHeight(float3 wp, float slope01, float freq, int oct)
{
    float top   = sdFbm2(wp.xz * freq, oct, 2.0, 0.5);
    float sideX = sdFbm2(wp.zy * freq, oct, 2.0, 0.5);
    float sideZ = sdFbm2(wp.xy * freq, oct, 2.0, 0.5);
    float side  = 0.5 * (sideX + sideZ);
    return lerp(top, side, slope01);
}

// ── 有限差分による法線摂動 ─────────────────────────────────────────────────
//  N: マクロ法線（world）。strength: バンプ強度。eps: ワールド単位の差分幅。
float3 sdPerturbNormal(float3 wp, float3 N, float slope01,
                       float strength, float eps, float freq, int oct)
{
    float h  = sdSurfaceHeight(wp,                          slope01, freq, oct);
    float hx = sdSurfaceHeight(wp + float3(eps, 0.0, 0.0),  slope01, freq, oct);
    float hz = sdSurfaceHeight(wp + float3(0.0, 0.0, eps),  slope01, freq, oct);
    float3 grad = float3(hx - h, 0.0, hz - h) / eps;
    grad -= N * dot(grad, N);          // 接平面成分のみ
    return normalize(N - grad * strength);
}

// ── 岩の層理色（sedimentary strata）────────────────────────────────────────
//  ワールド Y に沿った帯 + fBm 揺らぎで堆積岩のような縞を生成。
float3 sdRockStrata(float3 wp, float3 baseRock, float strataFreq, float strataContrast)
{
    float warp  = sdFbm2(wp.xz * 0.05, 3, 2.0, 0.5) * 6.0;
    float bands = frac((wp.y + warp) * strataFreq);
    bands = abs(bands * 2.0 - 1.0);                 // 0..1 三角波
    float strata = lerp(0.78, 1.12, smoothstep(0.0, 1.0, bands));
    float grit   = lerp(0.85, 1.1, sdValueNoise2(wp.xz * 0.6));
    float3 col = baseRock * lerp(1.0, strata * grit, strataContrast);
    return col;
}

#endif // SANDBOX_TERRAIN_SURFACE_DETAIL_INCLUDED
