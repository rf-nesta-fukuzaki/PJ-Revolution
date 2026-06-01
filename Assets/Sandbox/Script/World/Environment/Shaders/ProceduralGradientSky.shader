Shader "Sandbox/ProceduralGradientSky"
{
    // URP 用のプロシージャル空。Skybox/ として割当てる。
    // - _SkyZenith / _SkyHorizon / _SkyGround は AtmosphericProfileController が時刻で動的設定する Shader Global
    // - 太陽方向 _SkySunDir も Atmospheric から流れる
    // - 単一 Pass・depth/light なし
    Properties
    {
        // すべて global 受信なのでプロパティは UI 用ダミー
        [Header(Global Inputs)]
        [PerRendererData] _Dummy ("Dummy (do not edit)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Skybox"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Shader Global（AtmosphericProfileController が SetGlobal*）
            float4 _SkyZenith;
            float4 _SkyHorizon;
            float4 _SkyGround;
            float4 _SkySunDir;     // xyz = sun dir (world, pointing toward sun)
            float4 _SkySunColor;
            float  _SkySunSize;

            // ── 雲（手続き fBm）───────────────────────────────────────
            // すべてリテラル既定値。C# 配線不要。必要なら global 化して時刻連動可能。
            // NOTE: 値は数学的に妥当だが、本 MCP セッションではシェーダー再コンパイルが
            // 効かず目視検証できていない。通常の Unity 再インポート時に有効化される。
            static const float CLOUD_SCALE    = 12.0;   // 大きいほど雲が細かく空全体に分布（低いと画面より大きな1塊になり一様化）
            static const float CLOUD_COVERAGE = 0.44;   // しきい値（fBm 平均~0.48。高いほど晴れ）
            static const float CLOUD_SOFT     = 0.16;   // 縁のソフトさ
            static const float CLOUD_OPACITY  = 0.85;
            static const float CLOUD_SPEED    = 0.004;  // 風で流れる速さ

            float sdHash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }
            float sdVNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = sdHash21(i);
                float b = sdHash21(i + float2(1, 0));
                float c = sdHash21(i + float2(0, 1));
                float d = sdHash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float sdFbmSky(float2 p)
            {
                float s = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 5; k++) { s += a * sdVNoise(p); p = p * 2.02 + 11.3; a *= 0.5; }
                return s;
            }

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionHCS : SV_POSITION; float3 dirWS : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                // Skybox 用の方向: object 座標を viewport の方向に
                VertexPositionInputs vp = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionHCS = vp.positionCS;
                o.dirWS = normalize(mul((float3x3)unity_ObjectToWorld, i.positionOS.xyz));
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dirWS);
                float h = d.y; // -1..+1
                // 地面（下半球）: ground -> horizon
                // 空（上半球）  : horizon -> zenith
                float3 col;
                if (h >= 0)
                {
                    // 上半球: 0 で horizon, 1 で zenith。pow で水平線寄せ
                    float t = pow(saturate(h), 0.55);
                    col = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, t);
                }
                else
                {
                    float t = pow(saturate(-h), 0.55);
                    col = lerp(_SkyHorizon.rgb, _SkyGround.rgb, t);
                }

                // Sun disk + soft glow
                float3 sunDir = normalize(_SkySunDir.xyz);
                float ang = dot(d, sunDir); // 1 が一致
                float ss = max(_SkySunSize, 1e-4);
                float disk  = smoothstep(1.0 - ss * 0.5, 1.0 - ss * 0.05, ang);
                float glow  = pow(saturate(ang), 16.0) * 0.35;
                col += _SkySunColor.rgb * (disk + glow);

                // ── 雲のオーバーレイ ──────────────────────────────────
                // 視線を水平面に投影して雲座標へ（地平付近のゼロ割回避に d.y を下限クランプ）。
                // 天頂で潰れ縞になりにくい投影（d.y で割る量を 0.5〜1.0 に緩和）。
                float2 cuv  = d.xz * CLOUD_SCALE / (d.y * 0.5 + 0.5);
                float2 wind = normalize(float2(1.0, 0.6)) * (_Time.y * CLOUD_SPEED);
                float shape = sdFbmSky(cuv + wind);
                float cover = smoothstep(CLOUD_COVERAGE, CLOUD_COVERAGE + CLOUD_SOFT, shape);
                float horizonFade = smoothstep(0.0, 0.14, d.y);    // 地平近くまで雲を出す
                float cloudAmt = saturate(cover) * horizonFade * CLOUD_OPACITY;

                // 雲の陰影：高周波 density で明暗、太陽側を縁から発光させ立体感を出す。
                float dens = sdFbmSky(cuv * 2.3 + wind * 1.7 + 7.0);
                float3 cloudLit  = float3(1.03, 1.03, 1.02);          // 明るい白（>1 で bloom 映え）
                float3 cloudDark = float3(0.60, 0.65, 0.74);          // 影側のグレーブルー
                float3 cloudCol  = lerp(cloudDark, cloudLit, saturate(dens * 1.4));
                cloudCol += _SkySunColor.rgb * pow(saturate(ang), 5.0) * 0.7;
                col = lerp(col, cloudCol, cloudAmt);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
