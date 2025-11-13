Shader "URP/StencilReplace"
{
    Properties { _StencilRef("Stencil Ref", Int) = 0 }
    SubShader{
        Tags{ "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        ZTest LEqual
        ZWrite Off
        ColorMask 0
        Stencil{ Ref [_StencilRef] Comp Always Pass Replace }

        Pass{
            HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{ float4 pos:POSITION; };
            struct V{ float4 pos:SV_Position; };
            V vert(A i){ V o; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }
            half4 frag(V i):SV_Target{ return 0; }
            ENDHLSL
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
