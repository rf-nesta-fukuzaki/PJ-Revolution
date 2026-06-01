Shader "Sandbox/PlacementInstancedIndirect"
{
    // DrawMeshInstancedIndirect 用。StructuredBuffer<PlacementInstance> から
    // unity_InstanceID で TRS を構築（procedural instancing）。
    //  - URP PBR（UniversalFragmentPBR）= 主光源/追加光源/影/GI(SH)/反射/フォグ
    //  - 風揺れ（_IsFoliage=1 の植生のみ、頂点高さに比例）
    //  - 葉の透過光（サブサーフェス: 逆光で葉が光る）
    //  - インスタンス毎の色ゆらぎ（位置ハッシュ）+ 高さ方向 AO グラデーション
    //  - 個別距離カリング（_CullDistance 超で退化）+ ディザ距離フェード
    Properties
    {
        _BaseColor    ("Base Color", Color) = (1,1,1,1)
        _TopColor     ("Top/Tip Color", Color) = (1,1,1,1)
        _IsFoliage    ("Is Foliage (0/1)", Float) = 0
        _MeshHeight   ("Mesh Local Height", Float) = 2
        _MeshBaseY    ("Mesh Local Base Y", Float) = 0
        _Smoothness   ("Smoothness", Range(0,1)) = 0.1
        _Translucency ("Translucency", Range(0,2)) = 0.8
        _ColorVariation ("Per-Instance Variation", Range(0,1)) = 0.25
        _AOStrength   ("Vertical AO", Range(0,1)) = 0.6
        _WindStrength ("Wind Strength", Float) = 0.25
        _WindFreq     ("Wind Frequency", Float) = 1.4
        _CullDistance ("Cull Distance", Float) = 300
        _FadeStart    ("Fade Start", Float) = 220
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        // ── Forward ───────────────────────────────────────────────
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TopColor;
                float  _IsFoliage;
                float  _MeshHeight;
                float  _MeshBaseY;
                float  _Smoothness;
                float  _Translucency;
                float  _ColorVariation;
                float  _AOStrength;
                float  _WindStrength;
                float  _WindFreq;
                float  _CullDistance;
                float  _FadeStart;
            CBUFFER_END

            struct PlacementInstance { float3 position; float scale; float rotationY; uint prototype; };
        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<PlacementInstance> _Instances;
        #endif

            void setup()
            {
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                PlacementInstance inst = _Instances[unity_InstanceID];
                float sc = inst.scale;
                if (distance(_WorldSpaceCameraPos, inst.position) > _CullDistance) sc = 0.0;
                float c = cos(inst.rotationY), s = sin(inst.rotationY);
                float3 p = inst.position;
                unity_ObjectToWorld = float4x4(
                    c*sc, 0.0, s*sc, p.x,  0.0, sc, 0.0, p.y,  -s*sc, 0.0, c*sc, p.z,  0.0,0.0,0.0,1.0);
                float invs = 1.0 / max(sc, 1e-4);
                unity_WorldToObject = float4x4(
                    c*invs, 0.0, -s*invs, -(c*p.x - s*p.z)*invs,
                    0.0,    invs, 0.0,    -p.y*invs,
                    s*invs, 0.0,  c*invs, -(s*p.x + c*p.z)*invs,
                    0.0,    0.0,  0.0,     1.0);
            #endif
            }

            float instHash(float3 io){ return frac(sin(dot(io.xz, float2(12.9898,78.233))) * 43758.5453); }

            float3 applyWind(float3 ws, float3 io, float yf)
            {
                float sway = _IsFoliage * _WindStrength * yf * yf;
                if (sway <= 1e-5) return ws;
                float h = instHash(io);
                float phase = io.x * 0.15 + io.z * 0.15 + h * 6.2831853;
                float t = _Time.y * _WindFreq;
                ws.x += sin(t + phase) * sway;
                ws.z += cos(t * 0.8 + phase * 1.37) * sway;
                ws.x += sin(t * 2.3 + phase * 0.5) * sway * 0.3; // flutter
                return ws;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float2 yfHash      : TEXCOORD2; // x=heightFrac y=instanceHash
                float  fogCoord    : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 io = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float yf = saturate((IN.positionOS.y - _MeshBaseY) / max(_MeshHeight, 1e-3));

                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                ws = applyWind(ws, io, yf);

                OUT.positionWS  = ws;
                OUT.positionHCS = TransformWorldToHClip(ws);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.yfHash      = float2(yf, instHash(io));
                OUT.fogCoord    = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ディザ距離フェード
                float dist = distance(_WorldSpaceCameraPos, IN.positionWS);
                float fade = saturate((_CullDistance - dist) / max(_CullDistance - _FadeStart, 1e-3));
                float dither = frac(52.9829189 * frac(dot(IN.positionHCS.xy, float2(0.06711056, 0.00583715))));
                clip(fade - dither);

                float yf   = IN.yfHash.x;
                float hash = IN.yfHash.y;

                // 高さグラデーション色 + インスタンス毎ゆらぎ
                float3 grad = lerp(_BaseColor.rgb, _TopColor.rgb, yf);
                float vary  = lerp(1.0 - _ColorVariation, 1.0 + _ColorVariation, hash);
                float ao    = lerp(1.0, 0.45 + 0.55 * yf, _AOStrength); // 根元/内部を暗く
                float3 albedo = saturate(grad * vary * ao);

                float3 N = normalize(IN.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.normalWS                = N;
                inputData.viewDirectionWS         = normalize(GetWorldSpaceViewDir(IN.positionWS));
                inputData.shadowCoord             = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord                = IN.fogCoord;
                inputData.bakedGI                 = SampleSH(N);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask              = half4(1,1,1,1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedo;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion  = ao;
                surfaceData.alpha      = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // 葉の透過光（逆光サブサーフェス）
                if (_IsFoliage > 0.5)
                {
                    Light ml = GetMainLight(inputData.shadowCoord);
                    float3 transDir = ml.direction + N * 0.4;
                    float  trans = pow(saturate(dot(inputData.viewDirectionWS, -transDir)), 3.0);
                    color.rgb += albedo * ml.color * (trans * _Translucency * ml.shadowAttenuation);
                }

                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ── ShadowCaster ──────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex svert
            #pragma fragment sfrag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; float4 _TopColor;
                float _IsFoliage; float _MeshHeight; float _MeshBaseY; float _Smoothness; float _Translucency;
                float _ColorVariation; float _AOStrength; float _WindStrength; float _WindFreq;
                float _CullDistance; float _FadeStart;
            CBUFFER_END

            float3 _LightDirection;

            struct PlacementInstance { float3 position; float scale; float rotationY; uint prototype; };
        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<PlacementInstance> _Instances;
        #endif

            void setup()
            {
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                PlacementInstance inst = _Instances[unity_InstanceID];
                float sc = inst.scale;
                if (distance(_WorldSpaceCameraPos, inst.position) > _CullDistance) sc = 0.0;
                float c = cos(inst.rotationY), s = sin(inst.rotationY);
                float3 p = inst.position;
                unity_ObjectToWorld = float4x4(
                    c*sc, 0.0, s*sc, p.x,  0.0, sc, 0.0, p.y,  -s*sc, 0.0, c*sc, p.z,  0.0,0.0,0.0,1.0);
                float invs = 1.0 / max(sc, 1e-4);
                unity_WorldToObject = float4x4(
                    c*invs, 0.0, -s*invs, -(c*p.x - s*p.z)*invs,
                    0.0,    invs, 0.0,    -p.y*invs,
                    s*invs, 0.0,  c*invs, -(s*p.x + c*p.z)*invs,
                    0.0,    0.0,  0.0,     1.0);
            #endif
            }

            float3 applyWindS(float3 ws, float3 io, float yf)
            {
                float sway = _IsFoliage * _WindStrength * yf * yf;
                if (sway <= 1e-5) return ws;
                float h = frac(sin(dot(io.xz, float2(12.9898,78.233))) * 43758.5453);
                float phase = io.x * 0.15 + io.z * 0.15 + h * 6.2831853;
                float t = _Time.y * _WindFreq;
                ws.x += sin(t + phase) * sway;
                ws.z += cos(t * 0.8 + phase * 1.37) * sway;
                return ws;
            }

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionHCS : SV_POSITION; };

            V svert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                float3 io = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float yf = saturate((i.positionOS.y - _MeshBaseY) / max(_MeshHeight, 1e-3));
                float3 ws  = TransformObjectToWorld(i.positionOS.xyz);
                ws = applyWindS(ws, io, yf);
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
    }
}
