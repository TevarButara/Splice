using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Splice.Characters
{
    // Simple VRM/Unity-chan style spring bone สำหรับหาง/ผม/หนวด/ผ้าชิ้นแคบ
    // หลักการ: Verlet inertia -> ดึงกลับท่า rest -> gravity direction -> constrain ความยาว -> หมุนกระดูก
    [DisallowMultipleComponent]
    public class SpringBoneChain : MonoBehaviour
    {
        [System.Serializable]
        public struct SpringSphere
        {
            public Transform center;
            public float radius;
        }

        [Tooltip("root ของแต่ละสายที่จะไหว เช่น spring_tail_1 / spring_hair_1")]
        [SerializeField] private Transform[] roots;

        [Header("Auto Find")]
        [Tooltip("ถ้า Roots ว่าง จะค้นหา root กระดูกที่ชื่อขึ้นต้น Name Prefix ให้อัตโนมัติ")]
        [SerializeField] private bool autoFindByName = true;
        [SerializeField] private string namePrefix = "spring_";

        [Header("Simple Spring")]
        [Range(0.05f, 2f)]
        [Tooltip("ความเร็วรวมของ simulation. ต่ำ = ขยับช้า/นุ่มขึ้น, สูง = ตอบสนองไวขึ้น")]
        [SerializeField] private float simulationSpeed = 0.65f;
        [Range(0f, 1f)]
        [Tooltip("แรงดึงกลับท่าเดิม มาก = แข็ง/กลับไว, น้อย = นุ่ม/ตามหลัง")]
        [SerializeField] private float stiffness = 0.25f;
        [FormerlySerializedAs("damping")]
        [Range(0f, 1f)]
        [Tooltip("แรงหน่วง มาก = หยุดไว, น้อย = แกว่ง/พริ้วนาน")]
        [SerializeField] private float drag = 0.35f;
        [FormerlySerializedAs("gravity")]
        [Tooltip("ทิศที่อยากให้ chain โน้มไป เช่น (0,-1,0)=ลงพื้น, (0,1,0)=ลอยขึ้น")]
        [SerializeField] private Vector3 gravityDirection = Vector3.down;
        [Min(0f)]
        [Tooltip("แรงโน้มตาม Gravity Direction มาก = ตก/ชี้ตามทิศชัดขึ้น")]
        [SerializeField] private float gravityPower = 0.25f;
        [Tooltip("จุดอ้างอิงการเคลื่อนของตัวละคร เช่น Character Root หรือ Hips. ช่วยให้การวิ่ง/เลื่อนตำแหน่งไม่ทำให้หางกระชากเกิน")]
        [SerializeField] private Transform center;
        [Range(0f, 1f)]
        [Tooltip("ให้หางรับแรงเหวี่ยงจากการเคลื่อนของ Center. 0=ตามตัวละครทันที, 1=lag/เหวี่ยงตาม movement ชัด")]
        [SerializeField] private float motionInfluence = 0.75f;

        [Header("Collision")]
        [FormerlySerializedAs("boneRadius")]
        [Tooltip("รัศมีปลายกระดูกตอนชน collider")]
        [SerializeField] private float hitRadius = 0.04f;
        [SerializeField] private SpringSphere[] colliders;

        [Header("Debug")]
        [SerializeField] private bool logDebugInfo;
        [SerializeField] private int debugRootCount;
        [SerializeField] private int debugNodeCount;
        [SerializeField] private float debugAverageBoneLength;

        private class Node
        {
            public Transform bone;
            public Quaternion initialLocalRotation;
            public Vector3 localAxis;
            public float length;
            public Vector3 currentTail;
            public Vector3 previousTail;
            public Vector3 currentDirection;
        }

        private readonly List<Node> nodes = new();
        private int builtRootCount;
        private Vector3 previousCenterPosition;

        private void OnValidate()
        {
            RefreshDebugPreview();
        }

        private void Start()
        {
            Rebuild();
            if (logDebugInfo)
            {
                Debug.Log(
                    $"SpringBoneChain '{name}' roots={debugRootCount}, nodes={debugNodeCount}, avgLength={debugAverageBoneLength:F3}",
                    this);
            }
        }

        public void Rebuild()
        {
            nodes.Clear();
            builtRootCount = 0;

            if (roots != null)
            {
                foreach (var root in roots)
                {
                    AddChain(root);
                }
            }

            // ไม่ได้ลาก root มาเอง → หาจากชื่อ (ตามที่ AutoRig ตั้ง prefix ให้)
            if (nodes.Count == 0 && autoFindByName && !string.IsNullOrEmpty(namePrefix))
            {
                AutoCollectRoots();
            }

            previousCenterPosition = CenterPosition();
            RefreshDebugInfo();
        }

        private void AddChain(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var before = nodes.Count;
            Build(root);
            if (nodes.Count > before)
            {
                builtRootCount++;
            }
        }

        // เก็บ "root ของแต่ละสาย" = กระดูกชื่อขึ้นต้น prefix ที่พ่อ 'ไม่' ได้ขึ้นต้น prefix (= จุดเริ่มสาย)
        private void AutoCollectRoots()
        {
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == transform || !t.name.StartsWith(namePrefix)) continue;
                if (t.parent != null && t.parent.name.StartsWith(namePrefix)) continue;  // ไม่ใช่ root
                AddChain(t);
            }
        }

        // ไล่ลงสาย: สร้าง node เฉพาะกระดูกที่ 'มีลูก' (= มีท่อนให้สปริง) — leaf ที่ไม่มีลูกจะเกาะ parent ไปเอง
        private void Build(Transform bone)
        {
            if (bone == null || bone.childCount == 0)
            {
                return;
            }

            var child = bone.GetChild(0);
            var axis = bone.InverseTransformPoint(child.position);
            var length = axis.magnitude;
            if (length <= 0.0001f)
            {
                return;
            }

            nodes.Add(new Node
            {
                bone = bone,
                initialLocalRotation = bone.localRotation,
                localAxis = axis.normalized,
                length = length,
                currentTail = child.position,
                previousTail = child.position,
                currentDirection = SafeDirection(child.position - bone.position, bone.TransformDirection(axis.normalized))
            });

            // parent มาก่อนลูกในลิสต์ → LateUpdate ประมวลตามลำดับได้เลย
            Build(child);
        }

        private void LateUpdate()
        {
            if (nodes.Count == 0 || Time.deltaTime <= 0f)
            {
                return;
            }

            var centerDelta = CenterPosition() - previousCenterPosition;
            previousCenterPosition += centerDelta;
            if (centerDelta.sqrMagnitude > 0f)
            {
                // ขยับ tail ตาม center แค่บางส่วน เพื่อให้ส่วนที่เหลือกลายเป็นแรงเหวี่ยง/lag จาก movement ของตัวละคร
                var carry = centerDelta * (1f - motionInfluence);
                for (var i = 0; i < nodes.Count; i++)
                {
                    nodes[i].currentTail += carry;
                    nodes[i].previousTail += carry;
                }
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i].bone.localRotation = nodes[i].initialLocalRotation;
            }

            var simulatedDeltaTime = Time.deltaTime * Mathf.Max(0.01f, simulationSpeed);
            var frameScale = Mathf.Clamp(simulatedDeltaTime * 60f, 0.05f, 2f);
            var inertiaKeep = Mathf.Pow(1f - Mathf.Clamp01(drag), frameScale);
            var springStep = Mathf.Clamp01(stiffness * simulatedDeltaTime * 8f);
            var gravityStep = simulatedDeltaTime * 8f;
            var directionStep = Mathf.Clamp01(simulatedDeltaTime * 12f);
            var gravity = GravityDirectionWorld();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var basePosition = node.bone.position;
                var restTail = node.bone.TransformPoint(node.localAxis * node.length);
                var restDirection = SafeDirection(restTail - basePosition, node.bone.TransformDirection(node.localAxis));

                var inertia = (node.currentTail - node.previousTail) * inertiaKeep;
                var spring = (restTail - node.currentTail) * springStep;
                var gravityOffset = gravity * gravityPower * node.length * gravityStep;
                var nextTail = node.currentTail + inertia + spring + gravityOffset;

                nextTail = KeepLength(basePosition, nextTail, restDirection, node.length);
                nextTail = ResolveCollisions(basePosition, nextTail, restDirection, node.length);

                // หมุนกระดูกให้ "แกน rest" ชี้ไปที่ปลายใหม่
                var targetDirection = SafeDirection(nextTail - basePosition, restDirection);
                var smoothedDirection = SafeDirection(
                    Vector3.Slerp(node.currentDirection, targetDirection, directionStep),
                    restDirection);

                nextTail = basePosition + smoothedDirection * node.length;
                node.previousTail = node.currentTail;
                node.currentTail = nextTail;
                node.currentDirection = smoothedDirection;

                var aimWorld = node.bone.TransformDirection(node.localAxis);
                node.bone.rotation = Quaternion.FromToRotation(aimWorld, smoothedDirection) * node.bone.rotation;
            }
        }

        private Vector3 ResolveCollisions(Vector3 basePosition, Vector3 tailPosition, Vector3 fallbackDirection, float length)
        {
            if (colliders == null)
            {
                return tailPosition;
            }

            for (var i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col.center == null)
                {
                    continue;
                }

                var radius = Mathf.Max(0f, col.radius) + Mathf.Max(0f, hitRadius);
                var delta = tailPosition - col.center.position;
                if (delta.sqrMagnitude >= radius * radius || delta.sqrMagnitude <= 1e-8f)
                {
                    continue;
                }

                tailPosition = col.center.position + delta.normalized * radius;
                tailPosition = KeepLength(basePosition, tailPosition, fallbackDirection, length);
            }

            return tailPosition;
        }

        private static Vector3 KeepLength(Vector3 basePosition, Vector3 tailPosition, Vector3 fallbackDirection, float length)
        {
            var direction = SafeDirection(tailPosition - basePosition, fallbackDirection);
            return basePosition + direction * length;
        }

        private Vector3 GravityDirectionWorld()
        {
            return gravityDirection.sqrMagnitude > 1e-8f ? gravityDirection.normalized : Vector3.zero;
        }

        private Vector3 CenterPosition()
        {
            return center != null ? center.position : Vector3.zero;
        }

        private static Vector3 SafeDirection(Vector3 value, Vector3 fallback)
        {
            if (value.sqrMagnitude > 1e-8f)
            {
                return value.normalized;
            }

            return fallback.sqrMagnitude > 1e-8f ? fallback.normalized : Vector3.down;
        }

        private void RefreshDebugInfo()
        {
            debugRootCount = builtRootCount;
            debugNodeCount = nodes.Count;
            if (nodes.Count == 0)
            {
                debugAverageBoneLength = 0f;
                return;
            }

            var total = 0f;
            for (var i = 0; i < nodes.Count; i++)
            {
                total += nodes[i].length;
            }

            debugAverageBoneLength = total / nodes.Count;
        }

        private void RefreshDebugPreview()
        {
            if (Application.isPlaying && nodes.Count > 0)
            {
                return;
            }

            var rootCount = 0;
            var nodeCount = 0;
            var totalLength = 0f;
            var hasManualRoots = roots != null && roots.Length > 0;

            if (hasManualRoots)
            {
                for (var i = 0; i < roots.Length; i++)
                {
                    if (roots[i] == null)
                    {
                        continue;
                    }

                    rootCount++;
                    CountPreviewChain(roots[i], ref nodeCount, ref totalLength);
                }
            }
            else if (autoFindByName && !string.IsNullOrEmpty(namePrefix))
            {
                var all = GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (t == transform || !t.name.StartsWith(namePrefix))
                    {
                        continue;
                    }

                    if (t.parent != null && t.parent.name.StartsWith(namePrefix))
                    {
                        continue;
                    }

                    rootCount++;
                    CountPreviewChain(t, ref nodeCount, ref totalLength);
                }
            }

            debugRootCount = rootCount;
            debugNodeCount = nodeCount;
            debugAverageBoneLength = nodeCount > 0 ? totalLength / nodeCount : 0f;
        }

        private static void CountPreviewChain(Transform bone, ref int nodeCount, ref float totalLength)
        {
            if (bone == null || bone.childCount == 0)
            {
                return;
            }

            var child = bone.GetChild(0);
            nodeCount++;
            totalLength += Vector3.Distance(bone.position, child.position);
            CountPreviewChain(child, ref nodeCount, ref totalLength);
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
