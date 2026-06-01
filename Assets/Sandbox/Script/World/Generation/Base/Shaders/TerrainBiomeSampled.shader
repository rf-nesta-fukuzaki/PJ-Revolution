Shader "Sandbox/TerrainBiomeSampled"
{
    // AAA 級オープンワールド地形シェーダー（外部テクスチャ不要・完全手続き的）。
    //  - ベース色: BiomeColorTex（チャンク毎 MPB）+ 標高帯 overlay（shader global）
    //  - 表層ディテール: ワールド座標の fBm/層理で岩・草・雪を高周波変調（タイリング無し）
    //  - 法線: NormalSlopeTex(.xyz=world normal) を per-pixel サンプル + 有限差分マイクロバンプ
    //  - ライティング: URP PBR（UniversalFragmentPBR）= 主光源/追加光源/影/GI(SH)/反射/フォグ
    //  - 斜度で岩、標高で雪、谷でキャビティ AO を手続き的にブレンド
    Properties
    {
        _BiomeColorTex ("Biome Color", 2D) = "white" {}
        _NormalSlopeTex ("Normal/Slope", 2D) = "bump" {}
        _UVScale ("UV Scale", Float) = 1
        _UVOffset ("UV Offset", Float) = 0

        [Header(Surface Detail)]
        _DetailFreq    ("Detail Freq (1/m)",      Float) = 0.18
        _DetailStrength("Detail Normal Strength", Range(0,4)) = 1.7
        _DetailFade    ("Detail Fade Distance",   Float) = 300
        _AlbedoFade    ("Albedo Detail Fade",     Float) = 750
        _MacroFreq     ("Macro Variation Freq",   Float) = 0.012
        _MacroStrength ("Macro Variation",        Range(0,1)) = 0.38
        _GrassDryTint  ("Grass Dry Tint", Color) = (0.92, 1.08, 0.80, 1)

        [Header(Rock)]
        _RockColor     ("Rock Color", Color) = (0.48, 0.47, 0.43, 1)
        _StrataFreq    ("Strata Freq (1/m)", Float) = 0.09
        _StrataContrast("Strata Contrast",   Range(0,1)) = 0.5
        _SlopeRockStart("Slope Rock Start (deg)", Float) = 40
        _SlopeRockFull ("Slope Rock Full (deg)",  Float) = 60

        [Header(Snow)]
        _SnowColor     ("Snow Color", Color) = (0.94, 0.96, 1.0, 1)
        _SnowSparkle   ("Snow Sparkle", Range(0,1)) = 0.6
        _SnowSlopeMax  ("Snow Max Slope (deg)", Float) = 42

        [Header(PBR)]
        _RockSmooth ("Rock Smoothness",  Range(0,1)) = 0.13
        _GrassSmooth("Grass Smoothness", Range(0,1)) = 0.18
        _SnowSmooth ("Snow Smoothness",  Range(0,1)) = 0.54
        _AOStrength ("Cavity AO",        Range(0,1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TerrainSurfaceDetail.hlsl"

            TEXTURE2D(_BiomeColorTex);
            SAMPLER(sampler_BiomeColorTex);
            TEXTURE2D(_NormalSlopeTex);
            SAMPLER(sampler_NormalSlopeTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BiomeColorTex_ST;
                float  _UVScale;
                float  _UVOffset;
                float  _DetailFreq;
                float  _DetailStrength;
                float  _DetailFade;
                float  _AlbedoFade;
                float  _MacroFreq;
                float  _MacroStrength;
                float4 _GrassDryTint;
                float4 _RockColor;
                float  _StrataFreq;
                float  _StrataContrast;
                float  _SlopeRockStart;
                float  _SlopeRockFull;
                float4 _SnowColor;
                float  _SnowSparkle;
                float  _SnowSlopeMax;
                float  _RockSmooth;
                float  _GrassSmooth;
                float  _SnowSmooth;
                float  _AOStrength;
            CBUFFER_END

            // 標高帯 overlay（AtmosphericProfileController が Shader.SetGlobal で動的設定）
            float  _ShoreLine;  float _ShoreBlend;
            float  _GrassLine;  float _GrassBlend;
            float  _RockLine;   float _RockBlend;
            float  _SnowLine;   float _SnowBlend;
            float4 _ColShore;
            float4 _ColGrass;
            float4 _ColRock;
            float4 _ColSnow;
            float  _BandStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv * _UVScale + _UVOffset;
                OUT.fogCoord    = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 wp = IN.positionWS;

                // ── マクロ法線（per-pixel）。NormalSlopeTex.xyz=world normal、未バインド時はメッシュ法線 ──
                float3 texN = SAMPLE_TEXTURE2D(_NormalSlopeTex, sampler_NormalSlopeTex, IN.uv).xyz;
                float3 macroN = (dot(texN, texN) > 1e-4) ? normalize(texN) : normalize(IN.normalWS);

                float slopeDeg = degrees(acos(saturate(macroN.y)));
                float slope01  = saturate(slopeDeg / 90.0);

                // ── カメラ距離でディテールをフェード ──
                //  detailAmt: 高周波の法線バンプ用（近距離のみ。遠景のエイリアス抑制）
                //  albedoAmt: 色ディテール用（より遠くまで保持し、遠景でも岩肌/草むらが見える）
                float camDist   = distance(wp, _WorldSpaceCameraPos);
                float detailAmt = saturate(1.0 - camDist / max(1.0, _DetailFade));
                float albedoAmt = saturate(1.0 - camDist / max(1.0, _AlbedoFade));

                // ── 法線マイクロバンプ（有限差分）──
                float3 N = macroN;
                if (detailAmt > 0.001)
                {
                    float eps = max(0.25, camDist * 0.004);
                    float3 bumped = sdPerturbNormal(wp, macroN, slope01,
                                        _DetailStrength * detailAmt, eps, _DetailFreq, 4);
                    N = bumped;
                }

                // ── ベース色: biome + 標高帯 overlay ──
                float3 biomeCol = SAMPLE_TEXTURE2D(_BiomeColorTex, sampler_BiomeColorTex, IN.uv).rgb;
                float y = wp.y;
                float wSnowB  = smoothstep(_SnowLine  - _SnowBlend,  _SnowLine  + _SnowBlend,  y);
                float wRockB  = smoothstep(_RockLine  - _RockBlend,  _RockLine  + _RockBlend,  y) * (1.0 - wSnowB);
                float wGrassB = smoothstep(_GrassLine - _GrassBlend, _GrassLine + _GrassBlend, y) * (1.0 - wRockB - wSnowB);
                float wShoreB = saturate(1.0 - wSnowB - wRockB - wGrassB);
                float3 bandCol = _ColSnow.rgb*wSnowB + _ColRock.rgb*wRockB + _ColGrass.rgb*wGrassB + _ColShore.rgb*wShoreB;
                float3 albedo = lerp(biomeCol, bandCol, saturate(_BandStrength));

                // ── マクロ変動（大きなまだら）でフラット感を解消 ──
                float macro = sdFbm2(wp.xz * _MacroFreq, 4, 2.0, 0.5);
                albedo *= lerp(1.0 - _MacroStrength, 1.0 + _MacroStrength, macro);

                float rockW = smoothstep(_SlopeRockStart, _SlopeRockFull, slopeDeg);

                // ── 草地のパッチ（クランプ状の濃淡 + 乾いた草の斑）──
                float grassBand = saturate((1.0 - wSnowB) * (1.0 - wRockB) * (1.0 - rockW)
                                  * smoothstep(_ShoreLine, _GrassLine + _GrassBlend, y));
                if (grassBand > 0.01 && albedoAmt > 0.001)
                {
                    float patch = sdFbm2(wp.xz * 0.085, 4, 2.0, 0.5);
                    float blade = sdValueNoise2(wp.xz * 0.6);
                    float3 g = albedo * lerp(0.74, 1.20, patch);
                    g = lerp(g, g * _GrassDryTint.rgb, saturate(blade - 0.55) * 0.7);
                    albedo = lerp(albedo, g, grassBand * albedoAmt * 0.85);
                }

                // ── 岩肌を斜面に乗せる（距離非依存 → 崖/稜線が遠景でも岩として読める）──
                float3 rockFlat   = _RockColor.rgb * lerp(1.0 - _MacroStrength, 1.0 + _MacroStrength, macro);
                float3 rockStrata = sdRockStrata(wp, _RockColor.rgb, _StrataFreq, _StrataContrast);
                float3 rockCol    = lerp(rockFlat, rockStrata, albedoAmt);
                albedo = lerp(albedo, rockCol, rockW * 0.88);

                // ── 雪: 標高帯の上 + 緩斜面のみ堆積。風紋(サスツルギ)・凹みの青み・きらめき ──
                float snowAlt   = wSnowB;
                float snowSlope = 1.0 - smoothstep(_SnowSlopeMax - 6.0, _SnowSlopeMax + 6.0, slopeDeg);
                float snowW     = saturate(snowAlt * snowSlope);
                float smoothness = lerp(_GrassSmooth, _RockSmooth, rockW);
                if (snowW > 0.001)
                {
                    // 風向に伸びた縞（横方向に大きく伸長）で雪原に陰影を作る
                    float2 windDir = normalize(float2(1.0, 0.35));
                    float2 sp = float2(dot(wp.xz, windDir), dot(wp.xz, float2(-windDir.y, windDir.x)));
                    float ripple = sdFbm2(float2(sp.x * 0.07, sp.y * 0.45), 4, 2.0, 0.5);
                    float3 snowCol = _SnowColor.rgb * lerp(0.84, 1.07, ripple);
                    // 凹み（暗い縞）にわずかな青を差して立体感
                    snowCol = lerp(snowCol * float3(0.93, 0.96, 1.05), snowCol, smoothstep(0.2, 0.6, ripple));
                    // きらめき（高周波の白点）
                    float spk = sdValueNoise2(wp.xz * 11.0);
                    snowCol *= 1.0 + 0.55 * step(0.94, spk) * _SnowSparkle * detailAmt;
                    albedo  = lerp(albedo, snowCol, snowW);
                    smoothness = lerp(smoothness, _SnowSmooth, snowW);
                }

                // ── キャビティ AO（高周波の谷を暗く）──
                float cav = sdFbm2(wp.xz * (_DetailFreq * 0.5), 3, 2.0, 0.5);
                float ao  = lerp(1.0, saturate(0.55 + cav), _AOStrength * detailAmt);

                // ── URP PBR ライティング ──
                InputData inputData = (InputData)0;
                inputData.positionWS              = wp;
                inputData.normalWS                = N;
                inputData.viewDirectionWS         = normalize(GetWorldSpaceViewDir(wp));
                inputData.shadowCoord             = TransformWorldToShadowCoord(wp);
                inputData.fogCoord                = IN.fogCoord;
                inputData.vertexLighting          = half3(0,0,0);
                inputData.bakedGI                 = SampleSH(N);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask              = half4(1,1,1,1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = saturate(albedo);
                surfaceData.metallic   = 0.0;
                surfaceData.specular   = half3(0,0,0);
                surfaceData.smoothness = saturate(smoothness);
                surfaceData.occlusion  = ao;
                surfaceData.emission    = half3(0,0,0);
                surfaceData.alpha      = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex svert
            #pragma fragment sfrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionHCS : SV_POSITION; };

            V svert(A i)
            {
                V o;
                float3 ws  = TransformObjectToWorld(i.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(i.normalOS);
                float4 pos = TransformWorldToHClip(ApplyShadowBias(ws, nws, _LightDirection));
            #if UNITY_REVERSED_Z
                pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
            #else
                pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionHCS = pos;
                return o;
            }

            half4 sfrag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex dvert
            #pragma fragment dfrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionHCS : SV_POSITION; };

            V dvert(A i)
            {
                V o;
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }
            half4 dfrag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
