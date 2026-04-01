Shader "UI/VideoKeyBlackColorGrade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        // ===== 黒抜き =====
        _Cutoff ("Black Cutoff", Range(0,1)) = 0.08
        _Softness ("Softness", Range(0.001,0.2)) = 0.05

        // ===== 全体アルファ =====
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 1

        // ===== 色いじり（Tint） =====
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _TintStrength ("Tint Strength", Range(0,1)) = 0

        // ===== 明度/コントラスト/彩度 =====
        _Brightness ("Brightness", Range(-1,1)) = 0
        _Contrast   ("Contrast", Range(0,2)) = 1
        _Saturation ("Saturation", Range(0,2)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _Cutoff;
            float _Softness;
            float _GlobalAlpha;

            float4 _TintColor;
            float _TintStrength;

            float _Brightness;
            float _Contrast;
            float _Saturation;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // =====================================
                // 1) 黒抜き（アルファ計算）今まで通り
                // =====================================
                float lum = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                float a = smoothstep(_Cutoff, _Cutoff + _Softness, lum);
                col.a = a * _GlobalAlpha;

                // =====================================
                // 2) RGB調整（Brightness / Contrast / Saturation）
                // =====================================
                float3 rgb = col.rgb;

                // 明度（-1+1）: 単純加算
                rgb += _Brightness;

                // コントラスト（0-2）: 0.5 を中心に拡大縮小
                rgb = (rgb - 0.5) * _Contrast + 0.5;

                // 彩度（0 - 2）: グレー(luma)と元色を lerp
                float gray = dot(rgb, float3(0.2126, 0.7152, 0.0722));
                rgb = lerp(gray.xxx, rgb, _Saturation);

                // =====================================
                // 3) Tint（色味変更）
                // =====================================
                float3 tinted = rgb * _TintColor.rgb;
                rgb = lerp(rgb, tinted, _TintStrength);

                // clamp（暴れ防止）
                col.rgb = saturate(rgb);

                return col;
            }
            ENDCG
        }
    }
}
