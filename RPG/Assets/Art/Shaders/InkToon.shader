// Cartoon cel shading with Chinese ink-painting (水墨) touches, for characters.
//  - Two-band cel ramp: lit / wash shadow / core shadow, tinted toward cool ink-wash greys,
//    with shadows slightly desaturated and the albedo warmed toward rice-paper tones.
//  - Ink rim: a brushy dark contour on the inner silhouette.
//  - Ink outline: inverted hull whose width swells and thins like a brush stroke, with
//    dry-brush (飞白) gaps where the stroke runs out of ink.
//  - Rim and outline fade out with camera distance: from the high RTS view a figure is only a few dozen
//    pixels tall and the ink would swallow it, so far figures read by colour and light instead.
// The outline and brush noise read TEXCOORD7 (masked smooth normal in tangent space + stroke noise),
// baked at import by InkOutlineDataBaker for models under Assets/Art/Characters.
Shader "WorldRPG/InkToon"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Header(Distance)]
        _InkFadeStart("Ink Fade Start (m)", Float) = 18
        _InkFadeEnd("Ink Fade End (m)", Float) = 50

        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 1
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 2)) = 0.5
        _Saturation("Saturation", Range(0, 1.5)) = 0.85
        _PaperColor("Paper Color", Color) = (0.97, 0.92, 0.80, 1)
        _PaperTint("Paper Tint", Range(0, 1)) = 0.25

        [Header(Cel Shading)]
        _ShadowThreshold("Shadow Threshold", Range(0, 1)) = 0.5
        _CoreShadowThreshold("Core Shadow Threshold", Range(0, 1)) = 0.2
        _ShadowSoftness("Band Softness", Range(0.001, 0.3)) = 0.03
        _ShadowColor("Wash Shadow Tint", Color) = (0.58, 0.60, 0.70, 1)
        _CoreShadowColor("Core Shadow Tint", Color) = (0.36, 0.37, 0.48, 1)
        _ShadowDesaturate("Shadow Desaturate", Range(0, 1)) = 0.4
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.3

        [Header(Ink Rim)]
        _InkColor("Ink Color", Color) = (0.07, 0.07, 0.09, 1)
        _InkRimWidth("Ink Rim Width", Range(0, 1)) = 0.32
        _InkRimSoftness("Ink Rim Softness", Range(0.001, 0.5)) = 0.06
        _InkRimStrength("Ink Rim Strength", Range(0, 1)) = 0.75
        _InkRimBreakup("Ink Rim Brush Breakup", Range(0, 1)) = 0.45

        [Header(Ink Outline)]
        _OutlineColor("Outline Color", Color) = (0.05, 0.05, 0.07, 1)
        _OutlineWidth("Outline Width (m)", Range(0, 0.05)) = 0.02
        _OutlineMinPixels("Min Width (px)", Range(0, 4)) = 1.2
        _OutlineMaxPixels("Max Width (px)", Range(0, 12)) = 7
        _OutlineVariation("Brush Width Variation", Range(0, 1)) = 0.7
        _OutlineDryBrush("Dry Brush Gaps", Range(0, 1)) = 0.12
        [Tooltip(Pushes the hull away from the camera so it only shows at silhouettes)] _OutlineDepthOffset("Depth Offset (m)", Range(0, 0.2)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Saturation;
            half4 _PaperColor;
            half _PaperTint;
            half _ShadowThreshold;
            half _CoreShadowThreshold;
            half _ShadowSoftness;
            half4 _ShadowColor;
            half4 _CoreShadowColor;
            half _ShadowDesaturate;
            half _AmbientStrength;
            half4 _InkColor;
            half _InkRimWidth;
            half _InkRimSoftness;
            half _InkRimStrength;
            half _InkRimBreakup;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineMinPixels;
            float _OutlineMaxPixels;
            half _OutlineVariation;
            half _OutlineDryBrush;
            float _OutlineDepthOffset;
            float _InkFadeStart;
            float _InkFadeEnd;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        // Baked smooth normal (tangent space, xyz) -> object space, using the skinned TBN.
        // Its length is the outline mask (0 = buried under another layer, no stroke).
        float3 SmoothNormalOS(float3 normalOS, float4 tangentOS, float4 inkData, out float mask)
        {
            if (all(inkData == 0)) { mask = 1; return normalOS; } // no baked data: fall back
            float3 bitangentOS = cross(normalOS, tangentOS.xyz) * tangentOS.w;
            float3 smoothOS = tangentOS.xyz * inkData.x + bitangentOS * inkData.y + normalOS * inkData.z;
            mask = saturate(length(inkData.xyz));
            return mask > 1e-3 ? smoothOS : normalOS;
        }

        // 1 up close, 0 beyond _InkFadeEnd.
        float InkFade(float3 positionWS)
        {
            return 1 - smoothstep(_InkFadeStart, _InkFadeEnd, distance(positionWS, GetCameraPositionWS()));
        }
        ENDHLSL

        // ------------------------------------------------------------------ main shading
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 inkData    : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                half   inkNoise   : TEXCOORD4;
                half   fogFactor  : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = nrm.normalWS;
                output.tangentWS = float4(nrm.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.inkNoise = input.inkData.w;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half Band(half x, half threshold)
            {
                return smoothstep(threshold - _ShadowSoftness, threshold + _ShadowSoftness, x);
            }

            half3 AdditionalLightToon(Light light, float3 normalWS, half3 albedo)
            {
                half ndl = dot(normalWS, light.direction) * 0.5 + 0.5;
                return albedo * light.color * (light.distanceAttenuation * light.shadowAttenuation * Band(ndl, _ShadowThreshold));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 albedo = (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor).rgb;
                albedo = lerp(Luminance(albedo).xxx, albedo, _Saturation);
                albedo = lerp(albedo, albedo * _PaperColor.rgb, _PaperTint);

                float3 geomNormalWS = normalize(input.normalWS);
                float3 normalWS = geomNormalWS;
                #ifdef _NORMALMAP
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    float3 bitangentWS = cross(geomNormalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    normalWS = normalize(mul(normalTS, float3x3(input.tangentWS.xyz, bitangentWS, geomNormalWS)));
                #endif
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // LIGHT_LOOP_BEGIN (Forward+) expects an InputData named inputData.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);

                // Main light: cast shadows push into the wash band, the terminator into the core band.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, shadowMask);
                half ndl = dot(normalWS, mainLight.direction) * 0.5 + 0.5;
                half lit = Band(ndl, _ShadowThreshold) * mainLight.shadowAttenuation;
                half core = Band(ndl, _CoreShadowThreshold);
                half3 tone = lerp(_CoreShadowColor.rgb, lerp(_ShadowColor.rgb, 1, lit), core);
                half3 shadedAlbedo = lerp(albedo, Luminance(albedo).xxx, _ShadowDesaturate * (1 - lit));

                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    ambient *= GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV).indirectAmbientOcclusion;
                #endif
                half3 color = shadedAlbedo * (tone * mainLight.color + ambient);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        color += AdditionalLightToon(GetAdditionalLight(lightIndex, input.positionWS, shadowMask), normalWS, albedo);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(lightCount)
                        color += AdditionalLightToon(GetAdditionalLight(lightIndex, input.positionWS, shadowMask), normalWS, albedo);
                    LIGHT_LOOP_END
                #endif

                // Brushy ink contour along the inner silhouette.
                half fresnel = 1 - saturate(dot(geomNormalWS, viewDirWS));
                half brush = (input.inkNoise - 0.5) * _InkRimBreakup;
                half rimEdge = 1 - _InkRimWidth;
                half rim = smoothstep(rimEdge - _InkRimSoftness, rimEdge + _InkRimSoftness, fresnel + brush);
                color = lerp(color, _InkColor.rgb, rim * _InkRimStrength * InkFade(input.positionWS));

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ ink outline (inverted hull)
        Pass
        {
            Name "InkOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 inkData    : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half   inkNoise   : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
                half   inkFade    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                half fade = InkFade(TransformObjectToWorld(input.positionOS.xyz));
                output.inkFade = fade;
                float mask;
                float3 normalWS = TransformObjectToWorldNormal(SmoothNormalOS(input.normalOS, input.tangentOS, input.inkData, mask));
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                float viewDepth = max(-positionVS.z, 1e-3);
                float pixelsPerMeter = UNITY_MATRIX_P[1][1] * _ScreenParams.y * 0.5 / viewDepth;

                // A world-space width converted to pixels, clamped so distant characters keep a
                // visible line and close-ups don't get blobby.
                float pixels = clamp(_OutlineWidth * pixelsPerMeter, _OutlineMinPixels, _OutlineMaxPixels);
                // Brush pressure: the stroke swells and thins along the surface.
                half pressure = 0.25 + 1.25 * smoothstep(0.2, 0.8, input.inkData.w);
                pixels *= lerp(1, pressure, _OutlineVariation) * mask * fade;

                // Push the hull back along the view ray so it hides behind overlapping parts and
                // only reads at true silhouettes. Scaled with the stroke's real size: far away the
                // min pixel width covers several centimetres and would otherwise speckle through.
                float extrusionMeters = pixels / pixelsPerMeter;
                positionVS += normalize(positionVS) * (_OutlineDepthOffset + extrusionMeters * 1.5);
                float4 positionCS = TransformWViewToHClip(positionVS);

                // Extrude in screen space along the projected smooth normal.
                float2 dirPixels = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy * _ScreenParams.xy;
                float dirLength = length(dirPixels);
                float2 dir = dirLength > 1e-5 ? dirPixels / dirLength : float2(0, 0);
                positionCS.xy += dir * pixels * 2 / _ScreenParams.xy * positionCS.w;
                output.positionCS = positionCS;
                output.inkNoise = input.inkData.w;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Dry brush: where the stroke is thinnest, let it break up entirely.
                clip(input.inkNoise - _OutlineDryBrush);
                clip(input.inkFade - 0.02);   // faded out: no hull at all (not even a sub-pixel speckle)
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ depth
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalize(input.normalWS));
                    return half4(PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5)), 0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
