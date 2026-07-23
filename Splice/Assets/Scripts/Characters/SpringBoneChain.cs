using System.Collections.Generic;
using UnityEngine;

namespace Splice.Characters
{
    // สปริงกระดูกน้ำหนักเบา (ผม / cape / หาง ของ HERO) — เขียนเอง ไม่ต้องซื้อ asset, ไม่ต้องแยก mesh.
    // ใส่ component นี้บน hero แล้วลาก "root ของแต่ละสายกระดูก" (ผม/ชาย cape) เข้า Roots — ระบบไล่ลูกลงไปทั้งสายเอง.
    //
    // อัลกอริทึม (สไตล์ Unity-chan SpringBone): ทุกเฟรม reset กลับท่า rest (ให้ตามการขยับของ parent ที่ animator ขับ)
    // → คำนวณตำแหน่งปลายใหม่ = inertia (ตามหลัง = พริ้ว) + ดึงกลับ rest (stiffness) + gravity/ลม → หมุนกระดูกให้เล็งปลายนั้น.
    //
    // ⚠️ ใส่เฉพาะ HERO (ไม่กี่ตัว) — อย่าใส่ monster ที่มีเต็มจอ. คุมจำนวนกระดูก ผม+cape รวม ~8-16 ท่อน/hero.
    // ⚠️ กระดูก chain เหล่านี้ต้อง 'ไม่' ถูก keyframe ใน Animator (ปล่อยให้ spring ขับล้วน) — root ของ chain ผูกกับ Head/Chest ได้
    [DisallowMultipleComponent]
    public class SpringBoneChain : MonoBehaviour
    {
        [System.Serializable]
        public struct SpringSphere   // จุดกันสายทะลุตัว (หัว/ลำตัว) — ลาก transform + ตั้งรัศมี
        {
            public Transform center;
            public float radius;
        }

        [Tooltip("root ของแต่ละสายที่จะไหว (ผม/ชาย cape/หาง) — ระบบไล่ลูกตัวแรกลงไปทั้งสายให้เอง. " +
                 "เว้นว่างได้ถ้าเปิด Auto Find (จะหาให้เองจากชื่อ)")]
        [SerializeField] private Transform[] roots;

        [Header("Auto Find (เชื่อมกับ AutoRig — กระดูกชื่อขึ้นต้น spring_)")]
        [Tooltip("ถ้า Roots ว่าง จะค้นหา 'root ของแต่ละสาย' อัตโนมัติจากกระดูกชื่อขึ้นต้น Name Prefix " +
                 "(root = ตัวที่พ่อไม่ได้ขึ้นต้น prefix). ตรงกับชื่อที่ AutoRig ตั้งให้ (spring_hair_/spring_cape_/spring_tail_)")]
        [SerializeField] private bool autoFindByName = true;
        [Tooltip("prefix ชื่อกระดูกสาย — ต้องตรงกับฝั่ง Blender AutoRig (ค่าเริ่มต้น spring_)")]
        [SerializeField] private string namePrefix = "spring_";

        [Header("Spring")]
        [Range(0f, 1f)]
        [Tooltip("แรงดึงกลับท่าเดิมต่อเฟรม (มาก = แข็ง ไหวน้อย / น้อย = พริ้วนุ่ม)")]
        [SerializeField] private float stiffness = 0.08f;
        [Range(0f, 1f)]
        [Tooltip("หน่วง (มาก = หยุดแกว่งไว ไม่ค้าง / น้อย = แกว่งนาน)")]
        [SerializeField] private float damping = 0.4f;
        [Tooltip("แรงโน้มถ่วง/ลม (world) ดึงปลายสายต่อเฟรม — ค่าน้อยๆ เช่น (0,-0.002,0). +X/+Z = ลมพัด")]
        [SerializeField] private Vector3 gravity = new Vector3(0f, -0.002f, 0f);

        [Header("Collision (กันทะลุตัว)")]
        [Tooltip("ความหนากระดูก (รัศมี) ตอนชน collider")]
        [SerializeField] private float boneRadius = 0.04f;
        [SerializeField] private SpringSphere[] colliders;

