Shader "URP/Unlit_AlwaysOnTop_FieldOnly"
{
    Properties{
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
    }
    SubShader{
        Tags{ "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Stencil{ Ref 1 Comp Equal Pass Keep } // Åöè∞(=1)ÇÃÇ∆Ç±ÇæÇØï`Ç≠

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float4 _BaseColor;
        struct A{ float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
        struct V{ float4 positionCS:SV_Position; float2 uv:TEXCOORD0; };
        V vert(A v){ V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
        float4 frag(V i):SV_Target{ return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor; }
        ENDHLSL
        Pass{ Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
    FallBack Off
}
