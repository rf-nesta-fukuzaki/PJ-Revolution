Shader "Custom/IrisTransition"
{
    // 画面遷移用アイリス（円形ワイプ）シェーダー。
    // ScreenSpaceOverlay Canvas 上の全画面 RawImage に貼って使う想定。
    //   _Radius     : 開き具合。0 で全画面 _Color（閉）、GetMaxRadius 以上で全透明（開）。
    //   _Color      : 覆う色（既定は黒）。
    //   _AspectRatio: 画面アスペクト比（width/height）。横方向距離の補正に使う。
    // C# 側 (Sandbox.UI.IrisTransition) が _Radius / _Color / _AspectRatio を駆動する。
    Properties
    {
        _Color ("Iris Color", Color) = (0,0,0,1)
        _Radius ("Radius", Float) = 1
        _AspectRatio ("Aspect Ratio", Float) = 1
        _EdgeSoftness ("Edge Softness", Range(0,0.05)) = 0.005
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Overlay"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }
        LOD 100

        Pass
        {
            Name "IrisOverlay"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Radius;
                float  _AspectRatio;
                float  _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 画面中心 (0.5,0.5) からの距離。横方向はアスペクト補正して円形に保つ。
                float2 d = IN.uv - 0.5;
                d.x *= _AspectRatio;
                float dist = length(d);

                // dist < _Radius は開いた領域（透明）、外側は _Color で覆う（不透明）。
                float edge  = max(_EdgeSoftness, 1e-4);
                float cover = smoothstep(_Radius - edge, _Radius + edge, dist);

                half4 col = _Color * IN.color;
                col.a *= cover;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
