using UnityEngine;

namespace Splice.Data
{
    [CreateAssetMenu(fileName = "NewMiner", menuName = "Splice/Miner Definition")]
    public class MinerDefinitionSO : ScriptableObject
    {
        public string minerId;
        public string displayName;
        public int maxHealth = 50;
        public float moveSpeed = 3f;

        [Tooltip("ทองที่แบกได้ต่อเที่ยว — ขุดจนเต็มแล้ววิ่งกลับฐาน")]
        public int carryCapacity = 20;

        [Tooltip("เวลาขุด (วินาที) กว่าจะเต็ม carryCapacity")]
        public float mineDurationSeconds = 2f;

        [Tooltip("เวลา 'สร้าง' (วินาที) หลังกดซื้อการ์ดจนโผล่ที่ spawn point — คิวทีละตัวเหมือนมอน")]
        public float buildTimeSeconds = 3f;

        [Tooltip("ระยะเผื่อ 'ถึง' — วัดจากผิว collider ของบ่อ (ไม่ใช่จุดกลาง) จึงไม่ต้องโตตามขนาดบ่อ. " +
                 "ตั้งเผื่อรัศมี agent เล็กน้อยพอ เช่น 0.5. สำหรับฐาน (ไม่มี collider) วัดจากจุดกลางฐาน")]
        public float arrivalRadius = 0.5f;

        public GameObject prefab;
    }
}