        private class Node
        {
            public Transform t;
            public Quaternion initLocalRot;
            public Vector3 boneAxisLocal;   // ทิศไปหา child (local)
            public float length;
            public Vector3 currTip, prevTip; // world
        }

        private readonly List<Node> nodes = new();

        private void Start()
        {
            if (roots != null)
                foreach (var root in roots)
                    if (root != null) Build(root);

            // ไม่ได้ลาก root มาเอง → หาจากชื่อ (ตามที่ AutoRig ตั้ง prefix ให้)
            if (nodes.Count == 0 && autoFindByName && !string.IsNullOrEmpty(namePrefix))
                AutoCollectRoots();
        }

        // เก็บ "root ของแต่ละสาย" = กระดูกชื่อขึ้นต้น prefix ที่พ่อ 'ไม่' ได้ขึ้นต้น prefix (= จุดเริ่มสาย)
        private void AutoCollectRoots()
        {
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == transform || !t.name.StartsWith(namePrefix)) continue;
                if (t.parent != null && t.parent.name.StartsWith(namePrefix)) continue;  // ไม่ใช่ root
                Build(t);
            }
        }

        // ไล่ลงสาย: สร้าง node เฉพาะกระดูกที่ 'มีลูก' (= มีท่อนให้สปริง) — leaf ที่ไม่มีลูกจะเกาะ parent ไปเอง
        private void Build(Transform bone)
        {
            if (bone.childCount == 0) return;
            var child = bone.GetChild(0);

            nodes.Add(new Node
            {
                t = bone,
                initLocalRot = bone.localRotation,
                boneAxisLocal = bone.InverseTransformPoint(child.position).normalized,
                length = Vector3.Distance(bone.position, child.position),
                currTip = child.position,
                prevTip = child.position
            });

            Build(child);   // parent มาก่อนลูกในลิสต์ → LateUpdate ประมวลตามลำดับได้เลย
        }

        private void LateUpdate()
        {
            if (nodes.Count == 0 || Time.deltaTime <= 0f) return;

            // reset ท่า rest ก่อน — ให้ transform สะท้อน "การขยับของ parent (animator ขับ) + ท่าเดิม"
            for (var i = 0; i < nodes.Count; i++) nodes[i].t.localRotation = nodes[i].initLocalRot;

            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                var basePos = n.t.position;

                // ปลายที่ "ควรอยู่" ตามท่า rest ปัจจุบัน (หลัง parent ขยับ)
                var restTip = n.t.TransformPoint(n.boneAxisLocal * n.length);

                // integrate: inertia (ตามหลัง=พริ้ว) + สปริงดึงกลับ rest + gravity/ลม
                var next = n.currTip
                           + (n.currTip - n.prevTip) * (1f - damping)   // ความเฉื่อย + หน่วง
                           + (restTip - n.currTip) * stiffness           // ดึงกลับ rest
                           + gravity;                                    // แรงโน้ม/ลม

                // คงความยาวท่อน
                next = basePos + (next - basePos).normalized * n.length;

                // ชน collider → ดันออก แล้วคงความยาวอีกที
                if (colliders != null)
                {
                    for (var c = 0; c < colliders.Length; c++)
                    {
                        var col = colliders[c];
                        if (col.center == null) continue;
                        var r = col.radius + boneRadius;
                        var d = next - col.center.position;
                        if (d.sqrMagnitude < r * r && d.sqrMagnitude > 1e-8f)
                        {
                            next = col.center.position + d.normalized * r;
                            next = basePos + (next - basePos).normalized * n.length;
                        }
                    }
                }

                n.prevTip = n.currTip;
                n.currTip = next;

                // หมุนกระดูกให้ "แกน rest" ชี้ไปที่ปลายใหม่
                var aimWorld = n.t.TransformDirection(n.boneAxisLocal);
                n.t.rotation = Quaternion.FromToRotation(aimWorld, next - basePos) * n.t.rotation;
            }
        }

        // ช่วยจัด collider ในซีน — เห็นทรงกันทะลุ
        private void OnDrawGizmosSelected()
        {
            if (colliders == null) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            foreach (var c in colliders)
                if (c.center != null) Gizmos.DrawWireSphere(c.center.position, c.radius);
        }
    }
}
