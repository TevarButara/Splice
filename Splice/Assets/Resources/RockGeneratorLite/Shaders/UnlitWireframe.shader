Shader "Hidden/Veridian/WireframeOverlayLite"
{
    Properties
    {
        _WireColor ("Wireframe Color", Color) = (1, 1, 1, 1)    
        _WireThickness ("Wire Thickness %", Range(1, 15)) = 5   
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Cull Off
            Offset -1, -1

           HLSLPROGRAM
            #pragma require geometry 
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #if defined(UNITY_PIPELINE_URP) || defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #else
                #include "UnityCG.cginc"
            #endif

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            struct GeometryOutput {
                float4 positionCS : SV_POSITION;
                float3 barycentric : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _WireColor;
                float _WireThickness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                #if defined(UNITY_PIPELINE_URP) || defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
                    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                #else
                    OUT.positionCS = UnityObjectToClipPos(IN.positionOS.xyz);
                #endif
                return OUT;
            }

            [maxvertexcount(3)]
            void geom(triangle Varyings IN[3], inout TriangleStream<GeometryOutput> triStream)
            {
                GeometryOutput o;
                o.positionCS = IN[0].positionCS; o.barycentric = float3(1, 0, 0); triStream.Append(o);
                o.positionCS = IN[1].positionCS; o.barycentric = float3(0, 1, 0); triStream.Append(o);
                o.positionCS = IN[2].positionCS; o.barycentric = float3(0, 0, 1); triStream.Append(o);
            }

            float4 frag(GeometryOutput IN) : SV_Target
            {
                float smallestBary = min(IN.barycentric.x, min(IN.barycentric.y, IN.barycentric.z));
                float thickness = _WireThickness / 100.0;

                if (smallestBary < thickness) return _WireColor;
                
                clip(-1); // Discards non-wireframe pixels so the solid rock renders underneath perfectly
                return float4(0,0,0,0);
            }
            ENDHLSL
        }
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; };
            float4 _WireColor;
            v2f vert (appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); return o; }
            float4 frag (v2f i) : SV_Target { clip(-1); return float4(0,0,0,0); }
            ENDCG
        }
    }
}