Shader "UI/VideoKeyBlack"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Black Cutoff", Range(0,1)) = 0.08
        _Softness ("Softness", Range(0.001,0.2)) = 0.05
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 1
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

                // luminance（黒かどうか判定）
                float lum = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));

                // 黒→透明、明るい→不透明
                float a = smoothstep(_Cutoff, _Cutoff + _Softness, lum);

                col.a = a * _GlobalAlpha;
                return col;
            }
            ENDCG
        }
    }
}
