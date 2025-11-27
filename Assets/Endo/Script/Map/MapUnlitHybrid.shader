Shader "Hidden/MapUnlitHybrid"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)

        // ---- Height mode ----
        _PlayerHeight("Player Height", Float) = 0
        _HeightSensitivity("Height Sensitivity", Float) = 0.4
        _SurfaceMinBrightness("Min Brightness", Float) = 0.3
        _SurfaceMaxBrightness("Max Brightness", Float) = 1.0

        // ---- Point mode ----
        // 最大32プレイヤー分
        //「配列の宣言をしないと2つ目以降が無効になる」(= (0,0,0)とかはダメよ)
        //_PlayerPos("Player Posistion", Vector) = {}
        _PlayerCount("Player Count", int) = 1

        _DistSensitivity("Distance Sensitivity", Float) = 1.0
        _PointRadius("Point Bright Radius", Float) = 1.0
        _PointMinBrightness("Point Min Brightness", Float) = 0.3
        _PointMaxBrightness("Point Max Brightness", Float) = 1.0

    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            float4 _Color;

            // Height variables
            float _PlayerHeight;
            float _HeightSensitivity;
            float _SurfaceMinBrightness;
            float _SurfaceMaxBrightness;

            // Point variables
            float3 _PlayerPos[32];
            int _PlayerCount;

            float _DistSensitivity;
            float _PointRadius;
            float _PointMinBrightness;
            float _PointMaxBrightness;

            //カラーの配列
            float4 _Colors[32];
            int _ColorCount;

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(int, _TagId)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.worldPos = TransformObjectToWorld(v.vertex).xyz;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {

                //int id = UNITY_ACCESS_INSTANCED_PROP(_TagId);
                //float3 baseColor = _Colors[id].rgb;

                //加算ではなく
                float3 wp = i.worldPos;

                // ===== Surface(高さ) =====
                float heightMax = _SurfaceMinBrightness;

                for (int n = 0; n < _PlayerCount; n++)
                {
                    //オブジェクトとプレイヤーの高低差の値を絶対値で
                    float heightDiff = abs(wp.y - _PlayerPos[n].y);

                    //遠ければ暗く、近ければ明るい
                    float bri = 1 - heightDiff * _HeightSensitivity;

                    // _SurfaceMinBrightness～_SurfaceMaxBrightnessの間に値を修正
                    bri = clamp(bri, _SurfaceMinBrightness, _SurfaceMaxBrightness);

                    heightMax = max(heightMax, bri);//一番明るい値を採用
                }

                // ===== Point（距離の丸） =====
                float pointMax = _PointMinBrightness;

                for (int n = 0; n < _PlayerCount; n++)
                {
                    float dist = distance(wp, _PlayerPos[n]);
                    float t = saturate((dist - _PointRadius) * _DistSensitivity);
                    float bri = lerp(_PointMaxBrightness, _PointMinBrightness, t);

                    pointMax = max(pointMax, bri);
                }

                // ===== 合成 =====
                float final = heightMax * pointMax;

                return float4(_Color.rgb * final, 1);
            }

            ENDHLSL
        }
    }
}
