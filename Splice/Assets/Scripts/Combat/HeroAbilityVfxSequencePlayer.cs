using Splice.Data;
using UnityEngine;

namespace Splice.Combat
{
    public static class HeroAbilityVfxSequencePlayer
    {
        public static void PlayExecutionStage(
            HeroAbilityDefinitionSO ability,
            HeroAbilityVfxStage stage,
            Transform heroRoot,
            Transform heroEffectAnchor,
            Vector3 from,
            Vector3 to,
            float lifetimeOverride)
        {
            if (ability == null) return;
            var cue = CueForStage(ability, stage);
            if (cue?.IsConfigured != true) return;

            var direction = to - from;
            direction.y = 0f;
            var rotation = cue.orientToCastDirection &&
                           direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction.normalized)
                : heroRoot != null ? heroRoot.rotation : Quaternion.identity;
            var lifetime = lifetimeOverride > 0f
                ? lifetimeOverride
                : Lifetime(cue, ability);
            var point = stage is HeroAbilityVfxStage.Cast or
                HeroAbilityVfxStage.Launch or
                HeroAbilityVfxStage.Travel
                ? from
                : to;
            Transform follow = null;

            if (stage == HeroAbilityVfxStage.Travel)
            {
                follow = heroEffectAnchor != null ? heroEffectAnchor : heroRoot;
                point = follow != null ? follow.position : from;
            }
            else
            {
                switch (cue.placement)
                {
                    case HeroAbilityEffectPlacement.HeroRoot:
                        follow = heroRoot;
                        point = heroRoot != null ? heroRoot.position : point;
                        break;
                    case HeroAbilityEffectPlacement.HeroEffectAnchor:
                        follow = heroEffectAnchor != null ? heroEffectAnchor : heroRoot;
                        point = follow != null ? follow.position : point;
                        break;
                    case HeroAbilityEffectPlacement.GroundSurface:
                        GroundSurfaceResolver.TrySnap(
                            point, heroRoot, out point, cue.groundOffset);
                        break;
                }
            }

            var instance = VfxPoolService.Spawn(
                cue.prefab,
                point,
                rotation,
                Mathf.Max(0.05f, lifetime),
                follow,
                cue.localOffset);
            if (instance == null) return;

            var scale = stage switch
            {
                HeroAbilityVfxStage.Cast => Mathf.Max(0.1f, ability.castRange),
                HeroAbilityVfxStage.End => Mathf.Max(0.1f, ability.castRange),
                HeroAbilityVfxStage.Impact => Mathf.Max(1f, ability.effectRadius * 0.55f),
                _ => 1f
            };
            var runtimeScale = instance.GetComponent<VfxRuntimeScale>();
            if (runtimeScale != null)
                runtimeScale.Configure(
                    scale,
                    lifetime,
                    stage == HeroAbilityVfxStage.End);
        }

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

        private static HeroAbilityVfxCue CueForStage(
            HeroAbilityDefinitionSO ability,
            HeroAbilityVfxStage stage)
        {
            return stage switch
            {
                HeroAbilityVfxStage.Cast => ability.castVfx,
                HeroAbilityVfxStage.Launch => ability.launchVfx,
                HeroAbilityVfxStage.Travel => ability.travelVfx,
                HeroAbilityVfxStage.Impact => ability.impactVfx,
                HeroAbilityVfxStage.Persistent => ability.persistentVfx,
                HeroAbilityVfxStage.End => ability.endVfx,
                _ => null
            };
        }
    }
}
