Shader "Sandbox/WaterSurface"
{
    // 島を取り囲む海面用のスタイライズド水面シェーダー（URP / Transparent）。
    // - シーン深度(_CameraDepthTexture)で「浅瀬→深場」の色グラデーションと岸の泡(foam)を生成
    // - ワールドXZのノイズ勾配で波法線を作り、メインライトの鏡面反射でキラめきを出す
    // - フレネルで水平線方向を空色に持ち上げる
    // Depth Texture が無効な URP でも破綻しない（深度差が取れない場合は深場色になるだけ）。
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.20, 0.55, 0.60, 0.55)
        _DeepColor    ("Deep Color",    Color) = (0.02, 0.12, 0.25, 0.95)
        _DepthMax     ("Depth Fade [m]", Float) = 14.0

        _FoamColor      ("Foam Color", Color) = (0.92, 0.96, 1.0, 1.0)
        _FoamDistance   ("Foam Distance [m]", Float) = 3.0
        _FoamNoiseScale ("Foam Noise Scale", Float) = 0.25

        _WaveScale    ("Wave Scale", Float) = 0.08
        _WaveSpeed    ("Wave Speed", Float) = 0.6
        _WaveStrength ("Wave Strength", Float) = 0.6

        _FresnelColor ("Fresnel Color", Color) = (0.60, 0.80, 0.95, 0.5)
        _FresnelPower ("Fresnel Power", Float) = 4.0

        _SpecPower     ("Specular Power", Float) = 200.0
        _SpecIntensity ("Specular Intensity", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 200
        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthMax;
                float4 _FoamColor;
                float  _FoamDistance;
                float  _FoamNoiseScale;
                float  _WaveScale;
                float  _WaveSpeed;
                float  _WaveStrength;
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _SpecPower;
                float  _SpecIntensity;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V {
                float4 positionHCS : SV_POSITION;
                float3 wPos        : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            V vert(A i)
            {
                V o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionHCS = vp.positionCS;
                o.wPos        = vp.positionWS;
                o.screenPos   = vp.positionNDC;
                o.fogFactor   = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 1e-5);

                // シーン深度（不透明描画の深度）と水面フラグメントの視点距離から「水深」を求める。
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragEye  = i.screenPos.w;
                float waterDepth = max(0.0, sceneEye - fragEye);

                // ── 波法線（ワールドXZのノイズ勾配・2方向スクロール） ──
                float2 w = i.wPos.xz;
                float t = _Time.y * _WaveSpeed;
                float2 uv1 = w * _WaveScale + float2(t, t * 0.6);
                float2 uv2 = w * _WaveScale * 1.7 - float2(t * 0.8, t * 0.3);
                float e = 0.75;
                float hL = Noise(uv1 - float2(e, 0)) + Noise(uv2 - float2(e, 0));
                float hR = Noise(uv1 + float2(e, 0)) + Noise(uv2 + float2(e, 0));
                float hD = Noise(uv1 - float2(0, e)) + Noise(uv2 - float2(0, e));
                float hU = Noise(uv1 + float2(0, e)) + Noise(uv2 + float2(0, e));
                float3 n = normalize(float3((hL - hR) * _WaveStrength, 1.0, (hD - hU) * _WaveStrength));

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.wPos));

                // ── 水深による色グラデーション ──
                float d01 = saturate(waterDepth / max(_DepthMax, 0.01));
                float3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, d01);

                // ── フレネル（水平線方向を空色へ） ──
                float fres = pow(1.0 - saturate(dot(viewDir, n)), _FresnelPower);
                col = lerp(col, _FresnelColor.rgb, fres * _FresnelColor.a);

                // ── メインライト鏡面反射 ──
                Light mainLight = GetMainLight();
                float3 hv = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(n, hv)), _SpecPower) * _SpecIntensity;
                col += mainLight.color * spec;

                // ── 岸の泡（水深が浅い帯にノイズで縁取り） ──
                float foamEdge = 1.0 - saturate(waterDepth / max(_FoamDistance, 0.01));
                float foamN = Noise(w * _FoamNoiseScale + t);
                float foam = smoothstep(0.45, 0.95, foamEdge * (0.55 + 0.7 * foamN));
                col = lerp(col, _FoamColor.rgb, foam);

                // ── アルファ（浅瀬ほど透過。泡は不透明） ──
                float alpha = lerp(_ShallowColor.a, _DeepColor.a, d01);
                alpha = saturate(max(alpha, foam));

                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
