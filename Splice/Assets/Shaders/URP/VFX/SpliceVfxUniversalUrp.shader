Shader "Splice/VFX/URP Universal"
{
    Properties
    {
        [Enum(Game3D,0,UI,1,Sprite2D,2)] _ApplicationMode("Application Mode", Float) = 0
        [Enum(Transparent,0,Additive,1,SoftAdditive,2,Blend,3,Opaque,4,Other,5)] _RenderingMode("Rendering Mode", Float) = 3
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestMode("Z Test Mode", Float) = 4
        [HideInInspector] _ZWrite("ZWrite", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.1
        _GlobalAlpha("Global Alpha", Range(0.0, 1.0)) = 1.0
        _AlphaFactor("Alpha Factor", Float) = 1.0
        _MainTex("Main Texture", 2D) = "white" {}
        _MainTexSpeedX("Main Texture Speed X", Float) = 0.0
        _MainTexSpeedY("Main Texture Speed Y", Float) = 0.0
        [HDR] _TintColor("Tint Color", Color) = (0.5,0.5,0.5,1)
        _MainTexBrightness("Main Texture Brightness", Range(0, 4)) = 1.0
        _MainTexContrast("Main Texture Contrast", Range(0, 2)) = 1.0
        _DualColorsState("Dual Colors State", Float) = 0.0
        [HDR] _ColorA("Color A", Color) = (1,0,0,1)
        [HDR] _ColorB("Color B", Color) = (0,0,1,1)
        _ColorThreshold("Color Threshold", Range(0, 1)) = 0.5
        _ColorSmoothness("Color Smoothness", Range(0, 1)) = 0.1
        _MaskTex("Mask Texture", 2D) = "white" {}
        _MaskRotation("Mask Rotation", Range(0, 360)) = 0.0
        _MaskFlowTex("Mask Flow Texture", 2D) = "white" {}
        _MaskFlowSpeed("Mask Flow Speed", Vector) = (0, 0, 0, 0)
        _MaskFlowStrength("Mask Flow Strength", Range(0, 10)) = 0.5
        _MaskFlowSmoothness("Mask Flow Smoothness", Range(0.02, 1)) = 0.1
        _MaskNoiseTex("Mask Noise Texture", 2D) = "white" {}
        _MaskNoiseSpeed("Mask Noise Speed", Vector) = (0, 0, 0, 0)
        _MaskNoiseIntensity("Mask Noise Intensity", Range(0, 1)) = 0.25
        _DissolveTex("Dissolve Texture", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0.0, 1.01)) = 0.1
        _DissolveSmoothness("Dissolve Smoothness", Range(0.0, 1.0)) = 0.1
        _DissolveRemapMin("Dissolve Remap Min", Range(0.0, 1.0)) = 0.0
        _DissolveRemapMax("Dissolve Remap Max", Range(0.0, 1.0)) = 1.0
        _DissolveOutlineStep("Dissolve Outline Step", Range(0.0, 3.0)) = 0.1
        [HDR] _DissolveOutlineColor("Dissolve Outline Color", Color) = (1, 1, 1, 1)
        _UVNoise("UV Noise", 2D) = "black" {}
        _UVNoiseBias("UV Noise Bias", Range(-1, 1)) = 0.6
        _UVNoiseIntensity("UV Noise Intensity", Range(0, 1)) = 0.5
        _UVNoiseSpeed("UV Noise Speed", Vector) = (0, 0, 0, 0)
        _DistortionTex("Distortion Texture", 2D) = "gray" {}
        _DistortionIntensity("Distortion Intensity", Float) = 0.5
        _DistortionSpeed("Distortion Speed", Vector) = (0.1,0.1,0,0)
        _GlowTex("Glow Texture", 2D) = "black" {}
        _GlowSpeedX("Glow Speed X", Float) = 0.0
        _GlowSpeedY("Glow Speed Y", Float) = 0.0
        [HDR] _GlowColor("Glow Color", Color) = (1, 1, 1, 1)
        _GlowBlinkMinAlpha("Glow Blink Min Alpha", Range(0.0, 1.0)) = 0.2
        _GlowBlinkMaxAlpha("Glow Blink Max Alpha", Range(0.0, 1.0)) = 1.0
        _GlowBlinkSpeed("Glow Blink Speed", Float) = 1.0
        [HDR] _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimIntensity("Rim Intensity", Range(0, 10)) = 1
        _RimFresnel("Rim Fresnel", Range(0, 5)) = 1
        [HDR] _RimLightColor("Rim Light Color", Color) = (1,1,1,1)
        _RimLightIntensity("Rim Light Intensity", Range(0, 10)) = 1
        _RimLightFresnel("Rim Light Fresnel", Range(0, 5)) = 1
        _OffsetNoiseTex("Offset Noise Texture", 2D) = "white" {}
        _OffsetAmount("Offset Amount", Range(0, 2)) = 0.0
        _OffsetPower("Offset Power", Range(0, 5)) = 1.0
        _ScrollSpeedX("Scroll Speed X", Float) = 0.0
        _ScrollSpeedY("Scroll Speed Y", Float) = 0.0
        _ShineMask("Shine Mask", 2D) = "white" {}
        [HDR] _ShineColor("Shine Color", Color) = (3,3,3,1)
        _ShineIntensity("Shine Intensity", Float) = 1.0
        _ShineWidth("Shine Width", Float) = 1.0
        _ShineRotation("Shine Direction", Vector) = (1,0,0,0)
        _ShineWorldDirection("World Direction", Vector) = (1,0,0,0)
        _ShineWorldSpace("Use World Space", Float) = 0
        _ShineSpeed("Shine Speed", Float) = 1.0
        _ShineSharpnessLeft("Left Edge Sharpness", Float) = 5.0
        _ShineSharpnessRight("Right Edge Sharpness", Float) = 3.0
        _ShineDelay("Shine Delay", Float) = 0.0
        _ColorMask("Color Mask", Float) = 15
        _InvFade("Soft Particles Factor", Range(0.01, 5.0)) = 1.0
        _Alpha("Alpha", Range(0.0, 1.0)) = 1.0
        _GrayscaleAlphaPower("Grayscale Alpha Power", Range(0.1, 5.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTestMode]
            Cull [_CullMode]
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local UI_MODE
            #pragma shader_feature_local _ALPHA_TEST
            #pragma shader_feature_local ALPHA_FROM_GRAYSCALE
            #pragma shader_feature_local ENABLE_DUAL_COLORS
            #pragma shader_feature_local ENABLE_MASK
            #pragma shader_feature_local ENABLE_MASK_FLOW
            #pragma shader_feature_local ENABLE_MASK_NOISE
            #pragma shader_feature_local ENABLE_DISSOLVE
            #pragma shader_feature_local ENABLE_DISSOLVE_VERTEX_COLOR
            #pragma shader_feature_local ENABLE_DISSOLVE_OUTLINE
            #pragma shader_feature_local ENABLE_DISSOLVE_REMAP
            #pragma shader_feature_local ENABLE_UV_NOISE
            #pragma shader_feature_local ENABLE_PARTICLE_UV_ANIMATION
            #pragma shader_feature_local ENABLE_GLOW
            #pragma shader_feature_local ENABLE_GLOW_BLINK
            #pragma shader_feature_local ENABLE_RIM
            #pragma shader_feature_local ENABLE_RIM_LIGHT
            #pragma shader_feature_local ENABLE_VERTEX_OFFSET
            #pragma shader_feature_local ENABLE_SHINE
            #pragma shader_feature_local SHINE_WORLD_SPACE
            #pragma shader_feature_local ENABLE_SOFTPARTICLES
            #pragma shader_feature_local ENABLE_DISTORTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_OffsetNoiseTex); SAMPLER(sampler_OffsetNoiseTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            TEXTURE2D(_MaskFlowTex); SAMPLER(sampler_MaskFlowTex);
            TEXTURE2D(_MaskNoiseTex); SAMPLER(sampler_MaskNoiseTex);
            TEXTURE2D(_DissolveTex); SAMPLER(sampler_DissolveTex);
            TEXTURE2D(_DistortionTex); SAMPLER(sampler_DistortionTex);
            TEXTURE2D(_ShineMask); SAMPLER(sampler_ShineMask);
            TEXTURE2D(_UVNoise); SAMPLER(sampler_UVNoise);
            TEXTURE2D(_GlowTex); SAMPLER(sampler_GlowTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _OffsetNoiseTex_ST, _MaskTex_ST, _MaskFlowTex_ST;
                float4 _MaskNoiseTex_ST, _DissolveTex_ST, _DistortionTex_ST;
                float4 _ShineMask_ST, _UVNoise_ST, _GlowTex_ST;
                half4 _TintColor, _ColorA, _ColorB, _DissolveOutlineColor, _GlowColor;
                half4 _RimColor, _RimLightColor, _ShineColor;
                float4 _MaskFlowSpeed, _MaskNoiseSpeed, _UVNoiseSpeed, _DistortionSpeed;
                float4 _ShineRotation, _ShineWorldDirection;
                float _MainTexSpeedX, _MainTexSpeedY, _MainTexBrightness, _MainTexContrast;
                float _ColorThreshold, _ColorSmoothness, _MaskRotation;
                float _MaskFlowStrength, _MaskFlowSmoothness, _MaskNoiseIntensity;
                float _DissolveAmount, _DissolveSmoothness, _DissolveRemapMin, _DissolveRemapMax;
                float _DissolveOutlineStep, _UVNoiseBias, _UVNoiseIntensity;
                float _DistortionIntensity, _GlowSpeedX, _GlowSpeedY;
                float _GlowBlinkMinAlpha, _GlowBlinkMaxAlpha, _GlowBlinkSpeed;
                float _RimIntensity, _RimFresnel, _RimLightIntensity, _RimLightFresnel;
                float _OffsetAmount, _OffsetPower, _ScrollSpeedX, _ScrollSpeedY;
                float _ShineIntensity, _ShineWidth, _ShineWorldSpace, _ShineSpeed;
                float _ShineSharpnessLeft, _ShineSharpnessRight, _ShineDelay;
                float _AlphaFactor, _GlobalAlpha, _Alpha, _InvFade, _GrayscaleAlphaPower, _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 uv : TEXCOORD0;
                half4 color : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 uvData : TEXCOORD0;
                half4 effectsUV : TEXCOORD1;
                half4 packedData : TEXCOORD2;
                half3 worldNormal : TEXCOORD3;
                half4 color : COLOR0;
                float4 screenPos : TEXCOORD4;
                float4 customData : TEXCOORD5;
                half2 shineUV : TEXCOORD6;
                float3 worldPos : TEXCOORD7;
                half2 distortionUV : TEXCOORD8;
                float eyeDepth : TEXCOORD9;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionOS = input.positionOS.xyz;

                #ifdef ENABLE_VERTEX_OFFSET
                    float2 noiseUV = input.uv.xy * _OffsetNoiseTex_ST.xy + _OffsetNoiseTex_ST.zw;
                    noiseUV += _Time.y * float2(_ScrollSpeedX, _ScrollSpeedY);
                    float noiseSample = SAMPLE_TEXTURE2D_LOD(
                        _OffsetNoiseTex, sampler_OffsetNoiseTex, noiseUV, 0).r;
                    float offset = pow(max(noiseSample, 0.0001), _OffsetPower) * _OffsetAmount;
                    #ifdef UI_MODE
                        offset *= 0.1;
                    #endif
                    positionOS += input.normalOS * offset;
                #endif

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionCS = positionInputs.positionCS;
                output.worldPos = positionInputs.positionWS;
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(positionInputs.positionWS).z;
                half2 mainUV = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                #ifdef ENABLE_PARTICLE_UV_ANIMATION
                    mainUV += input.uv.zw;
                #endif
                mainUV += frac(_Time.y * half2(_MainTexSpeedX, _MainTexSpeedY));
                output.uvData.xy = mainUV;
                output.uvData.zw = input.uv.xy * _UVNoise_ST.xy + _UVNoise_ST.zw;
                output.effectsUV.xy = input.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
                output.effectsUV.zw = input.uv.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
                output.packedData.xy = input.uv.xy * _GlowTex_ST.xy + _GlowTex_ST.zw;
                output.packedData.zw = input.uv.xy * _MaskFlowTex_ST.xy + _MaskFlowTex_ST.zw;
                output.customData = float4(
                    input.uv.zw,
                    input.uv.xy * _MaskNoiseTex_ST.xy + _MaskNoiseTex_ST.zw);
                output.shineUV = input.uv.xy * _ShineMask_ST.xy + _ShineMask_ST.zw;
                output.distortionUV = input.uv.xy * _DistortionTex_ST.xy + _DistortionTex_ST.zw;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                #if defined(_ALPHA_TEST) && !defined(ALPHA_FROM_GRAYSCALE)
                    if (input.color.a * _TintColor.a < _Cutoff * 0.3h) discard;
                #endif
                half4 originalColor;
                #ifdef ENABLE_UV_NOISE
                    half2 noiseUv = input.uvData.zw + _Time.y * _UVNoiseSpeed.zw;
                    half2 uvDistort = (_UVNoiseBias +
                        SAMPLE_TEXTURE2D(_UVNoise, sampler_UVNoise, noiseUv).rg) * _UVNoiseIntensity;
                    originalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvData.xy + uvDistort);
                #else
                    originalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvData.xy);
                #endif
                half4 color = originalColor;
                half luminance = dot(originalColor.rgb, half3(0.299h, 0.587h, 0.114h));
                half processedLuminance = saturate((luminance - 0.5h) * _MainTexContrast + 0.5h);
                processedLuminance *= _MainTexBrightness;
                #ifdef ENABLE_DUAL_COLORS
                    half blendFactor = smoothstep(
                        _ColorThreshold - _ColorSmoothness,
                        _ColorThreshold + _ColorSmoothness,
                        processedLuminance);
                    color.rgb = lerp(_ColorA.rgb, _ColorB.rgb, blendFactor) * input.color.rgb;
                #else
                    color.rgb = saturate((originalColor.rgb - 0.5h) * _MainTexContrast + 0.5h);
                    color.rgb *= _MainTexBrightness * input.color.rgb * _TintColor.rgb;
                #endif
                color.a = originalColor.a * input.color.a * _TintColor.a * _AlphaFactor;
                #ifdef ALPHA_FROM_GRAYSCALE
                    color.a *= smoothstep(
                        0.05h, 1.0h, saturate(pow(luminance, _GrayscaleAlphaPower)));
                #endif
                #ifdef _ALPHA_TEST
                    clip(color.a - _Cutoff);
                #endif

                #ifdef ENABLE_MASK
                    half2 maskUv = input.effectsUV.xy;
                    #ifdef ENABLE_MASK_NOISE
                        half2 maskNoiseUv = input.customData.zw + _Time.y * _MaskNoiseSpeed.xy;
                        half noise = SAMPLE_TEXTURE2D(
                            _MaskNoiseTex, sampler_MaskNoiseTex, maskNoiseUv).r - 0.5h;
                        float2 centeredNoise = maskUv - 0.5;
                        centeredNoise += normalize(centeredNoise + 0.00001) *
                            noise * _MaskNoiseIntensity * 0.25h;
                        maskUv = centeredNoise + 0.5;
                    #endif
                    #ifdef ENABLE_DISTORTION
                        half2 distortionUv = input.distortionUV + _Time.y * _DistortionSpeed.xy;
                        half4 distortionSample = SAMPLE_TEXTURE2D(
                            _DistortionTex, sampler_DistortionTex, distortionUv);
                        half distortion = (distortionSample.r - distortionSample.g) * _DistortionIntensity;
                        float2 centeredDistortion = maskUv - 0.5;
                        centeredDistortion += normalize(centeredDistortion + 0.00001) *
                            distortion * length(centeredDistortion) * 0.15;
                        maskUv = centeredDistortion + 0.5;
                    #endif
                    float radiansValue = _MaskRotation * 0.01745329252;
                    float sineValue;
                    float cosineValue;
                    sincos(radiansValue, sineValue, cosineValue);
                    float2 centeredMask = maskUv - 0.5;
                    maskUv = float2(
                        cosineValue * centeredMask.x - sineValue * centeredMask.y,
                        sineValue * centeredMask.x + cosineValue * centeredMask.y) + 0.5;
                    half baseMask = dot(
                        SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUv).rgb,
                        half3(0.299h, 0.587h, 0.114h));
                    #ifdef ENABLE_MASK_FLOW
                        half2 flowUv = input.packedData.zw + _Time.y * _MaskFlowSpeed.xy;
                        float2 centeredFlow = flowUv - 0.5;
                        flowUv = float2(
                            cosineValue * centeredFlow.x - sineValue * centeredFlow.y,
                            sineValue * centeredFlow.x + cosineValue * centeredFlow.y) + 0.5;
                        half flowMask = dot(
                            SAMPLE_TEXTURE2D(_MaskFlowTex, sampler_MaskFlowTex, flowUv).rgb,
                            half3(0.299h, 0.587h, 0.114h));
                        color.a *= smoothstep(
                            0.0h,
                            max(_MaskFlowSmoothness, 0.0001h),
                            baseMask - flowMask * _MaskFlowStrength);
                    #else
                        color.a *= baseMask;
                    #endif
                #endif

                #ifdef ENABLE_DISSOLVE
                    half4 dissolveSample = SAMPLE_TEXTURE2D(
                        _DissolveTex, sampler_DissolveTex, input.effectsUV.zw);
                    half dissolveValue = dissolveSample.a;
                    if (dissolveSample.a > 0.999h)
                        dissolveValue = dot(dissolveSample.rgb, half3(0.299h, 0.587h, 0.114h));
                    #ifdef ENABLE_DISSOLVE_REMAP
                        dissolveValue = saturate(
                            (dissolveValue - _DissolveRemapMin) /
                            max(_DissolveRemapMax - _DissolveRemapMin, 0.0001));
                    #endif
                    half threshold = _DissolveAmount;
                    #ifdef ENABLE_DISSOLVE_VERTEX_COLOR
                        threshold = saturate(input.customData.x);
                    #endif
                    half smoothWidth = _DissolveSmoothness * 0.5h;
                    color.a *= smoothstep(threshold - smoothWidth, threshold + smoothWidth, dissolveValue);
                    #ifdef ENABLE_DISSOLVE_OUTLINE
                        half outlineWidth = _DissolveOutlineStep * 0.1h;
                        half outlineMask =
                            smoothstep(threshold - outlineWidth, threshold, dissolveValue) -
                            smoothstep(threshold, threshold + outlineWidth, dissolveValue);
                        color.rgb = lerp(color.rgb, _DissolveOutlineColor.rgb, outlineMask);
                    #endif
                #endif

                #ifdef ENABLE_GLOW
                    half2 glowUv = input.packedData.xy + half2(_GlowSpeedX, _GlowSpeedY) * _Time.y;
                    half3 glow = SAMPLE_TEXTURE2D(_GlowTex, sampler_GlowTex, glowUv).rgb * _GlowColor.rgb;
                    half glowAlpha = _GlowColor.a;
                    #ifdef ENABLE_GLOW_BLINK
                        half blinkFactor = (_GlowBlinkMaxAlpha - _GlowBlinkMinAlpha) * 0.5h;
                        half blinkBase = (_GlowBlinkMaxAlpha + _GlowBlinkMinAlpha) * 0.5h;
                        glowAlpha = saturate(
                            blinkBase + blinkFactor * sin(_Time.y * _GlowBlinkSpeed * 6.28318));
                    #endif
                    color.rgb += color.a * glow * glowAlpha;
                #endif

                #if (defined(ENABLE_RIM) || defined(ENABLE_RIM_LIGHT)) && !defined(UI_MODE)
                    half3 worldNormal = normalize(input.worldNormal);
                    half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.worldPos);
                    half rimDot = dot(viewDirection, worldNormal);
                    #ifdef ENABLE_RIM
                        half rim = pow(1.0h - saturate(abs(rimDot)), _RimFresnel);
                        color.rgb = lerp(color.rgb, _RimColor.rgb * _RimIntensity, rim * color.a);
                    #endif
                    #ifdef ENABLE_RIM_LIGHT
                        half fresnel = pow(1.0h - saturate(abs(rimDot)), _RimLightFresnel);
                        color.rgb += _RimLightColor.rgb * _RimLightIntensity * fresnel * color.a;
                    #endif
                #endif

                #if defined(ENABLE_SOFTPARTICLES) && !defined(UI_MODE)
                    float2 screenUv = input.screenPos.xy / input.screenPos.w;
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                    color.a *= saturate((sceneDepth - input.eyeDepth) * _InvFade);
                #endif

                #ifdef ENABLE_SHINE
                    float totalCycle = 3.0 + _ShineDelay;
                    float rawTime = fmod(_Time.y * _ShineSpeed * 0.5, totalCycle);
                    float movingPoint = rawTime - 1.5;
                    float activePhase = rawTime < 3.0 ? 1.0 : 0.0;
                    float2 rayDirection = normalize(_ShineRotation.xy + 0.00001);
                    float projection = dot(input.shineUV - 0.5, rayDirection) * 2.0;
                    #if defined(SHINE_WORLD_SPACE) && !defined(UI_MODE)
                        float2 shineScreenUv = input.screenPos.xy / input.screenPos.w;
                        rayDirection = normalize(_ShineWorldDirection.xz + 0.00001);
                        projection = dot(shineScreenUv - 0.5, rayDirection) * 2.0;
                    #endif
                    float distanceToPoint = projection - movingPoint;
                    float edgeSharpness = lerp(
                        _ShineSharpnessRight, _ShineSharpnessLeft, distanceToPoint > 0.0);
                    float shine = 1.0 - saturate(
                        abs(distanceToPoint) / max(_ShineWidth * 0.08, 0.0001));
                    shine = pow(shine, edgeSharpness) * activePhase;
                    float mask = SAMPLE_TEXTURE2D(
                        _ShineMask, sampler_ShineMask, input.shineUV).r;
                    float shineValue = shine * mask * input.color.a * _ShineColor.a;
                    color.rgb += _ShineColor.rgb *
                        shineValue * lerp(1.5, 4.0, _ShineIntensity);
                #endif
                color.a *= _Alpha * _GlobalAlpha;
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }

    CustomEditor "GameParticles.Editor.GameParticleShaderGUI"
    Fallback Off
}
