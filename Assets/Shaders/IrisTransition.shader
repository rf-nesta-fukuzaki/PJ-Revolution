Shader "Custom/IrisTransition"
{
    Properties
    {
        _Radius      ("Radius",       Float)  = 1.0
        _Color       ("Color",        Color)  = (0, 0, 0, 1)
        _AspectRatio ("Aspect Ratio", Float)  = 1.778
        _EdgeSoftness("Edge Softness",Float)  = 0.01
    }
    SubShader
    {
        Tags
        {
            "Queue"          = "Overlay"
            "RenderType"     = "Transparent"
            "IgnoreProjector"= "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest  Always
        Cull   Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            float  _Radius;
            fixed4 _Color;
            float  _AspectRatio;
            float  _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV 中心を (0.5, 0.5) に合わせ、アスペクト比で X を補正
                float2 c = i.uv - 0.5;
                c.x *= _AspectRatio;
                float dist = length(c);

                // dist > _Radius → 黒マスク (1.0)
                // dist < _Radius → 透明    (0.0)
                float mask = smoothstep(_Radius - _EdgeSoftness,
                                        _Radius + _EdgeSoftness,
                                        dist);

                return fixed4(_Color.rgb, mask * _Color.a);
            }
            ENDCG
        }
    }
}
