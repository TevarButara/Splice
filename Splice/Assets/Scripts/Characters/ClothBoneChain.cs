using System.Collections.Generic;
using UnityEngine;

namespace Splice.Characters
{
    // Runtime helper for lightweight cloth-like bone strips.
    // Keep SpringBoneChain for tails/hair. This component targets cloth_ bones
    // generated as multiple vertical strips: tabards, skirt panels, coat tails.
    [DisallowMultipleComponent]
    public sealed class ClothBoneChain : MonoBehaviour
    {
        [System.Serializable]
        public struct ClothSphere
        {
            public Transform center;
            public float radius;
        }

        [System.Serializable]
        public struct ClothCapsule
        {
            public Transform start;
            public Transform end;
            public float radius;
        }

        [Header("Roots")]
        [Tooltip("Root bones for cloth strips. Leave empty to auto-find bones by prefix.")]
        [SerializeField] private Transform[] roots;

        [Header("Auto Find")]
        [SerializeField] private bool autoFindByName = true;
        [SerializeField] private string namePrefix = "cloth_";

        [Header("Cloth Motion")]
        [Range(0.05f, 2f)]
        [Tooltip("Overall simulation speed. Lower values make cloth motion slower and easier to read.")]
        [SerializeField] private float simulationSpeed = 0.65f;

        [Range(0f, 1f)]
        [Tooltip("Pull back to animated rest pose. Cloth usually wants more stiffness than tails.")]
        [SerializeField] private float stiffness = 0.16f;

        [Range(0f, 1f)]
        [Tooltip("Higher values settle faster and reduce long swinging.")]
        [SerializeField] private float damping = 0.55f;

        [Tooltip("Small world-space gravity/wind force applied per frame.")]
        [SerializeField] private Vector3 gravity = new(0f, -0.0015f, 0f);

        [Tooltip("Movement reference such as Hips or Character Root. Used for readable cloth lag when the character moves.")]
        [SerializeField] private Transform center;
        [Range(0f, 1f)]
        [Tooltip("How much cloth receives swing/lag from Center movement. 0 follows immediately, 1 lags strongly.")]
        [SerializeField] private float motionInfluence = 0.45f;

        [Header("Scale & Wind")]
        [Tooltip("Scale gravity/wind/noise by each bone length. Enable for large FBX imports so tiny force values still show motion.")]
        [SerializeField] private bool scaleForcesByBoneLength;
        [Min(0f)]
        [Tooltip("Extra multiplier for gravity/wind/noise after optional bone-length scaling.")]
        [SerializeField] private float forceMultiplier = 1f;
        [Tooltip("Constant world-space wind. Use small X/Z values for panel sway.")]
        [SerializeField] private Vector3 wind = Vector3.zero;
        [Min(0f)]
        [Tooltip("Procedural side sway strength. Keeps cloth from looking frozen on idle characters.")]
        [SerializeField] private float windNoiseStrength;
        [Min(0f)]
        [SerializeField] private float windNoiseSpeed = 1.2f;

        [Range(5f, 180f)]
        [Tooltip("Clamp bending away from the animated rest direction. Lower values keep cloth panels readable.")]
        [SerializeField] private float maxBendAngle = 55f;

        [Header("Collision")]
        [SerializeField] private float boneRadius = 0.025f;
        [SerializeField] private ClothSphere[] spheres;
        [SerializeField] private ClothCapsule[] capsules;

        [Header("Debug")]
        [SerializeField] private bool logDebugInfo;
        [SerializeField] private int debugRootCount;
        [SerializeField] private int debugNodeCount;
        [SerializeField] private float debugAverageBoneLength;

        private sealed class Node
        {
            public Transform Transform;
            public Quaternion InitLocalRotation;
            public Vector3 BoneAxisLocal;
            public float Length;
            public float WindSeed;
            public Vector3 CurrentTip;
            public Vector3 PreviousTip;
            public Vector3 CurrentDirection;
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
            nodes.Clear();
            builtRootCount = 0;

            if (roots != null)
            {
                foreach (var root in roots)
                {
                    if (root != null)
                    {
                        builtRootCount++;
                        Build(root);
                    }
                }
            }

            if (nodes.Count == 0 && autoFindByName && !string.IsNullOrEmpty(namePrefix))
            {
                AutoCollectRoots();
            }

            previousCenterPosition = CenterPosition();
            RefreshDebugInfo();
            if (logDebugInfo)
            {
                Debug.Log(
                    $"ClothBoneChain '{name}' roots={debugRootCount}, nodes={debugNodeCount}, avgLength={debugAverageBoneLength:F3}, scaleForces={scaleForcesByBoneLength}",
                    this);
            }
        }

        private void AutoCollectRoots()
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

