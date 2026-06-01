Shader "Sandbox/CloudSeaSoft"
{
    // 中腹高度に水平に置く半透明 plane 用シェーダー。
    // - 中心からの距離で alpha フォールオフ（円形ぼかし）
    // - 簡易 procedural noise + Time.y で緩慢にスクロール
    // - URP/Unlit 系・depth write 無効・depth test on で「立ち込めた雲」表現
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,0.55)
        _RadiusFalloff ("Radius Falloff", Float) = 0.45
        _NoiseScale ("Noise Scale", Float) = 0.4
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.55
        _ScrollSpeed ("Scroll Speed", Float) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _RadiusFalloff;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _ScrollSpeed;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 wPos : TEXCOORD1; };

            // ハッシュ + value noise
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
                o.wPos = vp.positionWS;
                o.uv = i.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                // 中心からの距離で円形フォールオフ（quad 中心 0.5,0.5）
                float2 d = i.uv - 0.5;
                float r = length(d) * 2.0; // 0..1 中心→端
                float fall = 1.0 - smoothstep(_RadiusFalloff, 1.0, r);

                // 緩慢にスクロールする 2 オクターブ noise
                float2 p = i.uv * 6.0 * _NoiseScale + _Time.y * _ScrollSpeed * float2(1.0, -0.3);
                float n = Noise(p) * 0.6 + Noise(p * 2.13) * 0.4;
                float cloud = saturate(lerp(1.0 - _NoiseStrength, 1.0, n));

                float a = _BaseColor.a * fall * cloud;
                return half4(_BaseColor.rgb, a);
            }
            ENDHLSL
        }
    }
}
