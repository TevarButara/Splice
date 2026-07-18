using UnityEngine;

namespace Splice.Characters
{
    public static class AnimatorUtil
    {
        // CrossFade แบบปลอดภัย — ข้ามเงียบๆ ถ้า Animator Controller ยังไม่มี state ชื่อนั้น (เช็คทุก layer).
        // กัน error "Invalid Layer Index '-1'" ตอน greybox ที่ยังต่อ clip/สร้าง state ไม่ครบ → เกมยังรันได้ ไม่ spam log.
        public static void SafeCrossFade(Animator animator, string stateName, float transition)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName)) return;

            var hash = Animator.StringToHash(stateName);
            for (var layer = 0; layer < animator.layerCount; layer++)
            {
                if (animator.HasState(layer, hash))
                {
                    animator.CrossFade(hash, transition, layer);
                    return;
                }
            }
            // ไม่พบ state นี้ใน controller → ข้าม (ไม่ error)
        }
    }
}
