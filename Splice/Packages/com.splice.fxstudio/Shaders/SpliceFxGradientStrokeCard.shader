Shader "Splice/FX Studio/Gradient Stroke Card"
{
    Properties
    {
        [MainTexture] _BaseMap("Image", 2D) = "white" {}
        [MainColor] _BaseColor("Main Color", Color) = (1,1,1,1)
        _GradientMap("Gradient", 2D) = "white" {}
        _GradientMode("Gradient Mode", Float) = 0
        _GradientReverse("Reverse Gradient", Float) = 0
        _FxEmission("Emission", Float) = 1
        _StrokeMode("Stroke Mode", Float) = 0
        _StrokeColor("Stroke Color", Color) = (1,0.3,0.05,1)
        _StrokeWidth("Stroke Width", Range(0,16)) = 0
        _StrokeDashFrequency("Dash Frequency", Range(1,32)) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GradientStrokeCard"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GradientMap);
            SAMPLER(sampler_GradientMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseMap_TexelSize;
                half4 _BaseColor;
                half4 _StrokeColor;
                float _GradientMode;
                float _GradientReverse;
                float _FxEmission;
                float _StrokeMode;
                float _StrokeWidth;
                float _StrokeDashFrequency;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(
                    input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            float GradientCoordinate(float2 uv)
            {
                float value = uv.y;
                if (_GradientMode > 1.5 && _GradientMode < 2.5)
                    value = uv.x;
                else if (_GradientMode > 2.5)
                    value = saturate(
                        length(uv - float2(0.5, 0.5)) * 2.0);
                if (_GradientMode > 3.5)
                    value = 1.0 - value;
                if (_GradientReverse > 0.5)
                    value = 1.0 - value;
                return saturate(value);
            }

            half AlphaAt(float2 uv)
            {
                return SAMPLE_TEXTURE2D(
                    _BaseMap, sampler_BaseMap,
                    saturate(uv)).a;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(
                    _BaseMap, sampler_BaseMap, input.uv);
                half4 gradient = half4(1, 1, 1, 1);
                if (_GradientMode > 0.5)
                {
                    float coordinate = GradientCoordinate(input.uv);
                    gradient = SAMPLE_TEXTURE2D(
                        _GradientMap, sampler_GradientMap,
                        float2(coordinate, 0.5));
                }

                half sourceAlpha =
                    source.a * _BaseColor.a * gradient.a *
                    input.color.a;
                // Solid mode preserves the source image and uses Main Color
                // as a tint. Gradient modes replace that tint and retain only
                // a small amount of source luminance for image detail.
                half luminance = dot(source.rgb,
                    half3(0.2126h, 0.7152h, 0.0722h));
                half detail = lerp(1.0h, luminance, 0.35h);
                half3 coloredSource = _GradientMode > 0.5
                    ? gradient.rgb * detail
                    : source.rgb * _BaseColor.rgb;
                half3 sourceRgb =
                    coloredSource * input.color.rgb *
                    max(0.0, _FxEmission);

                half stroke = 0;
                if (_StrokeMode > 0.5 && _StrokeWidth > 0.001)
                {
                    float2 pixel =
                        _BaseMap_TexelSize.xy * _StrokeWidth;
                    half nearby = 0;
                    nearby = max(nearby,
                        AlphaAt(input.uv + float2(pixel.x, 0)));
                    nearby = max(nearby,
                        AlphaAt(input.uv - float2(pixel.x, 0)));
                    nearby = max(nearby,
                        AlphaAt(input.uv + float2(0, pixel.y)));
                    nearby = max(nearby,
                        AlphaAt(input.uv - float2(0, pixel.y)));
                    nearby = max(nearby,
                        AlphaAt(input.uv + pixel));
                    nearby = max(nearby,
                        AlphaAt(input.uv - pixel));
                    nearby = max(nearby,
                        AlphaAt(input.uv + float2(pixel.x, -pixel.y)));
                    nearby = max(nearby,
                        AlphaAt(input.uv + float2(-pixel.x, pixel.y)));
                    stroke = saturate(nearby - source.a);

                    if (_StrokeMode > 1.5 && _StrokeMode < 2.5)
                        stroke *= 0.55;
                    else if (_StrokeMode > 2.5)
                    {
                        float angle = atan2(
                            input.uv.y - 0.5,
                            input.uv.x - 0.5);
                        stroke *= step(0.0,
                            sin(angle * _StrokeDashFrequency));
                    }
                }

                half strokeAlpha = stroke * _StrokeColor.a;
                half outputAlpha =
                    saturate(sourceAlpha + strokeAlpha);
                half3 premultiplied =
                    sourceRgb * sourceAlpha +
                    _StrokeColor.rgb * strokeAlpha;
                half3 outputRgb = premultiplied /
                    max(outputAlpha, 0.0001h);
                return half4(outputRgb, outputAlpha);
            }
            ENDHLSL
        }
    }
}
