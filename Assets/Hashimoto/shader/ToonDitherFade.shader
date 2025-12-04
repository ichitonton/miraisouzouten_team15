// ============================================================================
//  URP 3諧調トゥーン + テクスチャ + 影調整 + アウトライン + ShadowCaster + Fog
//  ＋ 画面上のラジアル・ディザーフェード（丸いドット穴）
// ============================================================================

Shader "Universal Render Pipeline/Toon/ToonDitherFade"
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

        // --- ラジアル・ディザーフェード用 ---
        [Toggle(_RADIAL_DITHER_ON)] _UseRadialDither ("ラジアルディザー有効", Float) = 0
        _RadialCenter  ("ラジアル中心(スクリーンUV xy)", Vector) = (0.5, 0.5, 0, 0)
        _RadialRadius  ("ラジアル半径", Range(0,1)) = 0.35
        _RadialFeather ("境界ぼかし", Range(0,1)) = 0.15
        _DitherScale   ("ディザー密度", Float) = 700
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        // =====================================================================
        // PASS 0: 本体（トゥーン受光 + Fog + ラジアルディザー）
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
            #pragma multi_compile _ FOG_LINEAR FOG_EXP FOG_EXP2

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _RADIAL_DITHER_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;

            // ★ Properties と同じ順番で全部 float で揃える
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _Threshold1;
                float  _Threshold2;
                float  _Fade1;
                float  _Fade2;
                float  _ShadowOffset;
                float  _LightContrast;
                float  _ReceiveShadows;
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _AlphaClip;
                float  _Cutoff;
                float  _UseRadialDither;
                float4 _RadialCenter;
                float  _RadialRadius;
                float  _RadialFeather;
                float  _DitherScale;
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

            void ThreeBandWeights(float v, float T1, float T2, float F1, float F2,
                                  out float wLight, out float wMid, out float wShadow)
            {
                float tHi = max(T1, T2);
                float tLo = min(T1, T2);
                float sL = smoothstep(tHi - saturate(F1), tHi + saturate(F1), v);
                float sS = smoothstep(tLo - saturate(F2), tLo + saturate(F2), v);
                wLight  = sL;
                wShadow = 1.0 - sS;
                wMid    = saturate(1.0 - wLight - wShadow);
            }

            float3 ApplyStrength(float3 baseCol, float3 toneCol, float a)
            {
                return lerp(baseCol, toneCol, saturate(a));
            }

            float AdjustLightResponse(float ndl)
            {
                float x = saturate(ndl + _ShadowOffset);
                x = saturate(pow(max(x, 1e-4), _LightContrast));
                return x;
            }

            float ToonLightFactor(float3 posWS, float3 normalWS, float4 shadowCoord)
            {
                float NdotL = 0;
                Light mainLight = GetMainLight(shadowCoord);
                float ndlMain = saturate(dot(normalWS, mainLight.direction));
                if (_ReceiveShadows > 0.5) ndlMain *= mainLight.shadowAttenuation;
                NdotL = max(NdotL, ndlMain);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                [loop] for (uint i = 0; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, posWS);
                    float ndl = saturate(dot(normalWS, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    NdotL = max(NdotL, ndl);
                }
                #endif

                return AdjustLightResponse(saturate(NdotL));
            }

            // ディザー用疑似乱数
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
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

            float4 frag(Varyings IN) : SV_Target
            {
                // アルファカット（葉っぱテクスチャ等用）
                #ifdef _ALPHATEST_ON
                float alphaSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                clip(alphaSample - _Cutoff);
                #endif

                // --- ラジアル・ディザーフェード（丸い穴） ---
                #ifdef _RADIAL_DITHER_ON
                {
                    float2 center = _RadialCenter.xy;

                    // センターが画面内 & 半径が有効なときだけディザー処理
                    if (center.x >= 0.0 && center.x <= 1.0 &&
                        center.y >= 0.0 && center.y <= 1.0 &&
                        _RadialRadius > 0.0001)
                    {
                        // クリップ空間 → スクリーンUV(0〜1)
                        float2 screenUV = IN.positionHCS.xy / IN.positionHCS.w;
                        screenUV = screenUV * 0.5f + 0.5f;

                        float dist = distance(screenUV, center);

                        // 内側=0, 外側=1
                        float fade  = smoothstep(_RadialRadius,
                                                 _RadialRadius + _RadialFeather,
                                                 dist);
                        float noise = Hash21(screenUV * _DitherScale);

                        // 外側ほど抜けやすい
                        if (noise < fade)
                        {
                            clip(-1);    // 穴あけ：後ろが見える
                        }
                    }
                }
                #endif
                // --- ここまで ---

                // 通常トゥーン
                float3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                float3 nWS = normalize(IN.normalWS);
                float  v   = ToonLightFactor(IN.posWS, nWS, IN.shadowCoord);

                float wL, wM, wS;
                ThreeBandWeights(v, _Threshold1, _Threshold2, _Fade1, _Fade2, wL, wM, wS);

                float3 baseTone   = _BaseColor.rgb      * tex;
                float3 midTone    = ApplyStrength(baseTone, _MidColor.rgb    * tex, _MidColor.a);
                float3 shadowTone = ApplyStrength(baseTone, _ShadowColor.rgb * tex, _ShadowColor.a);

                float3 col = wL * baseTone + wM * midTone + wS * shadowTone;
                col = MixFog(col, IN.fogCoord);

                return float4(col, 1);
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
            #pragma multi_compile _ FOG_LINEAR FOG_EXP FOG_EXP2
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float  _Cutoff;
            ENDHLSL
        }

        // =====================================================================
        // PASS 2: アウトライン（ディザー無し）
        // =====================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForwardOnly" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vertOL
            #pragma fragment fragOL
            #pragma multi_compile _ FOG_LINEAR FOG_EXP FOG_EXP2

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
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
                float3 rgb = _OutlineColor.rgb;
                rgb = MixFog(rgb, IN.fogCoord);
                return float4(rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
