Shader "Splice/VFX/URP Additive Intensify"
{
    Properties
    {
        [HDR] _TintColor("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        _MainTex("Particle Texture", 2D) = "white" {}
        _InvFade("Soft Particles Factor", Range(0.01,3.0)) = 1.0
        _Glow("Intensity", Range(0, 5)) = 1
        [Toggle] _UseSoftParticles("Use Soft Particles", Float) = 1
        [HideInInspector] _ZWrite("ZWrite", Float) = 0
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
            Blend SrcAlpha One
            ColorMask RGB
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half _Glow;
                half _UseSoftParticles;
                float _InvFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(positionInputs.positionWS).z;
                output.color = input.color * _TintColor;
                output.color.rgb *= _Glow;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = 2.0h * input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_UseSoftParticles > 0.5h)
                {
                    float2 screenUv = input.screenPos.xy / input.screenPos.w;
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
                    color.a *= saturate(_InvFade * (sceneDepth - input.eyeDepth));
                }
                color.rgb = MixFogColor(color.rgb, half3(0, 0, 0), input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
