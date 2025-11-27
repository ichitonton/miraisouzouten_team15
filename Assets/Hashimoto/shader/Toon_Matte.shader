// ============================================================================
//  URP 3諧調トゥーン + テクスチャ + 影の面積/コントラスト調整 + アウトライン + ShadowCaster + Fog対応
//  ・Mid/Shadow の A で影の濃さ
//  ・Threshold/Fade で3帯の切替
//  ・MaskMap(R=粉 G=濡れ B=モチ)で質感制御
//  ・MochiSSSStrength でモチモチ簡易SSS
//  ・Specular(Blinn-Phong)でメタリック/濡れツヤ追加
//  Tested: Unity 6000系 / URP 14+
// ============================================================================

Shader "Universal Render Pipeline/Toon/Toon_Matte"
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

        _HighlightColor    ("ハイライトカラー（Aで混ざり具合）", Color) = (1,1,1,1)
        _HighlightThreshold("ハイライト開始（明部側）", Range(0,1)) = 0.9
        _HighlightSmooth   ("ハイライトぼかし幅",       Range(0,0.5)) = 0.03
        _HighlightStrength ("ハイライト強さ",           Range(0,2))   = 1

        // --- 追加：本物ツヤ用スペキュラ ---
        _SpecColor    ("スペキュラカラー", Color) = (1,1,1,1)
        _SpecPower    ("スペキュラの鋭さ", Range(1,256)) = 64
        _SpecStrength ("スペキュラ強さ",   Range(0,3))   = 1

        _OutlineColor ("アウトライン色", Color) = (0,0,0,1)
        _OutlineWidth ("アウトライン太さ(Obj空間)", Range(0,0.02)) = 0.003

        [Toggle(_ALPHATEST_ON)] _AlphaClip ("アルファカット有効", Float) = 0
        _Cutoff ("カットオフ閾値", Range(0,1)) = 0.5

        // --- 質感マスク ---
        _MaskMap ("質感マスク (R=粉 G=濡れ B=モチ)", 2D) = "white" {}
        _PowderHighlightReduce ("粉: ハイライト減少量", Range(0,1)) = 0.8
        _WetHighlightBoost     ("濡れ: ハイライト増加量", Range(0,2)) = 1.0
        _MochiSSSStrength      ("モチ: 影を明るくする強さ", Range(0,1)) = 0.4
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
            #pragma multi_compile _ _FOG_LINEAR _FOG_EXP _FOG_EXP2

            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            // LerpWhiteTo など共通マテリアル関数
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;

            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);
            float4 _MaskMap_ST;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _MidColor;
                half4 _ShadowColor;
                half  _Threshold1, _Threshold2;
                half  _Fade1, _Fade2;
                half  _ShadowOffset, _LightContrast;
                half  _ReceiveShadows;

                half4 _HighlightColor;
                half  _HighlightThreshold;
                half  _HighlightSmooth;
                half  _HighlightStrength;

                // スペキュラ
                half4 _SpecColor;
                half  _SpecPower;
                half  _SpecStrength;

                half4 _OutlineColor;
                half  _OutlineWidth;
                half  _Cutoff;

                half  _PowderHighlightReduce;
                half  _WetHighlightBoost;
                half  _MochiSSSStrength;
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
                float  fogCoord    : TEXCOORD4;
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

                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
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

                // 質感マスク
                float2 maskUV = TRANSFORM_TEX(IN.uv, _MaskMap);
                half3 maskSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, maskUV).rgb;
                half  maskPowder = maskSample.r; // 粉
                half  maskWet    = maskSample.g; // 濡れ
                half  maskMochi  = maskSample.b; // モチ

                half wL, wM, wS;
                ThreeBandWeights(v, _Threshold1, _Threshold2, _Fade1, _Fade2, wL, wM, wS);

                half3 baseTone   = _BaseColor.rgb      * tex;
                half3 midTone    = ApplyStrength(baseTone, _MidColor.rgb    * tex, _MidColor.a);
                half3 shadowTone = ApplyStrength(baseTone, _ShadowColor.rgb * tex, _ShadowColor.a);

                half3 col = wL * baseTone + wM * midTone + wS * shadowTone;

                // --- ハイライト（粉・濡れ反映） ---
                {
                    half t  = _HighlightThreshold;
                    half s  = saturate(_HighlightSmooth);
                    half hMask = smoothstep(t - s, t + s, v);

                    // 粉：ハイライトを減少
                    half powderFactor = 1.0h - maskPowder * _PowderHighlightReduce;
                    // 濡れ：ハイライトを増加
                    half wetFactor    = 1.0h + maskWet * _WetHighlightBoost;

                    half highlightStrength = _HighlightStrength * powderFactor * wetFactor;

                    half3 highlightBase = lerp(col, _HighlightColor.rgb, _HighlightColor.a);
                    col = lerp(col, highlightBase, hMask * highlightStrength);
                }

                // --- スペキュラ（Blinn-Phong / 濡れ・メタリック） ---
                {
                    float3 viewDir = normalize(_WorldSpaceCameraPos - IN.posWS);
                
                    Light  mainLight = GetMainLight(IN.shadowCoord);
                    float3 lightDir  = mainLight.direction;
                    float3 halfDir   = normalize(viewDir + lightDir);
                
                    half nh = saturate(dot(nWS, halfDir));
                
                    float power = max((float)_SpecPower, 1.0);
                    half  spec  = (half)pow(nh, power);
                
                    // ライトの減衰・影も反映
                    spec *= mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                
                    // 粉・濡れマスクの掛け方を修正
                    half powderFactor = 1.0h - maskPowder * _PowderHighlightReduce;
                    half wetFactor    = 1.0h + maskWet    * _WetHighlightBoost;
                
                    half specMask = powderFactor * wetFactor;
                
                    spec *= specMask;
                    spec *= _SpecStrength;
                
                    col += _SpecColor.rgb * spec * _SpecColor.a;
                }


                // --- モチモチ補正（簡易SSS） ---
                {
                    half shadowFactor = 1.0h - v; // 影側で1
                    half mochiFactor  = maskMochi * _MochiSSSStrength * shadowFactor;
                    col = lerp(col, baseTone, mochiFactor);
                }

                col = MixFog(col, IN.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 1: ShadowCaster
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
            #pragma multi_compile _ _FOG_LINEAR _FOG_EXP _FOG_EXP2

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                OUT.fogCoord    = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 fragOL(Varyings IN) : SV_Target
            {
                half3 rgb = _OutlineColor.rgb;
                rgb = MixFog(rgb, IN.fogCoord);
                return half4(rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
