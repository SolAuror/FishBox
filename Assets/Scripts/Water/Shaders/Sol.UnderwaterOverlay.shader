// Sol.UnderwaterOverlay — fullscreen post-process for underwater visuals
//
// Used as the material for UnderwaterRendererFeature.
// Driven entirely by two global properties set each frame by
// UnderwaterVolumeController:
//
//   _UnderwaterFactor   0–1  (0 = above water, 1 = fully submerged)
//   _UnderwaterDepth    camera metres below the wave surface (≥ 0)
//
// Effects (all scale with _UnderwaterFactor, individually togglable via keywords):
//   1. Surface distortion + chromatic aberration
//   2. Wavelength-dependent colour absorption
//   3. Exponential depth fog
//   4. Caustic overlay (world-drift anchored)
//   5. Fake light shafts / god rays
//   6. Depth-based blur
//   7. Surface proximity brightening
//   8. Eye adaptation (anti-washout)
//   9. Edge vignette
//
// Requirements:
//   URP 17 / Unity 6.  Uses Blit.hlsl (vertex shader = Vert from that file).
//   Enable "Depth Texture" and "Opaque Texture" in your URP Renderer Asset.

Shader "Sol/UnderwaterOverlay"
{
    Properties
    {
        [Header(Colour Absorption)]
        [HDR] _UnderwaterTint   ("Deep Water Tint",  Color)       = (0.04, 0.22, 0.38, 1)
        _TintStrength            ("Tint Strength",   Range(0,1)) = 0.55
        _AbsorptionR             ("Red Absorption",  Range(0,6)) = 3.0
        _AbsorptionG             ("Green Absorption", Range(0,4)) = 1.5
        _AbsorptionB             ("Blue Absorption",  Range(0,2)) = 0.5

        [Header(Depth Fog)]
        _FogDensity    ("Fog Density",              Range(0,8)) = 1.2
        _FogStartDepth ("Fog Start (metres below)", Range(0,10)) = 0.5

        [Header(Surface Distortion)]
        _DistortionAmount ("Amount",  Range(0, 0.03)) = 0.008
        _DistortionSpeed  ("Speed",   Range(0, 5))    = 1.2
        _DistortionScale  ("Scale",   Range(0.5,10)) = 3.0

        [Header(Chromatic Aberration)]
        _ChromaStrength ("Chromatic Strength", Range(0,1)) = 0.5

        [Header(Caustics)]
        [NoScaleOffset] _CausticsMap ("Caustics Texture", 2D) = "white" {}
        _CausticsTiling    ("Tiling",    Range(0.5, 20)) = 4.0
        _CausticsSpeed     ("Speed",     Range(0, 2))    = 0.35
        _CausticsIntensity ("Intensity", Range(0, 4))    = 0.7
        _CausticsDrift      ("World Drift Speed", Range(0, 0.5)) = 0.1

        [Header(Light Shafts)]
        _ShaftIntensity ("Intensity",    Range(0, 0.5)) = 0.15
        _ShaftDir       ("Sun Direction (XY)", Vector) = (0.2, 1.0, 0, 0)

        [Header(Depth Blur)]
        _BlurMaxDepth   ("Full Blur Depth (m)", Range(1,20)) = 5.0
        _BlurAmount     ("Blur Spread",         Range(0, 0.005)) = 0.002

        [Header(Surface Proximity)]
        _SurfaceBrightness ("Near-Surface Glow", Range(0, 0.3)) = 0.1

        [Header(Eye Adaptation)]
        _AdaptStrength ("Anti-Washout", Range(0, 0.4)) = 0.2

        [Header(Vignette)]
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.35

        [Header(Debug)]
        [KeywordEnum(Off, Fog, Distortion, Absorption, Caustics, Depth)]
        _DebugMode ("Debug View", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "UnderwaterOverlay"
            ZWrite Off
            ZTest  Always
            Cull   Off
            Blend  Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment UnderwaterFrag

            // Feature toggles — enable in material Inspector or via script.
            // Disabled keywords skip their code block entirely.
            #pragma shader_feature_local _DISTORTION_ON
            #pragma shader_feature_local _CAUSTICS_ON
            #pragma shader_feature_local _FOG_ON
            #pragma shader_feature_local _LIGHTSHAFTS_ON
            #pragma shader_feature_local _DEPTHBLUR_ON
            #pragma shader_feature_local _CHROMATIC_ON

            // Debug view modes
            #pragma shader_feature_local _DEBUGMODE_OFF _DEBUGMODE_FOG _DEBUGMODE_DISTORTION _DEBUGMODE_ABSORPTION _DEBUGMODE_CAUSTICS _DEBUGMODE_DEPTH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ---------------------------------------------------------------
            //  Globals pushed each frame by UnderwaterVolumeController
            // ---------------------------------------------------------------
            half  _UnderwaterFactor;
            half  _UnderwaterDepth;

            // ---------------------------------------------------------------
            //  Material properties
            // ---------------------------------------------------------------
            half4  _UnderwaterTint;
            half   _TintStrength;
            half   _AbsorptionR;
            half   _AbsorptionG;
            half   _AbsorptionB;
            half   _FogDensity;
            half   _FogStartDepth;
            half   _DistortionAmount;
            half   _DistortionSpeed;
            half   _DistortionScale;
            half   _ChromaStrength;
            half   _CausticsTiling;
            half   _CausticsSpeed;
            half   _CausticsIntensity;
            half   _CausticsDrift;
            half   _ShaftIntensity;
            half4  _ShaftDir;
            half   _BlurMaxDepth;
            half   _BlurAmount;
            half   _SurfaceBrightness;
            half   _AdaptStrength;
            half   _VignetteStrength;

            TEXTURE2D(_CausticsMap);
            SAMPLER(sampler_CausticsMap);

            // ===============================================================
            //  Helper functions
            // ===============================================================

            // 1. Distortion — sinusoidal warp, strongest near the surface,
            //    with view-angle approximation for grazing-angle realism.
            half2 ComputeDistortion(half2 uv, half factor, half depth)
            {
                half distortDepthFade = 1.0h - saturate(depth / 1.5h);
                // Approximate view-angle fade: screen edges look more
                // through the surface plane than the centre.
                half2 centred = uv - 0.5h;
                half angleFade = 1.0h - saturate(dot(centred, centred) * 2.0h);
                half wt = _Time.y * _DistortionSpeed;
                half2 d = half2(
                    sin(uv.y * _DistortionScale * TWO_PI + wt)       * _DistortionAmount,
                    cos(uv.x * _DistortionScale * TWO_PI + wt * 0.7h) * _DistortionAmount * 0.5h
                ) * factor * distortDepthFade * angleFade;
                return d;
            }

            // 2. Chromatic aberration — splits RGB along the distort vector.
            half3 SampleChromatic(half2 uv, half2 distort, half factor)
            {
                half2 offset = distort * _ChromaStrength;
                half r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, saturate(uv + offset)).r;
                half g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, saturate(uv)).g;
                half b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, saturate(uv - offset)).b;
                return half3(r, g, b);
            }

            // 3. Wavelength-dependent colour absorption.
            half3 ApplyAbsorption(half3 col, half factor, half depth)
            {
                half3 absorption = half3(_AbsorptionR, _AbsorptionG, _AbsorptionB);
                half3 attenuated = col * exp(-absorption * depth);
                return lerp(col, attenuated, factor * _TintStrength);
            }

            // 4. Exponential depth fog.
            half3 ApplyFog(half3 col, half factor, half depth)
            {
                half depthBelowStart = max(0.0h, depth - _FogStartDepth);
                half fogT = 1.0h - exp(-depthBelowStart * _FogDensity);
                return lerp(col, _UnderwaterTint.rgb * 0.35h, fogT * factor);
            }

            // 5. Caustics — world-drift anchored so they feel less
            //    screen-stuck. Single sample with RG packed min.
            half3 ApplyCaustics(half3 col, half2 uv, half factor, half depth)
            {
                half causticsDepthFade = 1.0h - saturate(depth / 8.0h);
                half vis = factor * causticsDepthFade;

                // Slow world-space drift breaks the screen-stuck feeling.
                half2 drift = half2(_Time.y * _CausticsDrift, _Time.y * _CausticsDrift * 0.8h);
                half2 cauUV = (uv + drift) * _CausticsTiling;

                half2 cauUV1 = cauUV + _Time.y * _CausticsSpeed * half2( 1.0h,  0.7h);
                half2 cauUV2 = cauUV + _Time.y * _CausticsSpeed * half2(-0.6h, -1.0h);

                // Two samples min'd for the sharp double-caustic pattern.
                half c1 = SAMPLE_TEXTURE2D(_CausticsMap, sampler_CausticsMap, cauUV1).r;
                half c2 = SAMPLE_TEXTURE2D(_CausticsMap, sampler_CausticsMap, cauUV2).r;
                col += min(c1, c2) * _CausticsIntensity * vis;
                return col;
            }

            // 6. Fake light shafts / god rays.
            half3 ApplyLightShafts(half3 col, half2 uv, half factor, half causticsDepthFade)
            {
                half2 lightDir = normalize(_ShaftDir.xy);
                half shaft = dot(normalize(uv - 0.5h), lightDir);
                shaft = saturate(shaft * 0.5h + 0.5h);
                shaft = shaft * shaft * shaft;   // pow(x, 3)
                col += shaft * _ShaftIntensity * factor * causticsDepthFade;
                return col;
            }

            // 7. Depth blur — simple 2-tap box approximation.
            half3 ApplyDepthBlur(half3 col, half2 uv, half factor, half depth)
            {
                half blurT = saturate(depth / _BlurMaxDepth) * factor;
                half2 offset = blurT * _BlurAmount;
                half3 blur = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, saturate(uv + offset)).rgb
                           + SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, saturate(uv - offset)).rgb;
                return lerp(col, blur * 0.5h, blurT);
            }

            // 8. Surface proximity glow.
            half3 ApplySurfaceGlow(half3 col, half factor, half depth)
            {
                half surfaceFade = saturate(1.0h - depth / 0.5h);
                return col + surfaceFade * _SurfaceBrightness * factor;
            }

            // 9. Eye adaptation (anti-washout).
            half3 ApplyAdaptation(half3 col, half factor)
            {
                half brightness = dot(col, half3(0.2126h, 0.7152h, 0.0722h));
                half adapt = saturate(1.0h - brightness);
                return col * lerp(1.0h, 0.8h, adapt * _AdaptStrength * factor);
            }

            // 10. Vignette — sqrt(sqrt(x)) instead of pow(x, 0.3).
            half3 ApplyVignette(half3 col, half2 uv, half factor)
            {
                half2 vigUV = uv * (1.0h - uv.yx);
                half  raw   = saturate(vigUV.x * vigUV.y * 15.0h);
                half  vig   = sqrt(sqrt(raw));
                return lerp(col, col * vig, _VignetteStrength * factor);
            }

            // ===============================================================
            //  Fragment shader
            // ===============================================================
            half4 UnderwaterFrag(Varyings input) : SV_Target
            {
                half factor = _UnderwaterFactor;
                half depth  = _UnderwaterDepth;

                // Early-out when above water — zero GPU cost.
                UNITY_BRANCH
                if (factor < 0.002h)
                    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);

                half2 uv = input.texcoord;

                // ---- Debug views (return immediately) --------------------
                #if defined(_DEBUGMODE_DEPTH)
                    return half4((half3)saturate(depth / 10.0h), 1.0h);
                #endif

                // --- 1. Distortion + scene sample -------------------------
                half2 distort = half2(0.0h, 0.0h);
                #if defined(_DISTORTION_ON)
                    distort = ComputeDistortion(uv, factor, depth);
                #endif

                #if defined(_DEBUGMODE_DISTORTION)
                    return half4(abs(distort.x) * 200.0h, abs(distort.y) * 200.0h, 0.0h, 1.0h);
                #endif

                // --- Scene colour (with optional chromatic aberration) -----
                half3 sceneRGB;
                #if defined(_CHROMATIC_ON)
                    sceneRGB = SampleChromatic(uv + distort, distort, factor);
                #else
                    sceneRGB = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, saturate(uv + distort)).rgb;
                #endif
                half sceneA = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).a;

                // --- 2. Wavelength absorption -----------------------------
                half3 col = ApplyAbsorption(sceneRGB, factor, depth);

                #if defined(_DEBUGMODE_ABSORPTION)
                    half3 absorption = half3(_AbsorptionR, _AbsorptionG, _AbsorptionB);
                    return half4(exp(-absorption * depth), 1.0h);
                #endif

                // --- 3. Depth fog -----------------------------------------
                #if defined(_FOG_ON)
                    col = ApplyFog(col, factor, depth);
                #endif

                #if defined(_DEBUGMODE_FOG)
                    half dbs = max(0.0h, depth - _FogStartDepth);
                    half ft = 1.0h - exp(-dbs * _FogDensity);
                    return half4((half3)ft, 1.0h);
                #endif

                // --- 4. Caustics ------------------------------------------
                half causticsDepthFade = 1.0h - saturate(depth / 8.0h);
                #if defined(_CAUSTICS_ON)
                    col = ApplyCaustics(col, uv, factor, depth);
                #endif

                #if defined(_DEBUGMODE_CAUSTICS)
                    half2 drift = half2(_Time.y * _CausticsDrift, _Time.y * _CausticsDrift * 0.8h);
                    half2 cUV = (uv + drift) * _CausticsTiling;
                    half2 cUV1 = cUV + _Time.y * _CausticsSpeed * half2(1.0h, 0.7h);
                    half c = SAMPLE_TEXTURE2D(_CausticsMap, sampler_CausticsMap, cUV1).r;
                    return half4((half3)c, 1.0h);
                #endif

                // --- 5. Light shafts --------------------------------------
                #if defined(_LIGHTSHAFTS_ON)
                    col = ApplyLightShafts(col, uv, factor, causticsDepthFade);
                #endif

                // --- 6. Depth blur ----------------------------------------
                #if defined(_DEPTHBLUR_ON)
                    col = ApplyDepthBlur(col, uv, factor, depth);
                #endif

                // --- 7. Surface proximity glow ----------------------------
                col = ApplySurfaceGlow(col, factor, depth);

                // --- 8. Eye adaptation ------------------------------------
                col = ApplyAdaptation(col, factor);

                // --- 9. Vignette ------------------------------------------
                col = ApplyVignette(col, uv, factor);

                return half4(col, sceneA);
            }
            ENDHLSL
        }
    }
}
