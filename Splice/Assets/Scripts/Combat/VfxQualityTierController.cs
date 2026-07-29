using UnityEngine;

namespace Splice.Combat
{
    public enum VfxQualityTier
    {
        Low,
        Medium,
        High
    }

    // One pooled prefab can carry three visual budgets. Auto maps the active Unity quality
    // level across Low/Medium/High, while tests and accessibility settings may override it.
    public sealed class VfxQualityTierController : MonoBehaviour
    {
        [SerializeField] private GameObject low;
        [SerializeField] private GameObject medium;
        [SerializeField] private GameObject high;

        public static VfxQualityTier? OverrideTier { get; set; }

        public GameObject Low => low;
        public GameObject Medium => medium;
        public GameObject High => high;
        public VfxQualityTier ActiveTier { get; private set; }

        private void OnEnable() => Apply(ResolveTier());

        public void Configure(GameObject lowVariant, GameObject mediumVariant,
            GameObject highVariant)
        {
            low = lowVariant;
            medium = mediumVariant;
            high = highVariant;
            Apply(ResolveTier());
        }

        public void Apply(VfxQualityTier tier)
        {
            ActiveTier = tier;
            if (low != null) low.SetActive(tier == VfxQualityTier.Low);
            if (medium != null) medium.SetActive(tier == VfxQualityTier.Medium);
            if (high != null) high.SetActive(tier == VfxQualityTier.High);
        }

        public static VfxQualityTier ResolveTier()
        {
            if (OverrideTier.HasValue) return OverrideTier.Value;
            var count = Mathf.Max(1, QualitySettings.names.Length);
            if (count <= 1) return VfxQualityTier.Medium;
            var normalized = QualitySettings.GetQualityLevel() / (float)(count - 1);
            if (normalized < 0.34f) return VfxQualityTier.Low;
            return normalized < 0.67f
                ? VfxQualityTier.Medium
                : VfxQualityTier.High;
        }
    }

}
