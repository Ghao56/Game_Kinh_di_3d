Shader "Custom/InteractableOutlineComposite"
{
    Properties
    {
        _OutlineThicknessPx ("Outline Thickness (px)", Float) = 3
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 1
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 3
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Composite"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            CBUFFER_START(UnityPerMaterial)
                float _OutlineThicknessPx;
                float _GlowIntensity;
                float _PulseSpeed;
                float _PulseMin;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 px = _OutlineThicknessPx / _ScreenParams.xy;

                half4 center = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                half4 neighbors[4];
                neighbors[0] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(px.x, 0));
                neighbors[1] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv - float2(px.x, 0));
                neighbors[2] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(0, px.y));
                neighbors[3] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv - float2(0, px.y));

                half4 acc = (half4)0;
                half count = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    if (neighbors[i].a > 0.5)
                    {
                        acc += neighbors[i];
                        count += 1;
                    }
                }

                half pulse = _PulseMin + (1.0 - _PulseMin) * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                float isEdge = center.a > 0.5 ? 0 : (count > 0 ? 1 : 0);
                half alpha = isEdge * pulse * _GlowIntensity;
                half3 color = count > 0 ? acc.rgb / max(count, 1e-3) : (half3)0;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}