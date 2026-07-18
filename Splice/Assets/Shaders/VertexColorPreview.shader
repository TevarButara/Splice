// โชว์ vertex color ล้วนๆ บนผิว mesh (ไม่มีแสง ไม่มี texture) — ใช้โดย Vertex Painter tool เท่านั้น
// วาดทับ mesh จริงในหน้า Scene ผ่าน Graphics.DrawMeshNow (ไม่แตะ material ของ object → ไม่ทำให้ scene dirty)
Shader "Hidden/Splice/Vertex Color Preview"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZTest LEqual
            ZWrite On
            Cull Back
            Offset -1, -1   // ดันเข้าหากล้องนิดหน่อย กัน z-fight กับ mesh จริงที่อยู่ตำแหน่งเดียวกัน

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // alpha = mask ของ layer 5 (A) — โชว์เป็นสีขาว จะได้เห็นตอนทา (ดำ=Base แดง=R เขียว=G น้ำเงิน=B ขาว=A)
                half3 rgb = lerp(IN.color.rgb, half3(1, 1, 1), IN.color.a);
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
