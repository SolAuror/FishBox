Shader "Hidden/Sol/OutlineFullscreen"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            float4 _MaskTex_TexelSize;
            float  _MaxOutlineWidth;

            #define DIRS       8
            #define MAX_STEPS  20

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                half4 source = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                half4 maskCenter = SAMPLE_TEXTURE2D_LOD(_MaskTex, sampler_PointClamp, uv, 0);

                // Alpha encoding: (0, 0.5] = always-visible, (0.5, 1] = hover
                bool centerIsHover = maskCenter.a > 0.5;

                // Never draw outlines ON hover objects themselves
                if (centerIsHover)
                    return source;

                bool centerIsMasked = maskCenter.a > 0.005;

                float2 texel = _MaskTex_TexelSize.xy;
                half3 outColor = half3(0, 0, 0);
                float closest = _MaxOutlineWidth + 1.0;
                float maxW = min(_MaxOutlineWidth, (float)MAX_STEPS);

                [unroll]
                for (int i = 0; i < DIRS; i++)
                {
                    float angle = (float)i * (TWO_PI / (float)DIRS);
                    float2 dir = float2(cos(angle), sin(angle));

                    [loop]
                    for (float d = 1.0; d <= maxW; d += 1.0)
                    {
                        half4 s = SAMPLE_TEXTURE2D_LOD(_MaskTex, sampler_PointClamp, uv + dir * texel * d, 0);
                        if (s.a > 0.005)
                        {
                            bool nbrHover = s.a > 0.5;

                            // If center is always-visible mask, only draw hover outlines
                            // on top — skip same-group (always-visible) neighbors
                            if (centerIsMasked && !nbrHover)
                            {
                                break;
                            }

                            // Decode per-object width from encoded alpha
                            float objWidth = nbrHover
                                ? (s.a - 0.51) / 0.49 * _MaxOutlineWidth
                                : s.a / 0.49 * _MaxOutlineWidth;

                            if (d <= objWidth && d < closest)
                            {
                                closest = d;
                                outColor = s.rgb;
                            }
                            break;
                        }
                    }
                }

                if (closest <= maxW)
                    return half4(outColor, 1.0);

                return source;
            }
            ENDHLSL
        }
    }
}
