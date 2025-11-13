Shader "Custom/MapUnlit"
{
    Properties { _Color("Color", Color) = (1,1,1,1) }

    SubShader
    {
        Tags{ "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            float4 _Color;

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                return float4(_Color.rgb, 1); // “§–¾‚ð”ð‚¯‚½‚¢‚È‚ç return float4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}