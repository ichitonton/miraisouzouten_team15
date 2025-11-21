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
        _PlayerPos("Player Position", Vector) = (0,0,0,0)
        _DistSensitivity("Distance Sensitivity", Float) = 1.0
        _PointRadius("Point Bright Radius", Float) = 1.0
        _PointMinBrightness("Point Bright Radius", Float) = 0.3
        _PointMaxBrightness("Point Bright Radius", Float) = 1.0

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
            float3 _PlayerPos;
            float _DistSensitivity;
            float _PointRadius;
            float _PointMinBrightness;
            float _PointMaxBrightness;

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
                float3 wp = i.worldPos;

                // --------- HEIGHT MODE ---------
                float heightDiff = abs(wp.y - _PlayerHeight);
                float heightBri = 1 - heightDiff * _HeightSensitivity;
                heightBri = clamp(heightBri, _SurfaceMinBrightness, _SurfaceMaxBrightness);

                // --------- POINT MODE ---------
                float dist = distance(wp, _PlayerPos);

                float t = saturate((dist - _PointRadius) * _DistSensitivity);
                float pointBri = lerp(_PointMaxBrightness, _PointMinBrightness, t);

                // --------- FINAL ---------
                float final = heightBri * pointBri;

                return float4(_Color.rgb * final, 1);
            }

            ENDHLSL
        }
    }
}
