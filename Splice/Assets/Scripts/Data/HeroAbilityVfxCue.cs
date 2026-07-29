using System;
using UnityEngine;

namespace Splice.Data
{
    public enum HeroAbilityVfxStage
    {
        Cast,
        Launch,
        Travel,
        Impact,
        Persistent,
        End
    }

    [Serializable]
    public sealed class HeroAbilityVfxCue
    {
        public bool enabled = true;
        public GameObject prefab;
        [Min(0f)] public float delaySeconds;
        [Min(0f)] public float lifetimeSeconds = 1f;
        public HeroAbilityEffectPlacement placement = HeroAbilityEffectPlacement.WorldPoint;
        public Vector3 localOffset;
        [Min(0f)] public float groundOffset = 0.05f;
        public bool orientToCastDirection = true;
        [Min(0f)] public float travelDurationSeconds = 0.25f;

        public bool IsConfigured => enabled && prefab != null;
    }
}
