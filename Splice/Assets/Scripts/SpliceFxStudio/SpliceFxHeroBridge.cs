using Splice.Data;
using Splice.FxStudio;
using Splice.Placement;
using UnityEngine;

namespace Splice.Combat
{
    // Integration boundary: the reusable FX Studio package remains independent from
    // gameplay, while Splice's authoritative ability execution only emits presentation
    // stages into the existing pooled VFX path.
    public static class SpliceFxHeroBridge
    {
        public static bool PlayExecutionStage(
            HeroAbilityDefinitionSO ability,
            HeroAbilityVfxStage stage,
            Transform heroRoot,
            Transform heroEffectAnchor,
            Vector3 from,
            Vector3 to,
            float lifetimeOverride)
        {
            var binding = Binding(ability, stage);
            if (binding?.exportedPrefab == null) return false;
            Resolve(binding, stage, heroRoot, heroEffectAnchor, from, to,
                out var point, out var follow, out var rotation);
            var scale = ResolveScale(
                binding, ability, heroRoot, follow != null);
            var lifetime = lifetimeOverride > 0f
                ? Mathf.Max(lifetimeOverride, binding.LifetimeSeconds)
                : binding.LifetimeSeconds;
            if (stage == HeroAbilityVfxStage.Travel &&
                follow == null && (to - from).sqrMagnitude > 0.001f)
                VfxPoolService.SpawnTravel(binding.exportedPrefab,
                    point, to + binding.localOffset, rotation, lifetime,
                    Mathf.Max(0.01f, binding.LifetimeSeconds), scale);
            else
                VfxPoolService.Spawn(binding.exportedPrefab, point, rotation,
                    lifetime, follow, binding.localOffset, scale);
            return true;
        }

        public static bool PlayScheduled(
            HeroAbilityDefinitionSO ability,
            Transform heroRoot,
            Transform heroEffectAnchor,
            Vector3 target,
            Vector3 origin)
        {
            if (ability?.fxStudioPackage == null) return false;
            var played = false;
            foreach (var binding in ability.fxStudioPackage.stages)
            {
                if (binding?.exportedPrefab == null ||
                    binding.stage == SpliceFxStage.Custom)
                    continue;
                var stage = (HeroAbilityVfxStage)(int)binding.stage;
                Resolve(binding, stage, heroRoot, heroEffectAnchor,
                    origin, target, out var point, out var follow,
                    out var rotation);
                var scale = ResolveScale(
                    binding, ability, heroRoot, follow != null);
                var delay = Mathf.Max(0f, binding.delaySeconds);
                var lifetime = binding.LifetimeSeconds;
                if (stage == HeroAbilityVfxStage.Travel &&
                    follow == null &&
                    (target - origin).sqrMagnitude > 0.001f)
                    VfxPoolService.ScheduleTravel(
                        binding.exportedPrefab, point,
                        target + binding.localOffset, rotation, delay,
                        lifetime, lifetime, scale);
                else
                    VfxPoolService.Schedule(binding.exportedPrefab,
                        point, rotation, delay, lifetime, follow,
                        binding.localOffset, scale);
                played = true;
            }
            return played;
        }

        private static SpliceFxStageBinding Binding(
            HeroAbilityDefinitionSO ability,
            HeroAbilityVfxStage stage) =>
            ability?.fxStudioPackage?.Find((SpliceFxStage)(int)stage);

        private static void Resolve(
            SpliceFxStageBinding binding,
            HeroAbilityVfxStage stage,
            Transform heroRoot,
            Transform heroEffectAnchor,
            Vector3 from,
            Vector3 to,
            out Vector3 point,
            out Transform follow,
            out Quaternion rotation)
        {
            point = stage is HeroAbilityVfxStage.Cast or
                HeroAbilityVfxStage.Launch or
                HeroAbilityVfxStage.Travel
                ? from
                : to;
            follow = null;
            switch (binding.placement)
            {
                case SpliceFxPlacement.HeroRoot:
                    follow = heroRoot;
                    point = heroRoot != null ? heroRoot.position : point;
                    break;
                case SpliceFxPlacement.HeroEffectAnchor:
                    follow = heroEffectAnchor != null
                        ? heroEffectAnchor
                        : heroRoot;
                    point = follow != null ? follow.position : point;
                    break;
                case SpliceFxPlacement.GroundSurface:
                    GroundSurfaceResolver.TrySnap(point, heroRoot,
                        out point, binding.groundOffset);
                    break;
            }

            var direction = to - from;
            direction.y = 0f;
            rotation = binding.orientToDirection &&
                       direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction.normalized)
                : heroRoot != null
                    ? heroRoot.rotation
                    : Quaternion.identity;
        }

        private static float ResolveScale(
            SpliceFxStageBinding binding,
            HeroAbilityDefinitionSO ability,
            Transform heroRoot,
            bool inheritsHeroScale)
        {
            if (binding == null) return 1f;
            return binding.scaleMode switch
            {
                SpliceFxScaleMode.HeroRelative =>
                    inheritsHeroScale
                        ? 1f
                        : GroundPlacementProfile.ResolveScaleFactor(heroRoot),
                SpliceFxScaleMode.AbilityCastRange =>
                    Mathf.Max(0.1f, ability != null
                        ? ability.castRange
                        : 1f),
                SpliceFxScaleMode.AbilityEffectRadius =>
                    Mathf.Max(0.1f, ability != null
                        ? ability.effectRadius
                        : 1f),
                _ => 1f
            };
        }
    }
}
