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
                // ==== ピクセル化処理 ====
                // 1ピクセルのブロックサイズ（UV空間）
                float2 blockSize = _PixelSize * _MainTex_TexelSize.xy;

                // UVを blockSize 単位で丸める（これでモザイク）
                float2 baseUV = floor(i.uv / blockSize) * blockSize;

                // 中心サンプル
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV);

                // ==== かるいブラー（周囲4サンプルを混ぜる） ====
                float2 offset = blockSize * 0.5;

                float4 c1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV + float2( offset.x,  0));
                float4 c2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV + float2(-offset.x,  0));
                float4 c3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV + float2( 0,  offset.y));
                float4 c4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV + float2( 0, -offset.y));

                float4 blurCol = (col + c1 + c2 + c3 + c4) / 5.0;

                // ブラー強度でミックス
                float4 finalCol = lerp(col, blurCol, _BlurStrength);

                // 全体にTint（暗く＋α値もこれで調整可）
                finalCol.rgb = lerp(finalCol.rgb, _Tint.rgb, _Tint.a);
                finalCol.a = 1.0; // RawImage の Color.a に任せたければここを変えてもOK

                // UIカラーも乗算（必要なければ消していい）
                finalCol *= i.color;

                return finalCol;
            }

            ENDHLSL
        }
    }
}
