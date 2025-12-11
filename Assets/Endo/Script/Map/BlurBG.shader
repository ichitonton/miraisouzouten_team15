Shader "Custom/BlurBG"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size", Float) = 4.0     // モザイクの粗さ
        _BlurStrength ("Blur Strength", Float) = 0.5
        _Tint ("Tint", Color) = (0,0,0,0.4)        // 全体を暗くする色
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;   // x=1/width, y=1/height

            float _PixelSize;
            float _BlurStrength;
            float4 _Tint;

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

            Varyings vert (Attributes v)
            {
                Varyings o;

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionHCS = pos.positionCS;
                o.uv = v.uv;
                o.color = v.color;

                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                // ===== 純粋なブラー =====
                // テクセルサイズにブラー半径を掛けて、画面上の距離に変換
                float2 r = _PixelSize * _MainTex_TexelSize.xy;

                // 中心
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // ガウシアンっぽい9タップブラー（＋少しだけ広げた15タップ風）
                float4 acc = col * 4.0;
                float  w   = 4.0;

                // 十字方向
                float4 c1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( r.x,  0));
                float4 c2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-r.x,  0));
                float4 c3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( 0,  r.y));
                float4 c4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( 0, -r.y));

                acc += (c1 + c2 + c3 + c4) * 2.0;
                w   += 2.0 * 4.0;

                // 斜め方向
                float4 c5 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( r.x,  r.y));
                float4 c6 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-r.x,  r.y));
                float4 c7 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( r.x, -r.y));
                float4 c8 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-r.x, -r.y));

                acc += (c5 + c6 + c7 + c8);
                w   += 4.0;

                float4 blurCol = acc / w;

                // 元の絵とブラーのミックス
                float4 finalCol = lerp(col, blurCol, _BlurStrength);

                // 少しだけ明るいところを強調（光がにじんで見える用のミニBloom）
                float luma = dot(finalCol.rgb, float3(0.299, 0.587, 0.114));
                float glow = saturate((luma - 0.6) * 2.0);   // 明るい部分だけ強調
                finalCol.rgb = lerp(finalCol.rgb, finalCol.rgb * 1.2, glow);

                // 全体にTint（暗くしつつ、背景を目立たなくする）
                finalCol.rgb = lerp(finalCol.rgb, _Tint.rgb, _Tint.a);
                finalCol.a   = 1.0;

                // UI側の Color も乗算
                finalCol *= i.color;

                return finalCol;
            }

            ENDHLSL
        }
    }
}
