// ============================================================================
//  URP 3諧調トゥーン + テクスチャ + 影の面積/コントラスト調整 + アウトライン + ShadowCaster + Fog対応
//  ・Mid/Shadow の A で影の濃さ
//  ・Threshold/Fade で3帯の切替
//  ・ShadowCaster は URP 公式の Shaders/ShadowCasterPass.hlsl を使用
//  Tested: Unity 6000系 / URP 14+
// ============================================================================

Shader "Universal Render Pipeline/Toon/ThreeTone_Outline_Fog_JP"
{
    Properties
    {
        _BaseMap      ("ベーステクスチャ（アルベド）", 2D) = "white" {}
        _BaseColor    ("ベースカラー（明部）", Color) = (1,1,1,1)
        _MidColor     ("中間影カラー（Aで濃さ）",   Color) = (0.85,0.85,0.85,0.6)
        _ShadowColor  ("濃い影カラー（Aで濃さ）", Color) = (0.55,0.55,0.55,1)
        _Threshold1   ("閾値1：明→中", Range(0,1)) = 0.45
        _Threshold2   ("閾値2：中→濃", Range(0,1)) = 0.20
        _Fade1        ("フェード1",     Range(0,0.5)) = 0.03
        _Fade2        ("フェード2",     Range(0,0.5)) = 0.03
        _ShadowOffset ("影の広さオフセット", Range(-0.5,0.5)) = 0
        _LightContrast("光のメリハリ",       Range(0.25,4))   = 1
        _ReceiveShadows ("シャドウ受け取り (0/1)", Float) = 1
        _OutlineColor ("アウトライン色", Color) = (0,0,0,1)
        _OutlineWidth ("アウトライン太さ(Obj空間)", Range(0,0.02)) = 0.003
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("アルファカット有効", Float) = 0
        _Cutoff ("カットオフ閾値", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        // =====================================================================
        // PASS 0: 本体（トゥーン受光 + Fog）
        // =====================================================================
        Pass
        {
            Name "ForwardLitToon"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            // ★ Fog keywords （URP 6000 ではこれがないとバリアントが生成されない）
            #pragma multi_compile _ _FOG_LINEAR _FOG_EXP _FOG_EXP2

            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _MidColor;
                half4 _ShadowColor;
                half  _Threshold1, _Threshold2;
                half  _Fade1, _Fade2;
                half  _ShadowOffset, _LightContrast;
                half  _ReceiveShadows;
                half4 _OutlineColor;
                half  _OutlineWidth;
                half  _Cutoff;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 posWS       : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                float  fogCoord    : TEXCOORD4; // ★ Fog追加
            };

            void ThreeBandWeights(half v, half T1, half T2, half F1, half F2,
                                  out half wLight, out half wMid, out half wShadow)
            {
                half tHi = max(T1, T2);
                half tLo = min(T1, T2);
                half sL = smoothstep(tHi - saturate(F1), tHi + saturate(F1), v);
                half sS = smoothstep(tLo - saturate(F2), tLo + saturate(F2), v);
                wLight  = sL;
                wShadow = 1.0h - sS;
                wMid    = saturate(1.0h - wLight - wShadow);
            }

            half3 ApplyStrength(half3 baseCol, half3 toneCol, half a)
            {
                return lerp(baseCol, toneCol, saturate(a));
            }

            half AdjustLightResponse(half ndl)
            {
                half x = saturate(ndl + _ShadowOffset);
                x = saturate(pow(max(x, 1e-4h), _LightContrast));
                return x;
            }

            half ToonLightFactor(float3 posWS, float3 normalWS, float4 shadowCoord)
            {
                half NdotL = 0;
                Light mainLight = GetMainLight(shadowCoord);
                half ndlMain = saturate(dot(normalWS, mainLight.direction));
                if (_ReceiveShadows > 0.5h) ndlMain *= mainLight.shadowAttenuation;
                NdotL = max(NdotL, ndlMain);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                [loop] for (uint i = 0; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, posWS);
                    half ndl = saturate(dot(normalWS, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    NdotL = max(NdotL, ndl);
                }
                #endif

                return AdjustLightResponse(saturate(NdotL));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nm  = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = pos.positionCS;
                OUT.posWS       = pos.positionWS;
                OUT.normalWS    = normalize(nm.normalWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);

                #if defined(_MAIN_LIGHT_SHADOWS)
                    OUT.shadowCoord = GetShadowCoord(pos);
                #else
                    OUT.shadowCoord = 0;
                #endif

                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z); // ★ Fog追加
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                half alphaSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(alphaSample - _Cutoff);
                #endif

                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                half3 nWS = normalize(IN.normalWS);
                half  v   = ToonLightFactor(IN.posWS, nWS, IN.shadowCoord);

                half wL, wM, wS;
                ThreeBandWeights(v, _Threshold1, _Threshold2, _Fade1, _Fade2, wL, wM, wS);

                half3 baseTone   = _BaseColor.rgb      * tex;
                half3 midTone    = ApplyStrength(baseTone, _MidColor.rgb    * tex, _MidColor.a);
                half3 shadowTone = ApplyStrength(baseTone, _ShadowColor.rgb * tex, _ShadowColor.a);

                half3 col = wL * baseTone + wM * midTone + wS * shadowTone;
                col = MixFog(col, IN.fogCoord); // ★ Fog適用
                return half4(col, 1);
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 1: ShadowCaster（公式の Shaders/ShadowCasterPass を使用）
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            ENDHLSL
        }

        // =====================================================================
        // PASS 2: アウトライン（Inverted Hull + Fog）
        // =====================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vertOL
            #pragma fragment fragOL
            // ★ Fog keywords（アウトラインにも必要）
            #pragma multi_compile _ _FOG_LINEAR _FOG_EXP _FOG_EXP2
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // ★ Fog用

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half  _OutlineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float  fogCoord:TEXCOORD0; };

            Varyings vertOL(Attributes IN)
            {
                float3 posOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                VertexPositionInputs vp = GetVertexPositionInputs(posOS);
                Varyings OUT;
                OUT.positionHCS = vp.positionCS;
                OUT.fogCoord    = ComputeFogFactor(OUT.positionHCS.z); // ★ Fog追加
                return OUT;
            }

            half4 fragOL(Varyings IN) : SV_Target
            {
                half3 rgb = _OutlineColor.rgb;
                rgb = MixFog(rgb, IN.fogCoord); // ★ Fog適用
                return half4(rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
