using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Splice.Combat
{
    public static class VfxPoolService
    {
        private const int MaxInactivePerPrefab = 8;
        private static VfxPoolHost host;

        public static int ActiveCount => host != null ? host.ActiveCount : 0;
        public static int InactiveCount => host != null ? host.InactiveCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            host = null;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
            float lifetimeSeconds, Transform follow = null, Vector3 localOffset = default,
            float worldScaleMultiplier = 1f)
        {
            if (prefab == null) return null;
            return Host.Spawn(prefab, position, rotation, lifetimeSeconds, follow, localOffset,
                false, position, 0f, worldScaleMultiplier);
        }

        public static GameObject SpawnTravel(GameObject prefab, Vector3 origin, Vector3 target,
            Quaternion rotation, float lifetimeSeconds, float travelSeconds,
            float worldScaleMultiplier = 1f)
        {
            if (prefab == null) return null;
            return Host.Spawn(prefab, origin, rotation,
                Mathf.Max(lifetimeSeconds, travelSeconds), null, Vector3.zero,
                true, target, Mathf.Max(0.01f, travelSeconds),
                worldScaleMultiplier);
        }

        public static void Schedule(GameObject prefab, Vector3 position, Quaternion rotation,
            float delaySeconds, float lifetimeSeconds, Transform follow = null,
            Vector3 localOffset = default, float worldScaleMultiplier = 1f)
        {
            if (prefab == null) return;
            Host.Schedule(new VfxSpawnRequest
            {
                prefab = prefab,
                position = position,
                rotation = rotation,
                delaySeconds = Mathf.Max(0f, delaySeconds),
                lifetimeSeconds = Mathf.Max(0.05f, lifetimeSeconds),
                follow = follow,
                localOffset = localOffset,
                worldScaleMultiplier = worldScaleMultiplier
            });
        }

        public static void ScheduleTravel(GameObject prefab, Vector3 origin, Vector3 target,
            Quaternion rotation, float delaySeconds, float lifetimeSeconds, float travelSeconds,
            float worldScaleMultiplier = 1f)
        {
            if (prefab == null) return;
            Host.Schedule(new VfxSpawnRequest
            {
                prefab = prefab,
                position = origin,
                target = target,
                rotation = rotation,
                delaySeconds = Mathf.Max(0f, delaySeconds),
                lifetimeSeconds = Mathf.Max(lifetimeSeconds, travelSeconds),
                isTravel = true,
                travelSeconds = Mathf.Max(0.01f, travelSeconds),
                worldScaleMultiplier = worldScaleMultiplier
            });
        }

        public static void ReleaseAllForTests()
        {
            if (host != null) host.ReleaseAll();
        }

        private static VfxPoolHost Host
        {
            get
            {
                if (host != null) return host;
                var existing = Object.FindFirstObjectByType<VfxPoolHost>(
                    FindObjectsInactive.Include);
                if (existing != null)
                {
                    host = existing;
                    return host;
                }
                var go = new GameObject("[Splice VFX Pool]");
                Object.DontDestroyOnLoad(go);
                host = go.AddComponent<VfxPoolHost>();
                return host;
            }
        }

        private sealed class VfxSpawnRequest
        {
            public GameObject prefab;
            public Vector3 position;
            public Vector3 target;
            public Quaternion rotation;
            public float delaySeconds;
            public float lifetimeSeconds;
            public Transform follow;
            public Vector3 localOffset;
            public bool isTravel;
            public float travelSeconds;
            public float worldScaleMultiplier = 1f;
        }

        private sealed class ActiveVfx
        {
            public EntityId key;
            public GameObject instance;
            public Transform follow;
            public Vector3 localOffset;
            public float releaseAt;
            public bool isTravel;
            public Vector3 origin;
            public Vector3 target;
            public float travelStartedAt;
            public float travelSeconds;
        }

        private sealed class ScheduledVfx
        {
            public VfxSpawnRequest request;
            public float executeAt;
        }

        private sealed class VfxPoolHost : MonoBehaviour
        {
            private readonly Dictionary<EntityId, Queue<GameObject>> inactive = new();
            private readonly List<ActiveVfx> active = new();
            private readonly List<ScheduledVfx> scheduled = new();
            public int ActiveCount => active.Count;
            public int InactiveCount
            {
                get
                {
                    var total = 0;
                    foreach (var pair in inactive) total += pair.Value.Count;
                    return total;
                }
            }

            public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
                float lifetimeSeconds, Transform follow, Vector3 localOffset, bool isTravel,
                Vector3 target, float travelSeconds, float worldScaleMultiplier)
            {
                var key = prefab.GetEntityId();
                if (!inactive.TryGetValue(key, out var queue))
                    inactive[key] = queue = new Queue<GameObject>();
                GameObject instance = null;
                while (queue.Count > 0 && instance == null) instance = queue.Dequeue();
                if (instance == null)
                {
                    instance = Object.Instantiate(prefab);
                    instance.name = prefab.name + " [Pooled]";
                }

                var entry = new ActiveVfx
                {
                    key = key,
                    instance = instance,
                    follow = follow,
                    localOffset = localOffset,
                    releaseAt = Time.time + Mathf.Max(0.05f, lifetimeSeconds),
                    isTravel = isTravel,
                    origin = position,
                    target = target,
                    travelStartedAt = Time.time,
                    travelSeconds = Mathf.Max(0.01f, travelSeconds)
                };
                Prepare(instance, prefab, position, rotation, follow, localOffset,
                    worldScaleMultiplier);
                active.Add(entry);
                return instance;
            }

            public void Schedule(VfxSpawnRequest request)
            {
                if (request.delaySeconds <= 0f)
                {
                    Execute(request);
                    return;
                }
                scheduled.Add(new ScheduledVfx
                {
                    request = request,
                    executeAt = Time.time + request.delaySeconds
                });
            }

            private void Update()
            {
                for (var i = scheduled.Count - 1; i >= 0; i--)
                {
                    if (Time.time < scheduled[i].executeAt) continue;
                    var request = scheduled[i].request;
                    scheduled.RemoveAt(i);
                    Execute(request);
                }

                for (var i = active.Count - 1; i >= 0; i--)
                {
                    var entry = active[i];
                    if (entry.instance == null)
                    {
                        active.RemoveAt(i);
                        continue;
                    }
                    if (entry.follow != null)
                    {
                        entry.instance.transform.localPosition = entry.localOffset;
                    }
                    else if (entry.isTravel)
                    {
                        var t = Mathf.Clamp01((Time.time - entry.travelStartedAt) /
                                              entry.travelSeconds);
                        entry.instance.transform.position = Vector3.Lerp(
                            entry.origin, entry.target, t);
                    }
                    if (Time.time < entry.releaseAt) continue;
                    active.RemoveAt(i);
                    Release(entry);
                }
            }

            private void Execute(VfxSpawnRequest request)
            {
                if (request == null || request.prefab == null) return;
                if (request.follow == null && request.localOffset != Vector3.zero)
                    request.position += request.localOffset;
                Spawn(request.prefab, request.position, request.rotation,
                    request.lifetimeSeconds, request.follow, request.localOffset,
                    request.isTravel, request.target, request.travelSeconds,
                    request.worldScaleMultiplier);
            }

            private void Release(ActiveVfx entry)
            {
                if (entry.instance == null) return;
                StopEffects(entry.instance);
                entry.instance.transform.SetParent(transform, false);
                entry.instance.SetActive(false);
                if (!inactive.TryGetValue(entry.key, out var queue))
                    inactive[entry.key] = queue = new Queue<GameObject>();
                if (queue.Count < MaxInactivePerPrefab) queue.Enqueue(entry.instance);
                else Object.Destroy(entry.instance);
            }

            public void ReleaseAll()
            {
                scheduled.Clear();
                for (var i = active.Count - 1; i >= 0; i--) Release(active[i]);
                active.Clear();
            }

            private static void Prepare(GameObject instance, GameObject prefab,
                Vector3 position, Quaternion rotation, Transform follow,
                Vector3 localOffset, float worldScaleMultiplier)
            {
                instance.SetActive(false);
                var tf = instance.transform;
                var baseScale = prefab != null
                    ? prefab.transform.localScale
                    : Vector3.one;
                if (follow != null)
                {
                    tf.SetParent(follow, false);
                    tf.localPosition = localOffset;
                    tf.rotation = rotation;
                    // Attached FX already inherit the Hero scale from their parent.
                    tf.localScale = baseScale;
                }
                else
                {
                    tf.SetParent(null, false);
                    tf.SetPositionAndRotation(position, rotation);
                    tf.localScale = baseScale *
                                    Mathf.Clamp(worldScaleMultiplier, 0.05f, 20f);
                }
                instance.SetActive(true);
                foreach (var trail in instance.GetComponentsInChildren<TrailRenderer>(true))
                {
                    trail.Clear();
                    trail.emitting = true;
                }
                foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Play(true);
                }
                foreach (var visual in instance.GetComponentsInChildren<VisualEffect>(true))
                {
                    visual.enabled = true;
                    visual.Reinit();
                    visual.Play();
                }
                foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true))
                {
                    audio.Stop();
                    if (audio.playOnAwake) audio.Play();
                }
            }

            private static void StopEffects(GameObject instance)
            {
                foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var trail in instance.GetComponentsInChildren<TrailRenderer>(true))
                {
                    trail.emitting = false;
                    trail.Clear();
                }
                foreach (var visual in instance.GetComponentsInChildren<VisualEffect>(true))
                    visual.Stop();
                foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true))
                    audio.Stop();
            }

            private void OnDestroy()
            {
                if (host == this) host = null;
            }
        }
    }
}
