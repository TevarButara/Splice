using UnityEngine;

namespace Splice.Combat
{
    public sealed class VfxRuntimeScale : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool shrinkOverLifetime;
        private Vector3 configuredScale = Vector3.one;
        private float lifetime = 1f;
        private float startedAt;

        private void OnEnable()
        {
            startedAt = Time.time;
            ApplyScale();
        }

        public void Configure(float uniformScale, float lifetimeSeconds,
            bool shouldShrink = false)
        {
            configuredScale = Vector3.one * Mathf.Max(0.01f, uniformScale);
            lifetime = Mathf.Max(0.05f, lifetimeSeconds);
            shrinkOverLifetime = shouldShrink;
            startedAt = Time.time;
            ApplyScale();
        }

        private void Update()
        {
            if (!shrinkOverLifetime) return;
            var t = Mathf.Clamp01((Time.time - startedAt) / lifetime);
            var eased = 1f - t * t;
            Target.localScale = configuredScale * eased;
        }

        private void ApplyScale() => Target.localScale = configuredScale;

        private Transform Target => visualRoot != null ? visualRoot : transform;
    }
}
