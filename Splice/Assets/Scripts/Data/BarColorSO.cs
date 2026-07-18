using UnityEngine;

namespace Splice.Data
{
    // สีของแท่ง (HP/โล่/มานา) ตามสัดส่วนที่เหลือ — "กองกลาง" แก้สีที่เดียว ไม่ hardcode.
    // ใช้ Unity Gradient: ใส่ color key ได้หลายจุด (0 = ว่าง/หมด, 1 = เต็ม) แล้ว evaluate ตามค่าปัจจุบัน.
    // เช่น HP: pos 1.0 = น้ำเงิน, 0.5 = เขียว, 0.25 = ส้ม, 0.1 = แดง (ไล่ gradient ให้เอง).
    [CreateAssetMenu(fileName = "BarColor", menuName = "Splice/Bar Color")]
    public class BarColorSO : ScriptableObject
    {
        [Tooltip("สีตามสัดส่วนที่เหลือ (ซ้าย 0 = ว่าง/ต่ำ, ขวา 1 = เต็ม). ดับเบิลคลิกแถบเพื่อเพิ่ม color key")]
        public Gradient fillGradient = DefaultGradient();

        public Color Evaluate(float fill01) => fillGradient.Evaluate(Mathf.Clamp01(fill01));

        // ค่าเริ่มต้น: ต่ำ=แดง → กลาง=เขียว → เต็ม=น้ำเงิน (ผู้ใช้แก้ต่อได้)
        private static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.85f, 0.15f, 0.15f), 0f),   // ต่ำ = แดง
                    new GradientColorKey(new Color(0.20f, 0.75f, 0.25f), 0.5f), // กลาง = เขียว
                    new GradientColorKey(new Color(0.20f, 0.45f, 0.95f), 1f)    // เต็ม = น้ำเงิน
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
