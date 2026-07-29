using UnityEngine;

namespace Splice.Combat
{
    public enum UltimateVfxMotionMode
    {
        Cast,
        Launch,
        Travel,
        Impact,
        End
    }

    /// <summary>
    /// Lightweight pooled presentation motion for the Rowan Ultimate variants.
    /// It animates the quality-root only, leaving gameplay scale on the prefab root.
    /// </summary>
    public sealed class UltimateVfxMotion : MonoBehaviour
    {
        [SerializeField] private UltimateVfxMotionMode mode;
        [SerializeField, Min(0.05f)] private float lifetimeSeconds = 1f;
        [SerializeField] private float rotationDegreesPerSecond = 28f;

        private Vector3 authoredScale = Vector3.one;
        private Quaternion authoredRotation = Quaternion.identity;
        private float startedAt;
        private bool captured;

        public UltimateVfxMotionMode Mode => mode;

        public void ConfigureEditor(
            UltimateVfxMotionMode valueMode,
            float valueLifetimeSeconds,
            float valueRotationDegreesPerSecond)
        {
            mode = valueMode;
            lifetimeSeconds = Mathf.Max(0.05f, valueLifetimeSeconds);
            rotationDegreesPerSecond = valueRotationDegreesPerSecond;
            CaptureAuthoredTransform();
        }

        private void Awake() => CaptureAuthoredTransform();

        private void OnEnable()
        {
            CaptureAuthoredTransform();
            startedAt = Time.time;
            Apply(0f);
        }

        private void OnDisable()
        {
            if (!captured) return;
            transform.localScale = authoredScale;
            transform.localRotation = authoredRotation;
        }

        private void Update()
        {
            var elapsed = Mathf.Max(0f, Time.time - startedAt);
            Apply(Mathf.Clamp01(elapsed / lifetimeSeconds));
        }

        private void Apply(float normalizedTime)
        {
            if (!captured) CaptureAuthoredTransform();
            var scale = 1f;
            var rotation = rotationDegreesPerSecond *
                           Mathf.Max(0f, Time.time - startedAt);
            switch (mode)
            {
                case UltimateVfxMotionMode.Cast:
                {
                    var reveal = Smooth01(normalizedTime / 0.14f);
                    var pulse = 1f + Mathf.Sin(normalizedTime * Mathf.PI * 8f) *
                        0.035f * (1f - normalizedTime);
                    scale = Mathf.Lerp(0.08f, 1f, reveal) * pulse;
                    break;
                }
                case UltimateVfxMotionMode.Launch:
                    scale = Mathf.Lerp(0.18f, 1.15f,
                        Smooth01(normalizedTime / 0.42f));
                    rotation *= 2.25f;
                    break;
                case UltimateVfxMotionMode.Travel:
                    scale = 1f + Mathf.Sin(normalizedTime * Mathf.PI * 10f) * 0.08f;
                    rotation *= 1.5f;
                    break;
                case UltimateVfxMotionMode.Impact:
                {
                    var attack = Smooth01(normalizedTime / 0.2f);
                    var release = Smooth01(
                        Mathf.Clamp01((normalizedTime - 0.2f) / 0.8f));
                    scale = Mathf.Lerp(0.12f, 1.28f, attack);
                    scale = Mathf.Lerp(scale, 0.86f, release);
                    rotation *= 1.8f;
                    break;
                }
                case UltimateVfxMotionMode.End:
                    scale = Mathf.Lerp(1.05f, 0.04f,
                        Smooth01(normalizedTime));
                    rotation *= -1.6f;
                    break;
            }

            transform.localScale = authoredScale * Mathf.Max(0.01f, scale);
            transform.localRotation = authoredRotation *
                                      Quaternion.Euler(0f, rotation, 0f);
        }

        private void CaptureAuthoredTransform()
        {
            if (captured) return;
            authoredScale = transform.localScale;
            authoredRotation = transform.localRotation;
            captured = true;
        }

        private static float Smooth01(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
