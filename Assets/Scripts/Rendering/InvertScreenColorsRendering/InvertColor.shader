Shader "Custom/InvertColor"
{
    Properties
    {
        // <gen:shader-properties>
        _Intensity ("Intensity", Float) = 1
        // </gen:shader-properties>
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        // <gen:hlsl-includes>
        // Core.hlsl for XR dependencies (TEXTURE2D_X, etc.)
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        // </gen:hlsl-includes>

        CBUFFER_START(UnityPerMaterial)
            // <gen:cbuffer-properties>
            float _Intensity;
            // </gen:cbuffer-properties>
        CBUFFER_END

        // <gen:texture-declarations>
        // </gen:texture-declarations>
        ENDHLSL

        Pass
        {
            Name "InvertColor"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                return FragBlit(input, sampler_LinearClamp);
            }

            ENDHLSL
        }
    }
}
