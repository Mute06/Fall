Shader "Custom/MountainBackdrop"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Cutout Mountain Panorama)", 2D) = "white" {}
        [MainColor] _BaseColor("Color Tint", Color) = (1, 1, 1, 1)
        _HorizonFogColor("Horizon Bottom Fog Color", Color) = (0.85, 0.72, 0.62, 1)
        _AtmosphereStrength("Atmospheric Horizon Tint Strength", Range(0, 1)) = 0.4
        _FadeBottomY("Fade Bottom World Y", Float) = -25.0
        _FadeHeightRange("Fade Height Range", Float) = 45.0
        _AlphaBoost("Alpha Multiplier", Range(0.5, 3.0)) = 1.0
        _Cutoff("Alpha Cutoff Threshold", Range(0.0, 0.5)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float4 color        : COLOR;
                float  fogFactor    : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _HorizonFogColor;
                float  _AtmosphereStrength;
                float  _FadeBottomY;
                float  _FadeHeightRange;
                float  _AlphaBoost;
                float  _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(vertexInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // Alpha cutoff for cleanest sky separation
                if (texColor.a < _Cutoff)
                {
                    discard;
                }

                half4 finalColor = texColor * _BaseColor * input.color;

                // Height-based atmospheric horizon fade (eliminates harsh bottom cuts)
                float heightNorm = saturate((input.positionWS.y - _FadeBottomY) / max(0.1, _FadeHeightRange));
                
                // Blend color towards horizon fog color at the bottom
                finalColor.rgb = lerp(_HorizonFogColor.rgb, finalColor.rgb, lerp(1.0 - _AtmosphereStrength, 1.0, heightNorm));

                // Smoothly fade alpha to zero at very bottom
                float alphaFade = smoothstep(0.0, 0.25, heightNorm);
                finalColor.a = saturate(finalColor.a * _AlphaBoost * alphaFade);

                // Apply scene fog
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
