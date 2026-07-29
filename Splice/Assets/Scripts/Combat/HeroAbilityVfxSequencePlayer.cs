using Splice.Data;
using UnityEngine;

namespace Splice.Combat
{
    public static class HeroAbilityVfxSequencePlayer
    {
        public static void Play(HeroAbilityDefinitionSO ability, Transform heroRoot,
            Transform heroEffectAnchor, Vector3 targetPoint, Vector3 originPoint)
        {
            if (ability == null || !ability.HasStagedVfx) return;
            var direction = targetPoint - originPoint;
            direction.y = 0f;
            var rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction.normalized)
                : heroRoot != null ? heroRoot.rotation : Quaternion.identity;

            var travelDelay = CueDelay(ability.travelVfx, 0f);
            var travelDuration = ability.travelVfx != null
                ? Mathf.Max(0.01f, ability.travelVfx.travelDurationSeconds)
                : 0f;
            Schedule(ability.castVfx, HeroAbilityVfxStage.Cast, ability,
                heroRoot, heroEffectAnchor, originPoint, targetPoint, rotation, 0f);
            Schedule(ability.launchVfx, HeroAbilityVfxStage.Launch, ability,
                heroRoot, heroEffectAnchor, originPoint, targetPoint, rotation, 0f);
            Schedule(ability.travelVfx, HeroAbilityVfxStage.Travel, ability,
                heroRoot, heroEffectAnchor, originPoint, targetPoint, rotation, 0f);
            Schedule(ability.impactVfx, HeroAbilityVfxStage.Impact, ability,
                heroRoot, heroEffectAnchor, originPoint, targetPoint, rotation,
                travelDelay + travelDuration);
            var persistentDelay = Mathf.Max(travelDelay + travelDuration,
                CueDelay(ability.impactVfx, 0f));
            Schedule(ability.persistentVfx, HeroAbilityVfxStage.Persistent, ability,
                heroRoot, heroEffectAnchor, originPoint, targetPoint, rotation,
                persistentDelay);
            var persistentLifetime = Lifetime(ability.persistentVfx, ability);
            Schedule(ability.endVfx, HeroAbilityVfxStage.End, ability,
                heroRoot, heroEffectAnchor, originPoint, targetPoint, rotation,
                persistentDelay + persistentLifetime);
        }

        private static void Schedule(HeroAbilityVfxCue cue, HeroAbilityVfxStage stage,
            HeroAbilityDefinitionSO ability, Transform heroRoot, Transform heroEffectAnchor,
            Vector3 origin, Vector3 target, Quaternion castRotation, float automaticDelay)
        {
            if (cue == null || !cue.IsConfigured) return;
            var delay = cue.delaySeconds > 0f ? cue.delaySeconds : automaticDelay;
            var lifetime = Lifetime(cue, ability);
            var rotation = cue.orientToCastDirection ? castRotation : Quaternion.identity;
            if (stage == HeroAbilityVfxStage.Travel)
            {
                VfxPoolService.ScheduleTravel(cue.prefab, origin + cue.localOffset,
                    target + cue.localOffset, rotation, delay, lifetime,
                    cue.travelDurationSeconds);
                return;
            }

            var point = stage is HeroAbilityVfxStage.Cast or HeroAbilityVfxStage.Launch
                ? origin : target;
            Transform follow = null;
            switch (cue.placement)
            {
                case HeroAbilityEffectPlacement.HeroRoot:
                    follow = heroRoot;
                    point = heroRoot != null ? heroRoot.position : origin;
                    break;
                case HeroAbilityEffectPlacement.HeroEffectAnchor:
                    follow = heroEffectAnchor != null ? heroEffectAnchor : heroRoot;
                    point = follow != null ? follow.position : origin;
                    break;
                case HeroAbilityEffectPlacement.GroundSurface:
                    GroundSurfaceResolver.TrySnap(point, heroRoot, out point, cue.groundOffset);
                    break;
            }
            VfxPoolService.Schedule(cue.prefab, point, rotation, delay, lifetime,
                follow, cue.localOffset);
        }

        private static float CueDelay(HeroAbilityVfxCue cue, float fallback) =>
            cue != null && cue.delaySeconds > 0f ? cue.delaySeconds : fallback;

        private static float Lifetime(HeroAbilityVfxCue cue,
            HeroAbilityDefinitionSO ability)
        {
            if (cue == null) return 0f;
            if (cue.lifetimeSeconds > 0f) return cue.lifetimeSeconds;
            return ability != null &&
                   ability.damageMode == HeroAbilityDamageMode.DamageOverTime
                ? Mathf.Max(0.05f, ability.damageDurationSeconds)
                : 1f;
        }
    }
}
