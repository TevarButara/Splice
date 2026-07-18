// น้ำสไตล์การ์ตูน (URP) — สีตื้น/ลึกตามความลึกจริง + ฟองขอบฝั่ง + ระลอกไหล + คลื่นเบาๆ
//   ⚠️ ต้องเปิด "Depth Texture" ใน URP Asset ไม่งั้นความลึก/ฟองจะไม่ทำงาน (น้ำจะเป็นสีเดียวเรียบ)
//   ⚠️ พื้น/ของใต้น้ำต้องมี DepthOnly pass (shader พื้นของเรามีให้แล้ว) ไม่งั้นวัดความลึกไม่เจอ
//   ใส่ _SurfaceNoise = texture noise ขาวดำ tile ได้ (เช่น cloud/voronoi) — ใช้ทำลายฟอง
Shader "Splice/Toon Water"
{
    Properties
    {
        [Header(Color by depth)]
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.85, 0.9, 0.65)
        _DeepColor ("Deep Color", Color) = (0.1, 0.35, 0.6, 0.95)
        _DepthMaxDistance ("Depth Max Distance", Float) = 2

        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (1,1,1,1)
        _FoamDistance ("Foam Distance (foam width from shore)", Float) = 0.5
        _FoamCutoff ("Foam Cutoff", Range(0,1)) = 0.7
        _SurfaceNoise ("Surface Noise (grayscale tileable)", 2D) = "white" {}
        _NoiseTiling ("Noise Tiling", Float) = 6
        _NoiseScroll ("Noise Scroll (XY)", Vector) = (0.03, 0.02, 0, 0)

        [Header(Waves)]
        _WaveAmplitude ("Wave Amplitude", Float) = 0.05
        _WaveFrequency ("Wave Frequency", Float) = 1.5
        _WaveSpeed ("Wave Speed", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;   // w = ระยะจากกล้อง (eye depth ของผิวน้ำ)
                float  fogCoord    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_SurfaceNoise); SAMPLER(sampler_SurfaceNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _NoiseScroll;
                float _DepthMaxDistance;
                float _FoamDistance;
                float _FoamCutoff;
                float _NoiseTiling;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // คลื่นเบาๆ: ยก y ตามคลื่นไซน์ 2 ทิศ (คิดจาก world pos → ผิวน้ำหลายชิ้นต่อกันไม่มีรอยต่อ)
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;
                posWS.y += (sin(posWS.x * _WaveFrequency + t) + cos(posWS.z * _WaveFrequency * 0.8 + t * 1.3))
                           * 0.5 * _WaveAmplitude;

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.uv = IN.uv;
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // --- ความลึกน้ำ = ระยะของพื้นใต้น้ำ − ระยะของผิวน้ำ ---
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float sceneRaw = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                float depthDiff = sceneEye - IN.screenPos.w;

                // สีตื้น→ลึก
                float depth01 = saturate(depthDiff / max(0.001, _DepthMaxDistance));
                half4 water = lerp(_ShallowColor, _DeepColor, depth01);

                // --- ฟองริมฝั่ง: ยิ่งตื้นยิ่งเกิดฟองง่าย + ตัดขอบด้วย noise ที่ไหล ---
                float foam01 = saturate(depthDiff / max(0.001, _FoamDistance));
                float cutoff = foam01 * _FoamCutoff;
                float2 noiseUV = IN.uv * _NoiseTiling + _NoiseScroll.xy * _Time.y;
                float noise = SAMPLE_TEXTURE2D(_SurfaceNoise, sampler_SurfaceNoise, noiseUV).r;
                float foam = smoothstep(cutoff - 0.02, cutoff + 0.02, noise);

                half4 col = lerp(water, _FoamColor, foam * _FoamColor.a);
                col.a = lerp(water.a, 1, foam);

                col.rgb = MixFog(col.rgb, IN.fogCoord);
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
