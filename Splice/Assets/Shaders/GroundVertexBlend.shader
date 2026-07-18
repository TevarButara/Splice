// พื้นแมป: ผสมได้ถึง 4 texture ด้วย "vertex color" (ทาด้วย Polybrush / Blender Vertex Paint)
//   vertex color ดำ (0,0,0) = Base          → ทา R = Layer R / ทา G = Layer G / ทา B = Layer B
//   ⚠️ mesh ต้องมี vertex color และ "init เป็นสีดำ" ไม่งั้น (default = ขาว) จะกลายเป็น Layer B ทั้งแมป
//   ⚠️ ความละเอียดของการไล่สี = ความถี่ของ mesh (ต้อง subdivide เป็นตาราง ไม่ใช่ Plane 4 จุด)
// ลุคการ์ตูน 2 ระดับ (toon ramp) + รับเงา + fog. sample 4 ครั้ง = เบา มือถือไหว
// Normal map = ออปชัน (ติ๊ก Use Normal Maps) — ปิดไว้ = ไม่ sample เพิ่ม ไม่กินเลย (keyword-gated)
Shader "Splice/Ground Vertex Blend"
{
    Properties
    {
        // หมายเหตุ: ข้อความใน [Header()] ห้ามมี - ( ) . หรืออักขระพิเศษ (ShaderLab parse ไม่ผ่าน) ใช้ได้แค่ตัวอักษร/ตัวเลข/ช่องว่าง
        [Header(Layers   paint vertex color to blend)]
        _BaseMap ("Base (vertex black)", 2D) = "white" {}
        _BaseTiling ("Base Tiling", Float) = 8
        _RMap ("Layer R (paint red)", 2D) = "white" {}
        _RTiling ("R Tiling", Float) = 8
        _GMap ("Layer G (paint green)", 2D) = "white" {}
        _GTiling ("G Tiling", Float) = 8
        _BMap ("Layer B (paint blue)", 2D) = "white" {}
        _BTiling ("B Tiling", Float) = 8
        // layer ที่ 5 ใช้ช่อง Alpha ของ vertex color — ปิดเป็น default เพราะ mesh เก่า/จาก Blender มัก alpha=1 ทั้งตัว
        // (จะกลายเป็น layer A เต็มแมปทันทีถ้าเปิดโดยไม่ repaint) เปิดแล้วให้กด Fill Base ในตัวทาก่อน
        [ToggleUI] _UseALayer ("Enable Layer A (5th, vertex alpha)", Float) = 0
        _AMap ("Layer A (paint alpha)", 2D) = "white" {}
        _ATiling ("A Tiling", Float) = 8

        [Header(Normal maps   optional)]
        [Toggle(_NORMALMAP)] _UseNormalMaps ("Use Normal Maps", Float) = 0
        [Normal] _BaseNormal ("Base Normal", 2D) = "bump" {}
        [Normal] _RNormal ("Layer R Normal", 2D) = "bump" {}
        [Normal] _GNormal ("Layer G Normal", 2D) = "bump" {}
        [Normal] _BNormal ("Layer B Normal", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1

        [Header(UV)]
        // เปิด = ฉาย texture จากพิกัดโลก (แกน XZ มองจากบน) ไม่ใช้ UV ของ mesh เลย — พื้นที่ปั้น/subdivide มา
        // มักไม่มี UV ที่ดี (uv กองจุดเดียว → เห็นเป็นสีล้วนไม่มีลาย). world UV = ลายสม่ำเสมอทั้งแมป ทุก mesh
        // ใช้ [ToggleUI] (ค่า float ล้วน ไม่ใช่ keyword) — keyword มีกับดัก material เก่าไม่ติด keyword ให้เอง
        [ToggleUI] _WorldUV ("World UV XZ (ignore mesh UV)", Float) = 1
        _WorldUVScale ("World UV Scale (world units per tile at tiling 1)", Float) = 10

        [Header(Lighting)]
        // Toon = ไล่แสง 2 ระดับ / Smooth = ไล่แสงนุ่ม (ไม่แบ่งแถบ เข้ากับพื้น painterly) / Unlit = texture ดิบๆ ตาม tile ไม่มีแสงเลย
        [KeywordEnum(Toon, Smooth, Unlit)] _LIGHTING ("Lighting Mode", Float) = 0
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.55, 0.6, 0.78, 1)
        _RampThreshold ("Toon Threshold", Range(0,1)) = 0.4
        _RampSmoothness ("Toon Smoothness", Range(0.001, 0.5)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _LIGHTING_TOON _LIGHTING_SMOOTH _LIGHTING_UNLIT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;      // vertex color = mask ผสม layer
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                #ifdef _NORMALMAP
                    float4 tangentWS   : TEXCOORD4;   // w = sign ของ bitangent
                    float3 bitangentWS : TEXCOORD5;
                #endif
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RMap);    SAMPLER(sampler_RMap);
            TEXTURE2D(_GMap);    SAMPLER(sampler_GMap);
            TEXTURE2D(_BMap);    SAMPLER(sampler_BMap);
            TEXTURE2D(_AMap);    SAMPLER(sampler_AMap);
            #ifdef _NORMALMAP
                TEXTURE2D(_BaseNormal); SAMPLER(sampler_BaseNormal);
                TEXTURE2D(_RNormal);    SAMPLER(sampler_RNormal);
                TEXTURE2D(_GNormal);    SAMPLER(sampler_GNormal);
                TEXTURE2D(_BNormal);    SAMPLER(sampler_BNormal);
            #endif

            // ทุก property ต้องอยู่ใน CBUFFER นี้ ไม่งั้น SRP Batcher ไม่ทำงาน
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float _BaseTiling;
                float _RTiling;
                float _GTiling;
                float _BTiling;
                float _ATiling;
                float _UseALayer;
                float _NormalStrength;
                float _RampThreshold;
                float _RampSmoothness;
                float _LIGHTING;
                float _WorldUV;
                float _WorldUVScale;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                #ifdef _NORMALMAP
                    OUT.tangentWS = float4(nrm.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                    OUT.bitangentWS = nrm.bitangentWS;
                #endif
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // world UV: ฉายจากบนลงล่าง (XZ) — ไม่พึ่ง UV ของ mesh. _WorldUVScale = กี่ world unit ต่อ 1 รอบ texture (ที่ tiling 1)
                // branch จากค่า float ตรงๆ (ไม่ใช่ keyword) → ทำงานกับ material เก่าเสมอ ไม่มีกับดัก keyword ไม่ติด
                float2 worldUV = IN.positionWS.xz / max(0.001, _WorldUVScale);
                float2 uv0 = _WorldUV > 0.5 ? worldUV : IN.uv;

                float2 uvBase = uv0 * _BaseTiling;
                float2 uvR = uv0 * _RTiling;
                float2 uvG = uv0 * _GTiling;
                float2 uvB = uv0 * _BTiling;

                // --- ผสม 4 layer ตาม vertex color (ไล่: Base → R → G → B) ---
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvBase).rgb;
                albedo = lerp(albedo, SAMPLE_TEXTURE2D(_RMap, sampler_RMap, uvR).rgb, IN.color.r);
                albedo = lerp(albedo, SAMPLE_TEXTURE2D(_GMap, sampler_GMap, uvG).rgb, IN.color.g);
                albedo = lerp(albedo, SAMPLE_TEXTURE2D(_BMap, sampler_BMap, uvB).rgb, IN.color.b);
                if (_UseALayer > 0.5)   // layer 5 (alpha) — เปิดใช้ต่อ material
                    albedo = lerp(albedo, SAMPLE_TEXTURE2D(_AMap, sampler_AMap, uv0 * _ATiling).rgb, IN.color.a);
                albedo *= _BaseColor.rgb;

                #if defined(_LIGHTING_UNLIT)
                    // texture ดิบๆ ตาม tile — ไม่คิดแสง/เงา/normal เลย (เบาสุด + ใช้เช็ค texture/tiling ได้ตรงๆ)
                    half3 col = albedo;
                #else
                    // --- normal: ผสมด้วย mask ชุดเดียวกัน แล้วแปลงเข้า world space ---
                    half3 N = normalize(IN.normalWS);
                    #ifdef _NORMALMAP
                        half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BaseNormal, sampler_BaseNormal, uvBase), _NormalStrength);
                        nTS = lerp(nTS, UnpackNormalScale(SAMPLE_TEXTURE2D(_RNormal, sampler_RNormal, uvR), _NormalStrength), IN.color.r);
                        nTS = lerp(nTS, UnpackNormalScale(SAMPLE_TEXTURE2D(_GNormal, sampler_GNormal, uvG), _NormalStrength), IN.color.g);
                        nTS = lerp(nTS, UnpackNormalScale(SAMPLE_TEXTURE2D(_BNormal, sampler_BNormal, uvB), _NormalStrength), IN.color.b);
                        half3 bitangent = IN.bitangentWS * IN.tangentWS.w;
                        half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS);
                        N = normalize(mul(normalize(nTS), tbn));
                    #endif

                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                    Light mainLight = GetMainLight(shadowCoord);

                    half ndl = saturate(dot(N, mainLight.direction));
                    half atten = ndl * mainLight.shadowAttenuation;

                    #if defined(_LIGHTING_SMOOTH)
                        half ramp = atten;                                                        // ไล่นุ่ม ไม่แบ่งแถบ
                    #else
                        half ramp = smoothstep(_RampThreshold, _RampThreshold + _RampSmoothness, atten);  // toon 2 ระดับ
                    #endif

                    half3 lit = lerp(_ShadowColor.rgb, mainLight.color.rgb, ramp);
                    half3 col = albedo * lit;
                #endif

                col = MixFog(col, IN.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // เงา (ให้พื้นทอดเงาได้ถ้าพื้นมีความสูง) + depth (จำเป็นสำหรับ URP depth texture / SSAO / น้ำ)
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
