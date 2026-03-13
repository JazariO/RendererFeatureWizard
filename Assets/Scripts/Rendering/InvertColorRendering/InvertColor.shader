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
        // </gen:hlsl-includes>

        CBUFFER_START(UnityPerMaterial)
            // <gen:cbuffer-properties>
            float _Intensity;
            // </gen:cbuffer-properties>
        CBUFFER_END

        // <gen:texture-declarations>
        // _BlitTexture is bound by URP's blit utilities when using Blitter.BlitTexture.
        #ifndef UNITY_CORE_BLIT_INCLUDED
        TEXTURE2D_X(_BlitTexture);
        #endif
        #ifndef UNITY_CORE_SAMPLERS_INCLUDED
        SAMPLER(sampler_LinearClamp);
        #endif

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

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Sample the source texture (XR-safe).
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                return col;
            }

            ENDHLSL
        }
    }
}
