Shader "Custom/MapUnlit"
{
    Properties { _Color("Color", Color) = (1,0,0,1) }
    SubShader
    {
        Tags {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry"
        }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            float4 _Color;

            Varyings vert (Attributes input)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
