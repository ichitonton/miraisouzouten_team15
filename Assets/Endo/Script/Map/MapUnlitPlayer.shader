Shader "Custom/MapUnlitPlayer"
{
    Properties
    {
        _Color (
            "Color", Color) = (1,1,1,1)
        _PlayerHeight("Player Position", Vector) = (0,0,0,0)
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
            float3 _PlayerPosition;
            float _DistSensitivity;
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
                //オブジェクトで頂点を参照して色を変えるため（オブジェクト内でグラデーションができるよ）
                o.pos = TransformObjectToHClip(v.vertex);
                o.worldPos = TransformObjectToWorld(v.vertex).xyz;

                return o;
            }

            // ========== 描画（色だけ返す） ==========
            float4 frag (Varyings i) : SV_Target
            {
                
                float3 wp = float3(i.worldPos.x, i.worldPos.y, i.worldPos.z);
                float3 pp = float3(_PlayerPosition.x, _PlayerPosition.y, _PlayerPosition.z);

                float dist = distance(wp,pp);

                 // 1m以内は明るい
                float radius = 3.0;

                 // dist が radius を超えた量に応じてフェード
                float t = saturate((dist - radius) * _DistSensitivity);

                //float brightness = 1 - dist * _DistSensitivity;

                //brightness = clamp(brightness, _MinBrightness, _MaxBrightness);
                float brightness = lerp(_MaxBrightness, _MinBrightness, t);

                return float4(_Color.rgb * brightness, 1);
            }

            ENDHLSL
        }
    }
}
