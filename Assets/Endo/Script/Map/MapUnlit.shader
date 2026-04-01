
Shader "Hidden/MapUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _PlayerHeight("Player Height", Float) = 0
        _HeightSensitivity("Sensitivity", Float) = 0.4
        _MinBrightness("Min", Float) = 0.3
        _MaxBrightness("Max", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off


        Pass
        {
           
            HLSLPROGRAM

            // ===== URP Core include =====
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            //C#側から渡される変数
            float4 _Color;
            float _PlayerHeight;
            float _HeightSensitivity;
            float _MinBrightness;
            float _MaxBrightness;



            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
            };

            // ========== 頂点でY反転  ==========
            Varyings vert (Attributes v)
            {
                Varyings o;

                // URPの便利関数：オブジェクト→HClip
                o.pos = TransformObjectToHClip(v.vertex);
                o.worldPos = TransformObjectToWorld(v.vertex).xyz;

                return o;
            }

            // ========== 描画（色だけ返す） ==========
            float4 frag (Varyings i) : SV_Target
            {
                
                float heightDiff = abs(i.worldPos.y - _PlayerHeight);

                float brightness = 1 - heightDiff * _HeightSensitivity;

                brightness = clamp(brightness, _MinBrightness, _MaxBrightness);

                return float4(_Color.rgb * brightness, 1);
            }

            ENDHLSL
        }
    }
}