// ============================================================================
//  URP Ground Toon Drop Shadow (受け側) - 修正版（Cascade/Screen-space対応）
// ============================================================================
Shader "URP/Ground/ToonDropShadow"
{
    Properties
    {
        _BaseMap    ("ベーステクスチャ", 2D) = "white" {}
        _BaseColor  ("ベースカラー", Color) = (1,1,1,1)

        _ShadowColor("影色（乗算）", Color) = (0,0,0,0.85)
        _ShadowThreshold("影の閾値（小さい=影広い）", Range(0,1)) = 0.35
        _ShadowFeather  ("縁フェード幅", Range(0,0.3)) = 0.06
        _ReceiveShadows ("シャドウ受け取り (0/1)", Float) = 1

        _Ambient     ("環境光(0..1)", Range(0,1)) = 0.35
        _DiffuseGain ("拡散強度(0..2)", Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardToonDropShadow"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // ===== 影バリアント =====
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN   // ★追加
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // どの方式でも true になるマクロ
            #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                #define HAS_MAIN_SHADOWS 1
            #else
                #define HAS_MAIN_SHADOWS 0
            #endif

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half  _ShadowThreshold;
                half  _ShadowFeather;
                half  _ReceiveShadows;
                half  _Ambient;
                half  _DiffuseGain;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 tangentOS  : TANGENT;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 posWS       : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vn = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = vp.positionCS;
                OUT.posWS       = vp.positionWS;
                OUT.normalWS    = normalize(vn.normalWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);

                #if HAS_MAIN_SHADOWS
                    OUT.shadowCoord = GetShadowCoord(vp);
                #else
                    OUT.shadowCoord = 0;
                #endif
                return OUT;
            }

            half3 SimpleLambert(float3 nWS, float3 posWS, float4 shadowCoord)
            {
                Light ml = GetMainLight(shadowCoord);
                half ndl = saturate(dot(nWS, ml.direction));
                half diff = _Ambient + _DiffuseGain * ndl;
                return diff * ml.color.rgb;
            }

            // 1=明 / 0=影 の二値（フェザー付き）
            half ToonShadowMask(float4 shadowCoord)
            {
                half s = 1.0h;
                #if HAS_MAIN_SHADOWS
                    Light ml = GetMainLight(shadowCoord);
                    s = (_ReceiveShadows > 0.5h) ? ml.shadowAttenuation : 1.0h;
                #endif

                return smoothstep(_ShadowThreshold - _ShadowFeather,
                                  _ShadowThreshold + _ShadowFeather,
                                  s);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;

                half3 lit = albedo * SimpleLambert(normalize(IN.normalWS), IN.posWS, IN.shadowCoord);
                half mask = ToonShadowMask(IN.shadowCoord);

                half3 finalCol = lerp(_ShadowColor.rgb * lit, lit, mask);
                return half4(finalCol, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
