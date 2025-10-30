// ============================================================================
//  URP 3諧調トゥーン + テクスチャ + 影の面積/コントラスト調整 + アウトライン
//  ・Mid/Shadow の「アルファ値」で影の“濃さ”を制御
//  ・Threshold1/2 と Fade1/2 で 3諧調の切り替え位置と滑らかさを制御
//  ・ShadowOffset / LightContrast で「影の広さ」と「メリハリ」を調整
//  ・Outline は Inverted Hull（法線方向に拡張）方式
//  Tested: Unity 6000系 / URP 14+
// ============================================================================

Shader "Universal Render Pipeline/Toon/ThreeTone_Outline_JP"
{
    Properties
    {
        // ================= ベース設定 =================
        _BaseMap     ("ベーステクスチャ（アルベド）", 2D) = "white" {}
        _BaseColor   ("ベースカラー（明部）", Color) = (1,1,1,1)

        // ================= 影色 =================
        _MidColor    ("中間影カラー（アルファで濃さ調整）", Color) = (0.85,0.85,0.85,0.6)
        _ShadowColor ("濃い影カラー（アルファで濃さ調整）", Color) = (0.55,0.55,0.55,1)

        // ================= 帯の切り替え =================
        _Threshold1  ("閾値1：明部→中間影（小さい=暗い領域広い）", Range(0,1)) = 0.45
        _Threshold2  ("閾値2：中間影→濃影（小さい=暗い領域広い）", Range(0,1)) = 0.20
        _Fade1       ("フェード1：閾値1の境界を滑らかにする", Range(0,0.5)) = 0.03
        _Fade2       ("フェード2：閾値2の境界を滑らかにする", Range(0,0.5)) = 0.03

        // ================= 光の当たり方 =================
        _ShadowOffset("影の広さオフセット（-で影広い、+で明広い）", Range(-0.5,0.5)) = 0
        _LightContrast("光のメリハリ（>1=カリッ、<1=ふんわり）", Range(0.25,4)) = 1

        // ================= その他 =================
        _ReceiveShadows ("シャドウを受ける (0=無効,1=有効)", Float) = 1

        // ================= アウトライン =================
        _OutlineColor ("アウトラインの色", Color) = (0,0,0,1)
        _OutlineWidth ("アウトラインの太さ（オブジェクト空間）", Range(0,0.1)) = 0.003
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        // =====================================================================
        // PASS 0: 本体（トゥーン受光）
        // =====================================================================
        Pass
        {
            Name "ForwardLitToon"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // ----- URP ライティングのバリアント -----
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            // ----- 必要なライブラリ -----
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ----- テクスチャ（元アルベド） -----
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST; // Tiling/Offset

            // ----- マテリアル定数（SRP Batcher対応） -----
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _MidColor;          // A=影の強さ（ベース→影色の寄り）
                half4 _ShadowColor;       // A=影の強さ（ベース→影色の寄り）
                half  _Threshold1, _Threshold2;
                half  _Fade1, _Fade2;
                half  _ShadowOffset, _LightContrast;
                half  _ReceiveShadows;
                half4 _OutlineColor;      // Outline 用（pass0では未使用でも保持OK）
                half  _OutlineWidth;      // Outline 用（同上）
            CBUFFER_END

            // ----- 頂点/補間構造体 -----
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
            };

            // ----- 頂点シェーダ：座標/法線/UV/シャドウ座標を準備 -----
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
                return OUT;
            }

            // ----- 3帯の重みを計算：Threshold と Fade で調整 -----
            void ThreeBandWeights(half v, half T1, half T2, half F1, half F2,
                                  out half wLight, out half wMid, out half wShadow)
            {
                // s1: 明↔中 の補間, s2: 中↔暗 の補間
                half s1 = smoothstep(T1 - F1, T1 + F1, v);
                half s2 = smoothstep(T2 - F2, T2 + F2, v);

                wShadow = 1.0h - s1;          // v が小さい（暗い）ほど影帯
                wMid    = s1 * (1.0h - s2);   // 中間帯
                wLight  = s2;                 // v が大きい（明るい）ほど明帯
            }

            // ----- 影色のアルファで“ベース→影色”の寄りを決める -----
            half3 ApplyStrength(half3 baseCol, half3 toneCol, half a)
            {
                return lerp(baseCol, toneCol, saturate(a));
            }

            // ----- NdotL をアーティスティックに再マッピング -----
            half AdjustLightResponse(half ndl)
            {
                // 1) 影/明の面積をシフト（負=影広い / 正=明広い）
                half x = saturate(ndl + _ShadowOffset);

                // 2) メリハリ調整（>1 カリッ / <1 ふんわり）
                x = saturate(pow(max(x, 1e-4h), _LightContrast));
                return x;
            }

            // ----- 0..1 の“トゥーン照度”を計算（メイン＋追加ライト） -----
            half ToonLightFactor(float3 posWS, float3 normalWS, float4 shadowCoord)
            {
                half NdotL = 0;

                // メインライト
                Light mainLight = GetMainLight(shadowCoord);
                half ndlMain = saturate(dot(normalWS, mainLight.direction));
                if (_ReceiveShadows > 0.5h)
                    ndlMain *= mainLight.shadowAttenuation;
                NdotL = max(NdotL, ndlMain);

                // 追加ライト（最大寄与を採用）
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                [loop] for (uint i = 0; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, posWS);
                    half ndl = saturate(dot(normalWS, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    NdotL = max(NdotL, ndl);
                }
                #endif

                // 影の広さ/メリハリをここで反映
                return AdjustLightResponse(saturate(NdotL));
            }

            // ----- ピクセルシェーダ：最終色の合成 -----
            half4 frag(Varyings IN) : SV_Target
            {
                // ① 元テクスチャ色（アルベド）
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;

                // ② トゥーン照度（0..1）
                half3 nWS = normalize(IN.normalWS);
                half  v   = ToonLightFactor(IN.posWS, nWS, IN.shadowCoord);

                // ③ 3帯の重み計算
                half wL, wM, wS;
                ThreeBandWeights(v, _Threshold1, _Threshold2, _Fade1, _Fade2, wL, wM, wS);

                // ④ 各トーン色（影の濃さ＝Alphaで調整）
                half3 baseTone   = _BaseColor.rgb      * tex;
                half3 midTone    = ApplyStrength(baseTone,   _MidColor.rgb    * tex, _MidColor.a);
                half3 shadowTone = ApplyStrength(baseTone,   _ShadowColor.rgb * tex, _ShadowColor.a);

                // ⑤ 重みで合成（wL+wM+wS ≈ 1）
                half3 col = wL * baseTone + wM * midTone + wS * shadowTone;

                return half4(col, 1);
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 1: アウトライン（Unlit / Inverted Hull）
        //  ・背面を描画＋法線方向に少し拡張してシルエットを描く
        //  ・オブジェクト空間スケールの影響を受ける（一定太さにしたい場合はSS版に変更可）
        // =====================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front        // 背面を描画（外向きに膨張した“外皮”だけが見える）
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vertOL
            #pragma fragment fragOL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half  _OutlineWidth;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
            };

            // 頂点を法線方向に少しだけ押し出す（オブジェクト空間）
            Varyings vertOL(Attributes IN)
            {
                float3 posOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                VertexPositionInputs vp = GetVertexPositionInputs(posOS);

                Varyings OUT;
                OUT.positionHCS = vp.positionCS;
                return OUT;
            }

            half4 fragOL(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
