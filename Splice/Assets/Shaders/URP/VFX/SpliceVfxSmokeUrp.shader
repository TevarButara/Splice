Shader "Splice/VFX/URP Procedural Smoke"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Frequency("Frequency", Float) = 0.5
        _Phase("Phase", Float) = 0.0
        _Amplitude("Amplitude", Float) = 0.1
        _Frequency_2("Frequency_2", Float) = 0.5
        _Phase_2("Phase_2", Float) = 0.0
        _Amplitude_2("Amplitude_2", Float) = 0.1
        _Frequency_3("Frequency_3", Float) = 0.5
        _Phase_3("Phase_3", Float) = 0.0
        _Amplitude_3("Amplitude_3", Float) = 0.1
        _RotationAngle("Rotation Angle (Degrees)", Float) = 0.0
        [HDR] _SmokeColor("Smoke Color", Color) = (1,1,1,1)
        _Transparency("Transparency", Range(0, 1)) = 1.0
        _ColorIntensity("Color Intensity", Range(0, 5)) = 1.0
        _UVOffsetX("UV Offset X", Range(-1, 1)) = 0.0
        _UVOffsetY("UV Offset Y", Range(-1, 1)) = 0.0
        _RandomSeed("Random Seed", Range(0, 100)) = 0.0
        [HideInInspector] _ZWrite("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
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
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Frequency;
                float _Phase;
                float _Amplitude;
                float _Frequency_2;
                float _Phase_2;
                float _Amplitude_2;
                float _Frequency_3;
                float _Phase_3;
                float _Amplitude_3;
                float _RotationAngle;
                half4 _SmokeColor;
                half _Transparency;
                half _ColorIntensity;
                float _UVOffsetX;
                float _UVOffsetY;
                float _RandomSeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float2 RotateUv(float2 uv, float angleDegrees)
            {
                float radiansValue = angleDegrees * 0.01745329252;
                float2 centered = uv - 0.5;
                float sineValue;
                float cosineValue;
                sincos(radiansValue, sineValue, cosineValue);
                return float2(
                    centered.x * cosineValue - centered.y * sineValue,
                    centered.x * sineValue + centered.y * cosineValue) + 0.5;
            }

            float CalculateOffset(float frequency, float phase, float2 uv)
            {
                float x = uv.y * frequency + phase - _Time.y * 2.0 + _RandomSeed;
                float a = frac(x) * 2.0 - 1.0;
                float circle = sqrt(saturate(1.0 - a * a));
                float signValue = smoothstep(0.5, 0.5, frac(x * 0.5)) * 2.0 - 1.0;
                return circle * signValue;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 warpedUv = input.uv + float2(_UVOffsetX, _UVOffsetY);
                warpedUv = RotateUv(warpedUv, _RotationAngle);
                float totalOffset =
                    CalculateOffset(_Frequency, _Phase, warpedUv) * warpedUv.y * _Amplitude +
                    CalculateOffset(_Frequency_2, _Phase_2, warpedUv) * warpedUv.y * _Amplitude_2 +
                    CalculateOffset(_Frequency_3, _Phase_3, warpedUv) * warpedUv.y * _Amplitude_3;

                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    float2(input.uv.x + totalOffset, input.uv.y));
                half luminance = dot(color.rgb, half3(0.299h, 0.587h, 0.114h));
                color.rgb *= _SmokeColor.rgb * _ColorIntensity;
                color.a = luminance * _SmokeColor.a * _Transparency;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
