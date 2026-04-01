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
        //  この高さ差までは「そのプレイヤーの段」とみなす
        _HeightAffectRange("Height Affect Range", Float) = 1.0

        //_Alpha
        _Alpha("Alpha",Float) = 1.0

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

            float _HeightAffectRange;

            float _Alpha;

            //カラーの配列
            float4 _Colors[32];
            int _ColorCount;

            // ここを「普通の」uniform にする
            int _TagId;

            //  instancing ブロックは消す
            // UNITY_INSTANCING_BUFFER_START(Props)
            // UNITY_DEFINE_INSTANCED_PROP(int, _TagId)
            // UNITY_INSTANCING_BUFFER_END(Props)

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

                // _TagId をそのまま使う
                int id = clamp(_TagId, 0, _ColorCount - 1);
                float3 baseColor = _Colors[id].rgb;

                // ===== 1) このピクセルに一番近い高さのプレイヤーを探す =====
                int   bestIndex = -1;
                float bestDiff  = 999999.0;

                [loop]
                for (int n = 0; n < _PlayerCount; n++)
                {
                    float d = abs(wp.y - _PlayerPos[n].y);
                    if (d < bestDiff)
                    {
                        bestDiff  = d;
                        bestIndex = n;
                    }
                }

                // ===== 2) 高さモード（そのプレイヤーだけを見る） =====
                float heightBright = _SurfaceMinBrightness;

                if (bestIndex >= 0)
                {
                    float playerY = _PlayerPos[bestIndex].y;

                    //  ここがポイント：高さ差が _HeightAffectRange を超えたら「影響なし」
                    if (bestDiff <= _HeightAffectRange)
                    {
                        // bestDiff が 01_HeightAffectRange のとき 01 に正規化
                        float t = 1.0 - (bestDiff / _HeightAffectRange);
                        t = saturate(t);

                        // t=0 → Min、t=1 → Max
                        float baseBri = lerp(_SurfaceMinBrightness, _SurfaceMaxBrightness, t);

                        // プレイヤー絶対高さで少し補正
                        float hDelta = playerY - _GlobalHeightRef;
                        float heightScale = 1.0 + hDelta * _GlobalHeightSensitivity;
                        heightScale = clamp(heightScale, _GlobalMinScale, _GlobalMaxScale);

                        heightBright = baseBri * heightScale;
                    }
                    // else の場合 = 遠すぎる → heightBright は _SurfaceMinBrightness のまま
                }

                // ===== 3) Point（距離●） =====
                float pointMax = _PointMinBrightness;

                [loop]
                for (int n = 0; n < _PlayerCount; n++)
                {
                    float dist = distance(wp, _PlayerPos[n]);
                    float t = saturate((dist - _PointRadius) * _DistSensitivity);
                    float bri = lerp(_PointMaxBrightness, _PointMinBrightness, t);

                    pointMax = max(pointMax, bri);
                }

                // ===== 4) 合成 =====
                float final = heightBright * pointMax;

                return float4(baseColor * final, _Alpha);
            }

            ENDHLSL
        }
    }
}
