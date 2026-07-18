using System.Collections.Generic;
using Splice.Characters;
using Splice.Data;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Combat
{
    // ป้อมปืนติดป้อม — จัดการ "เล็ง + อนิเมชันยิง + ภาพกระสุน" ทับบน combat ของ TowerCharacter
    // (ดาเมจยังคิด/ลงที่ server ใน TowerCharacter เท่านั้น). วางบน NetworkObject เดียวกับ TowerCharacter.
    //   - ค่าปรับทั้งหมด (โหมด/เล็ง/ความเร็ว/ความสูง/FX) อยู่ใน TurretDefinitionSO — component นี้เหลือแค่ลากชิ้นส่วน
    //   - เล็ง: server เก็บ target id ใน NetworkVariable → ทุก instance หมุน turretPivot เข้าหาเอง (local)
    //   - ยิง: server เรียก Fire(...) → FireClientRpc → ทุก client เล่น "fire" + spawn กระสุน cosmetic / beam
    public class TurretController : NetworkBehaviour
    {
        [Header("ค่าปรับ (SO — แก้ที่เดียวมีผลทุกป้อมที่ใช้ SO นี้)")]
        [SerializeField] private TurretDefinitionSO definition;

        [Header("ชิ้นส่วนใน prefab (ลากลูกของป้อมใส่ — SO อ้างไม่ได้)")]
        [Tooltip("ชิ้นที่หมุนเล็ง (จานหมุน+ปืน). เว้น = ใช้ transform ของ object นี้")]
        [SerializeField] private Transform turretPivot;
        [Tooltip("ปลายกระบอก — จุดเกิดกระสุน + ทิศเล็ง. เว้น = ใช้ turretPivot")]
        [SerializeField] private Transform muzzle;
        [Tooltip("โหมด Direct: เส้น beam วาบ (LineRenderer 2 จุด) — เว้นได้")]
        [SerializeField] private LineRenderer beam;
        [Tooltip("Animator ของ turret — เล่นตอนยิง (auto หา child ถ้าเว้น)")]
        [SerializeField] private Animator animator;
        [Tooltip("ชื่อ state ใน Animator (กองกลาง — แก้ที่เดียว ไม่ hardcode). เว้น = ใช้ชื่อ default 'fire'")]
        [SerializeField] private TurretAnimSetSO animSet;

        // target ปัจจุบันสำหรับเล็ง — server เขียน, ทุก client อ่านไปหมุน turret เอง (0 = ไม่มีเป้า)
        private readonly NetworkVariable<ulong> aimTargetId = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Quaternion restLocalRotation;
        private float beamTimer;

        public TurretFireMode Mode => definition != null ? definition.fireMode : TurretFireMode.Projectile;

        // ชื่อ state ยิง — จากกองกลาง animSet ถ้ามี, ไม่งั้น fallback 'fire'
        private string FireClip => animSet != null && !string.IsNullOrWhiteSpace(animSet.fire) ? animSet.fire : "fire";

        private void Awake()
        {
            if (turretPivot == null) turretPivot = transform;
            if (muzzle == null) muzzle = turretPivot;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            restLocalRotation = turretPivot.localRotation;
            if (beam != null) beam.enabled = false;
        }

        // ---------- server: เล็ง + ยิง ----------

        // เรียกทุกเฟรมจาก TowerCharacter ให้ turret หันตามเป้าหลัก (null = ไม่มีเป้า)
        public void SetAimTarget(CharacterBase target)
        {
            if (!IsServer) return;
            var id = NetIdOf(target);
            if (aimTargetId.Value != id) aimTargetId.Value = id;
        }

        // พร้อมยิงเป้านี้หรือยัง — Direct ต้องเล็งตรงก่อน (ยิงตามลำกล้อง) / Projectile ยิงได้เลย (homing เข้าเป้าเอง)
        public bool ReadyToFire(CharacterBase target)
        {
            if (definition == null || definition.fireMode != TurretFireMode.Direct) return true;
            return target != null && Mathf.Abs(AngleToTarget(target.transform.position)) <= definition.aimToleranceDeg;
        }

        // เวลาเดินทางของกระสุนถึงเป้า — server ใช้ตั้งเวลาลงดาเมจ + client ใช้ให้ภาพตรงกัน (Direct = 0 ลงทันที)
        public float TravelTimeTo(Vector3 targetPos)
        {
            if (definition == null || definition.fireMode == TurretFireMode.Direct || definition.projectilePrefab == null)
                return 0f;
            var t = Vector3.Distance(muzzle.position, targetPos) / definition.AverageSpeed;
            return Mathf.Max(definition.minFlightSeconds, t);   // floor เดียวกันทั้ง server+client → ดาเมจ/ภาพยังตรงกัน
        }

        // server สั่งยิงชุด (ปกติ 1 เป้า, หลายเป้าถ้าป้อม multi-target) → กระจายภาพให้ทุก client
        public void Fire(IReadOnlyList<CharacterBase> targets)
        {
            if (!IsServer || targets == null || targets.Count == 0) return;

            var ids = new ulong[targets.Count];
            var points = new Vector3[targets.Count];
            for (var i = 0; i < targets.Count; i++)
            {
                ids[i] = NetIdOf(targets[i]);
                points[i] = targets[i] != null ? targets[i].transform.position : muzzle.position;
            }
            FireClientRpc(ids, points);
        }

        [ClientRpc]
        private void FireClientRpc(ulong[] targetIds, Vector3[] targetPoints)
        {
            AnimatorUtil.SafeCrossFade(animator, FireClip, 0.02f);

            for (var i = 0; i < targetIds.Length; i++)
            {
                var tf = ResolveTransform(targetIds[i]);
                var point = tf != null ? tf.position : (i < targetPoints.Length ? targetPoints[i] : muzzle.position);
                if (Mode == TurretFireMode.Direct) ShowBeam(point);
                else SpawnProjectile(tf, point);
            }
        }

        private void SpawnProjectile(Transform target, Vector3 fallbackPoint)
        {
            if (definition == null) return;
            var dest = target != null ? target.position : fallbackPoint;
            ProjectileVisual.Spawn(definition, muzzle.position, target, fallbackPoint, TravelTimeTo(dest));
        }

        private void ShowBeam(Vector3 point)
        {
            if (beam != null)
            {
                beam.enabled = true;
                beam.positionCount = 2;
                beam.SetPosition(0, muzzle.position);
                beam.SetPosition(1, point);
                beamTimer = definition != null ? definition.beamFlashSeconds : 0.05f;
            }
            if (definition != null && definition.directImpactEffect != null)
                Instantiate(definition.directImpactEffect, point, Quaternion.identity);
        }

        // ---------- ทุก instance: หมุน turret เข้าหาเป้า (local) ----------

        private void Update()
        {
            AimUpdate();

            if (beam != null && beam.enabled)
            {
                beamTimer -= Time.deltaTime;
                if (beamTimer <= 0f) beam.enabled = false;
            }
        }

        private void AimUpdate()
        {
            if (definition == null) return;

            var targetTf = ResolveTransform(aimTargetId.Value);
            if (targetTf == null)
            {
                if (definition.returnToRestWhenIdle)
                    turretPivot.localRotation = Quaternion.RotateTowards(
                        turretPivot.localRotation, restLocalRotation, definition.turnSpeedDegPerSec * Time.deltaTime);
                return;
            }

            // หมุน "รอบแกนที่เลือก" อย่างเดียว (แกน local ปัจจุบันของ pivot) → ท่าเดิม/มุมเอียงคงไว้ ไม่พลิก
            var angle = AngleToTarget(targetTf.position);
            var step = Mathf.Clamp(angle, -definition.turnSpeedDegPerSec * Time.deltaTime, definition.turnSpeedDegPerSec * Time.deltaTime);
            if (Mathf.Abs(step) > 0.0001f)
                turretPivot.rotation = Quaternion.AngleAxis(step, AxisWorld(definition.spinAxis)) * turretPivot.rotation;
        }

        // องศาที่ยังต้องหมุน (รอบ Spin Axis) ให้ Forward Axis (ปากกระบอก) ชี้เข้าหา targetPos.
        // ฉายทั้งทิศเล็งและทิศเป้าลงระนาบตั้งฉากกับ Spin Axis แล้วหามุมเซ็น → หมุนแกนเดียว ไม่แตะท่าอื่น
        private float AngleToTarget(Vector3 targetPos)
        {
            var axis = AxisWorld(definition.spinAxis);
            var f = Vector3.ProjectOnPlane(AxisWorld(definition.forwardAxis), axis);
            var d = Vector3.ProjectOnPlane(targetPos - turretPivot.position, axis);
            if (f.sqrMagnitude < 0.0001f || d.sqrMagnitude < 0.0001f) return 0f;  // forwardAxis ∥ spinAxis = เล็งไม่ได้
            var signed = Vector3.SignedAngle(f, d, axis);
            return definition.invertSpin ? -signed : signed;
        }

        // ทิศ world ของแกน local ที่เลือก (ตามการหมุนปัจจุบันของ pivot)
        private Vector3 AxisWorld(TurretSpinAxis axis)
        {
            var v = axis switch
            {
                TurretSpinAxis.X => turretPivot.right,
                TurretSpinAxis.Z => turretPivot.forward,
                _ => turretPivot.up
            };
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.up;
        }

        // ---------- helpers ----------

        private static ulong NetIdOf(CharacterBase c) =>
            c != null && c.TryGetComponent<NetworkObject>(out var no) ? no.NetworkObjectId : 0;

        private static Transform ResolveTransform(ulong netId)
        {
            if (netId == 0 || NetworkManager.Singleton == null) return null;
            var sm = NetworkManager.Singleton.SpawnManager;
            return sm != null && sm.SpawnedObjects.TryGetValue(netId, out var no) ? no.transform : null;
        }
    }
}
