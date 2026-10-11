// Full-screen ink-painting pass (run by a Full Screen Pass Renderer Feature, before post-processing):
//  - Ink contours: edges in linear depth (silhouettes, eaves against the street) and in normals (roof ridges,
//    wall corners), drawn as a multiply so the line keeps the colour it crosses, like ink on tinted paper.
//    Line width is in pixels; lines fade out with distance so the far town doesn't turn to scribble.
//  - Distance haze toward a paper colour, from linear depth (replaces scene fog, whose distance range
//    doesn't come out as configured on this project's URP setup).
//  - A soft luminous haze in one corner of the frame, where the sun sits (the references' misty top-right).
//  - A faint static paper grain.
// Needs depth + normals (the feature's Requirements), which every lit shader in the project writes.
Shader "WorldRPG/InkEdges"
{
    Properties
    {
        [Header(Ink Lines)]
        _InkColor("Ink Color", Color) = (0.2, 0.18, 0.17, 1)
        _LineStrength("Line Strength", Range(0, 1)) = 0.7
        _LineWidth("Line Width (px)", Range(0.5, 3)) = 1.0
        _DepthThreshold("Depth Edge (relative)", Range(0.001, 0.2)) = 0.025
        _NormalThreshold("Normal Edge", Range(0.05, 1)) = 0.45
        _LineFadeStart("Line Fade Start (m)", Float) = 90
        _LineFadeEnd("Line Fade End (m)", Float) = 260
        _InnerFadeStart("Inner Line Fade Start (m)", Float) = 40
        _InnerFadeEnd("Inner Line Fade End (m)", Float) = 110

        [Header(Haze)]
        _HazeColor("Haze Color", Color) = (0.84, 0.84, 0.82, 1)
        _HazeStart("Haze Start (m)", Float) = 140
        _HazeEnd("Haze End (m)", Float) = 500
        _HazeStrength("Haze Strength", Range(0, 1)) = 0.35

        [Header(Sun Glow)]
        _SunGlowColor("Sun Glow Color", Color) = (1.0, 0.98, 0.94, 1)
        _SunGlowCenter("Sun Glow Centre (screen uv)", Vector) = (0.95, 1.05, 0, 0)
        _SunGlowRadius("Sun Glow Radius", Range(0.1, 2)) = 0.85
        _SunGlowStrength("Sun Glow Strength", Range(0, 1)) = 0.32

        [Header(Paper)]
        _PaperGrain("Paper Grain", Range(0, 0.1)) = 0.025
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "InkEdges"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _InkColor;
            half _LineStrength;
            float _LineWidth;
            float _DepthThreshold;
            half _NormalThreshold;
            float _LineFadeStart;
            float _LineFadeEnd;
            float _InnerFadeStart;
            float _InnerFadeEnd;
            half4 _HazeColor;
            float _HazeStart;
            float _HazeEnd;
            half _HazeStrength;
            half4 _SunGlowColor;
            float4 _SunGlowCenter;
            float _SunGlowRadius;
            half _SunGlowStrength;
            half _PaperGrain;

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool background = raw <= 1e-6;
                #else
                    bool background = raw >= 1 - 1e-6;
                #endif
                if (background) return half4(color, 1);

                float depth = LinearEyeDepth(raw, _ZBufferParams);
                float3 normal = SampleSceneNormals(uv);
                float2 texel = _ScreenSize.zw * _LineWidth;

                // Cross of four neighbours: the largest relative depth jump and the largest normal bend.
                float2 offsets[4] = { float2(texel.x, 0), float2(-texel.x, 0), float2(0, texel.y), float2(0, -texel.y) };
                float depthEdge = 0;
                half normalEdge = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float d = EyeDepth(uv + offsets[i]);
                    // Only the nearer side of a silhouette draws, so lines don't double up.
                    depthEdge = max(depthEdge, (d - depth) / depth);
                    normalEdge = max(normalEdge, 1 - dot(normal, SampleSceneNormals(uv + offsets[i])));
                }
                // Interior (normal) lines fade sooner than silhouettes: small, far things (people seen from the
                // RTS height, roof-tile ridges) would otherwise fill up with lines.
                half inner = smoothstep(_NormalThreshold, _NormalThreshold + 0.3, normalEdge)
                             * (1 - smoothstep(_InnerFadeStart, _InnerFadeEnd, depth));
                half edge = max(smoothstep(_DepthThreshold, _DepthThreshold * 2.5, depthEdge), inner);
                edge *= 1 - smoothstep(_LineFadeStart, _LineFadeEnd, depth);
                color = lerp(color, color * _InkColor.rgb, edge * _LineStrength);

                half haze = smoothstep(_HazeStart, _HazeEnd, depth) * _HazeStrength;
                color = lerp(color, _HazeColor.rgb, haze);

                // Misty sunlit corner (aspect-corrected distance from the glow centre).
                float2 toSun = (uv - _SunGlowCenter.xy) * float2(_ScreenSize.x / _ScreenSize.y, 1);
                half glow = pow(saturate(1 - length(toSun) / _SunGlowRadius), 2) * _SunGlowStrength;
                color = lerp(color, _SunGlowColor.rgb, glow);

                // Screen-locked grain reads as paper fibre rather than film noise.
                color *= 1 + (Hash(floor(uv * _ScreenSize.xy * 0.5)) - 0.5) * _PaperGrain;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
