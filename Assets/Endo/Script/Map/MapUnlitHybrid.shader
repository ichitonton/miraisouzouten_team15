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

        // ここから追加：プレイヤーの高さで全体トーンを変える用
        _GlobalHeightRef("Global Height Ref", Float) = 10.0          // 基準高さ（例: 10）
        _GlobalHeightSensitivity("Global Height Sensitivity", Float) = 0.01
        _GlobalMinScale("Global Min Scale", Float) = 0.8
        _GlobalMaxScale("Global Max Scale", Float) = 1.2


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


            // グローバル高さ補正用
            float _AvgPlayerHeight;          // C# から送る「プレイヤー平均高さ」
            float _GlobalHeightRef;          // 基準高さ（この高さのとき倍率1.0）
            float _GlobalHeightSensitivity;  // 高さ差 → 明るさ倍率 への変換係数
            float _GlobalMinScale;           // どれだけ暗くするかの下限
            float _GlobalMaxScale;           // どれだけ明るくするかの上限

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

                int id = UNITY_ACCESS_INSTANCED_PROP(Props,_TagId);
                float3 baseColor = _Colors[id].rgb;

                //加算ではなく
                float3 wp = i.worldPos;

                // ===== Surface(高さ) =====
                float heightMax = _SurfaceMinBrightness;

                for (int n = 0; n < _PlayerCount; n++)
                {
                    //オブジェクトとプレイヤーの高低差の値を絶対値で
                    float heightDiff = abs(wp.y - _PlayerPos[n].y);

                    // 元の高さベースの明るさ
                    float baseBri = 1.0 - heightDiff * _HeightSensitivity;
                    baseBri = clamp(baseBri, _SurfaceMinBrightness, _SurfaceMaxBrightness);

                    // プレイヤー n の「絶対の高さ」から係数を作る
                    float playerH = _PlayerPos[n].y;
                    float hDelta = playerH - _GlobalHeightRef;                   // 基準高さとの差
                    float heightScale = 1.0 + hDelta * _GlobalHeightSensitivity; // 差を係数に変換
                    heightScale = clamp(heightScale, _GlobalMinScale, _GlobalMaxScale);

                    // プレイヤー n の高さを反映した明るさ
                    float bri = baseBri * heightScale;

                    // そのピクセルは、影響が一番強いプレイヤーの明るさを採用
                    heightMax = max(heightMax, bri);
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

                return float4(baseColor * final, 1);
            }

            ENDHLSL
        }
    }
}