                builtRootCount++;
                Build(t);
            }
        }

        private void Build(Transform bone)
        {
            if (bone.childCount == 0)
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

            var axisDirection = axis.normalized;
            nodes.Add(new Node
            {
                Transform = bone,
                InitLocalRotation = bone.localRotation,
                BoneAxisLocal = axisDirection,
                Length = length,
                WindSeed = (nodes.Count + 1) * 23.719f + transform.position.sqrMagnitude,
                CurrentTip = child.position,
                PreviousTip = child.position,
                CurrentDirection = SafeDirection(child.position - bone.position, bone.TransformDirection(axisDirection))
            });

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
                var carry = centerDelta * (1f - motionInfluence);
                for (var i = 0; i < nodes.Count; i++)
                {
                    nodes[i].CurrentTip += carry;
                    nodes[i].PreviousTip += carry;
                }
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i].Transform.localRotation = nodes[i].InitLocalRotation;
            }

            var maxRadians = maxBendAngle * Mathf.Deg2Rad;
            var simulatedDeltaTime = Time.deltaTime * Mathf.Max(0.01f, simulationSpeed);
            var frameScale = Mathf.Clamp(simulatedDeltaTime * 60f, 0.05f, 2f);
            var inertiaKeep = Mathf.Pow(1f - Mathf.Clamp01(damping), frameScale);
            var springStep = Mathf.Clamp01(stiffness * simulatedDeltaTime * 8f);
            var directionStep = Mathf.Clamp01(simulatedDeltaTime * 12f);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var bone = node.Transform;
                var basePosition = bone.position;
                var restTip = bone.TransformPoint(node.BoneAxisLocal * node.Length);

                var next = node.CurrentTip
                           + (node.CurrentTip - node.PreviousTip) * inertiaKeep
                           + (restTip - node.CurrentTip) * springStep
                           + ScaledExternalForce(node) * frameScale;

                next = ConstrainLength(basePosition, next, node.Length);

                var restOffset = restTip - basePosition;
                var nextOffset = next - basePosition;
                var restDirection = restOffset.sqrMagnitude > 1e-8f ? restOffset.normalized : bone.TransformDirection(node.BoneAxisLocal);
                var nextDirection = nextOffset.sqrMagnitude > 1e-8f ? nextOffset.normalized : restDirection;
                var clampedDirection = Vector3.RotateTowards(restDirection, nextDirection, maxRadians, 0f);
                next = basePosition + clampedDirection * node.Length;

                next = ResolveCollisions(basePosition, next, node.Length);

                var targetDirection = SafeDirection(next - basePosition, restDirection);
                var smoothedDirection = SafeDirection(
                    Vector3.Slerp(node.CurrentDirection, targetDirection, directionStep),
                    restDirection);
                next = basePosition + smoothedDirection * node.Length;

                node.PreviousTip = node.CurrentTip;
                node.CurrentTip = next;
                node.CurrentDirection = smoothedDirection;

                var aimWorld = bone.TransformDirection(node.BoneAxisLocal);
                if (aimWorld.sqrMagnitude > 1e-8f)
                {
                    bone.rotation = Quaternion.FromToRotation(aimWorld, smoothedDirection) * bone.rotation;
                }
            }
        }

        private Vector3 ScaledExternalForce(Node node)
        {
            var force = gravity + wind + WindNoise(node);
            var scale = Mathf.Max(0f, forceMultiplier);
            if (scaleForcesByBoneLength)
            {
                scale *= Mathf.Max(0.0001f, node.Length);
            }

            return force * scale;
        }

        private Vector3 WindNoise(Node node)
        {
            if (windNoiseStrength <= 0f || windNoiseSpeed <= 0f)
            {
                return Vector3.zero;
            }

            var t = Time.time * windNoiseSpeed;
            var side = transform.right * ((Mathf.PerlinNoise(node.WindSeed, t) - 0.5f) * 2f);
            var forward = transform.forward * ((Mathf.PerlinNoise(node.WindSeed + 41.37f, t) - 0.5f) * 2f);
            return (side + forward) * windNoiseStrength;
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
                total += nodes[i].Length;
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

        private Vector3 ResolveCollisions(Vector3 basePosition, Vector3 tip, float length)
        {
            if (spheres != null)
            {
                for (var i = 0; i < spheres.Length; i++)
                {
                    var sphere = spheres[i];
                    if (sphere.center == null)
                    {
                        continue;
                    }

                    tip = PushOutOfSphere(basePosition, tip, sphere.center.position, sphere.radius + boneRadius, length);
                }
            }

            if (capsules != null)
            {
                for (var i = 0; i < capsules.Length; i++)
                {
                    var capsule = capsules[i];
                    if (capsule.start == null || capsule.end == null)
                    {
                        continue;
                    }

                    var closest = ClosestPointOnSegment(tip, capsule.start.position, capsule.end.position);
                    tip = PushOutOfSphere(basePosition, tip, closest, capsule.radius + boneRadius, length);
                }
            }

            return tip;
        }

        private static Vector3 PushOutOfSphere(Vector3 basePosition, Vector3 tip, Vector3 center, float radius, float length)
        {
            var delta = tip - center;
            if (delta.sqrMagnitude >= radius * radius || delta.sqrMagnitude <= 1e-8f)
            {
                return tip;
            }

            var pushed = center + delta.normalized * radius;
            return ConstrainLength(basePosition, pushed, length);
        }

        private static Vector3 ConstrainLength(Vector3 basePosition, Vector3 tip, float length)
        {
            var direction = tip - basePosition;
            if (direction.sqrMagnitude <= 1e-8f)
            {
                return basePosition;
            }

            return basePosition + direction.normalized * length;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            var segment = end - start;
            var lengthSq = segment.sqrMagnitude;
            if (lengthSq <= 1e-8f)
            {
                return start;
            }

            var t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSq);
            return start + segment * t;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.9f, 0.45f, 0.35f);
            if (spheres != null)
            {
                foreach (var sphere in spheres)
                {
                    if (sphere.center != null)
                    {
                        Gizmos.DrawWireSphere(sphere.center.position, sphere.radius);
                    }
                }
            }

            if (capsules == null)
            {
                return;
            }

            foreach (var capsule in capsules)
            {
                if (capsule.start == null || capsule.end == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(capsule.start.position, capsule.radius);
                Gizmos.DrawWireSphere(capsule.end.position, capsule.radius);
                Gizmos.DrawLine(capsule.start.position, capsule.end.position);
            }
        }
    }
}
