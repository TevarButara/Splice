using System;
using System.Collections;
using System.Collections.Generic;
using Splice.Characters;
using Splice.Data;
using UnityEngine;

namespace Splice.Combat
{
    [CreateAssetMenu(
        fileName = "NewMultiDashExecution",
        menuName = "Splice/Hero Abilities/Execution/Multi Dash")]
    public sealed class MultiDashHeroAbilityExecutionSO : HeroAbilityExecutionSO
    {
        [Header("Strike Sequence")]
        [Min(1)] public int strikeCount = 7;
        [Min(1f)] public float dashSpeed = 28f;
        [Min(0f)] public float targetOvershootDistance = 1.6f;
        [Min(0f)] public float impactHoldSeconds = 0.08f;
        [Min(0f)] public float impactVfxLifetimeSeconds = 0.5f;
        [Min(1f)] public float returnSpeed = 32f;

        [Header("Target Rules")]
        [Tooltip("Targets are selected by the authoritative server from enemies inside the ability cast range. One target receives every strike; multiple targets are selected randomly per strike.")]
        public bool randomizeMultipleTargets = true;

        private const float DestinationTolerance = 0.04f;

        public override bool TryStart(HeroAbilityExecutionContext context)
        {
            if (context?.CoroutineHost == null ||
                context.HeroTransform == null ||
                context.Ability == null ||
                context.ResolveTargets == null ||
                context.IsValidTarget == null ||
                context.CanContinue == null)
                return false;

            var initialTargets = context.ResolveTargets();
            if (initialTargets == null || initialTargets.Count == 0) return false;
            context.StartCoroutine(Run(context));
            return true;
        }

        public override void Validate(HeroAbilityDefinitionSO ability,
            Action<string, string> reportError)
        {
            if (strikeCount < 1)
                reportError?.Invoke("ABILITY_MULTI_DASH_STRIKES_INVALID",
                    "Multi-dash strike count must be at least one.");
            if (dashSpeed <= 0f || returnSpeed <= 0f)
                reportError?.Invoke("ABILITY_MULTI_DASH_SPEED_INVALID",
                    "Multi-dash and return speeds must be positive.");
            if (ability != null && ability.damage < strikeCount)
                reportError?.Invoke("ABILITY_MULTI_DASH_DAMAGE_TOO_LOW",
                    "Total ability damage must be at least the strike count so every strike can deal damage.");
        }

        public int DamageAtStrike(int totalDamage, int zeroBasedStrike)
        {
            return SplitDamage(totalDamage, Mathf.Max(1, strikeCount), zeroBasedStrike);
        }

        public static int SplitDamage(int totalDamage, int count, int zeroBasedIndex)
        {
            count = Mathf.Max(1, count);
            if (zeroBasedIndex < 0 || zeroBasedIndex >= count) return 0;
            totalDamage = Mathf.Max(0, totalDamage);
            var baseDamage = totalDamage / count;
            var remainder = totalDamage % count;
            return baseDamage + (zeroBasedIndex < remainder ? 1 : 0);
        }

        public float EstimatedDuration(float castRange) =>
            EstimatedDuration(castRange, 1f);

        public float EstimatedDuration(float authoredCastRange, float worldScaleFactor)
        {
            var scale = SanitizeScale(worldScaleFactor);
            var castRange = Mathf.Max(0.1f, authoredCastRange) * scale;
            var overshoot = targetOvershootDistance * scale;
            var worstDashDistance = Mathf.Max(0.1f, castRange * 2f + overshoot * 2f);
            var dashSeconds = worstDashDistance /
                              Mathf.Max(1f, dashSpeed * scale);
            var returnSeconds = castRange /
                                Mathf.Max(1f, returnSpeed * scale);
            return strikeCount * (dashSeconds + Mathf.Max(0f, impactHoldSeconds)) +
                   returnSeconds + 0.35f;
        }

        public float ScaledOvershootDistance(float worldScaleFactor) =>
            Mathf.Max(0f, targetOvershootDistance) *
            SanitizeScale(worldScaleFactor);

        public float ScaledDashSpeed(float worldScaleFactor) =>
            Mathf.Max(1f, dashSpeed) * SanitizeScale(worldScaleFactor);

        private IEnumerator Run(HeroAbilityExecutionContext context)
        {
            var origin = context.CastOrigin;
            var hits = 0;
            var worldScaleFactor = SanitizeScale(context.WorldScaleFactor);
            var overshootDistance = ScaledOvershootDistance(worldScaleFactor);
            var worldDashSpeed = ScaledDashSpeed(worldScaleFactor);
            var worldReturnSpeed =
                Mathf.Max(1f, returnSpeed) * worldScaleFactor;
            var seed = unchecked(
                (context.Ability.abilityId?.GetHashCode() ?? 0) ^
                Mathf.RoundToInt(Time.time * 1000f) ^
                Mathf.RoundToInt(origin.sqrMagnitude * 997f));
            var random = new System.Random(seed);
            var presentationLifetime = EstimatedDuration(
                context.Ability.castRange, worldScaleFactor);
            context.Present?.Invoke(
                HeroAbilityVfxStage.Cast, origin, origin, presentationLifetime);
            context.Present?.Invoke(
                HeroAbilityVfxStage.Launch, origin, origin, presentationLifetime);
            context.Present?.Invoke(
                HeroAbilityVfxStage.Travel, origin, origin, presentationLifetime);

            try
            {
                for (var strike = 0; strike < Mathf.Max(1, strikeCount); strike++)
                {
                    if (!context.CanContinue()) break;
                    var targets = RemoveInvalidTargets(
                        context.ResolveTargets(), context.IsValidTarget);
                    if (targets.Count == 0) break;

                    var target = SelectTarget(
                        targets, context.PreferredTarget, strike, random);
                    if (target == null) break;

                    var impactPoint = target.transform.position;
                    var dashDirection = impactPoint - origin;
                    dashDirection.y = 0f;
                    if (dashDirection.sqrMagnitude < 0.001f)
                        dashDirection = Vector3.forward;
                    dashDirection.Normalize();
                    dashDirection = Quaternion.AngleAxis(
                        strike * 137.50776f, Vector3.up) * dashDirection;

                    var destination = impactPoint +
                                      dashDirection * overshootDistance;
                    if (context.ResolveGroundedDestination != null)
                        destination = context.ResolveGroundedDestination(destination);
                    context.Face?.Invoke(dashDirection);

                    yield return MoveTo(
                        context, destination, worldDashSpeed);
                    if (!context.CanContinue()) break;
                    if (!context.IsValidTarget(target)) continue;

                    impactPoint = target.transform.position;
                    var damage = DamageAtStrike(context.Ability.damage, strike);
                    if (damage > 0)
                    {
                        context.ApplyDamage?.Invoke(target, damage);
                        hits++;
                    }
                    context.Present?.Invoke(
                        HeroAbilityVfxStage.Impact,
                        context.HeroTransform.position,
                        impactPoint,
                        Mathf.Max(0.05f, impactVfxLifetimeSeconds));

                    if (impactHoldSeconds > 0f)
                        yield return new WaitForSeconds(impactHoldSeconds);
                }

                if (context.CanContinue())
                {
                    var groundedOrigin = context.ResolveGroundedDestination != null
                        ? context.ResolveGroundedDestination(origin)
                        : origin;
                    context.Face?.Invoke(groundedOrigin - context.HeroTransform.position);
                    yield return MoveTo(
                        context, groundedOrigin, worldReturnSpeed);
                    context.HeroTransform.position = groundedOrigin;
                    context.Present?.Invoke(
                        HeroAbilityVfxStage.End,
                        context.HeroTransform.position,
                        groundedOrigin,
                        0.8f);
                }
            }
            finally
            {
                context.Completed?.Invoke(hits);
            }
        }

        private static float SanitizeScale(float value) =>
            Mathf.Clamp(value > 0f ? value : 1f, 0.05f, 20f);

        private CharacterBase SelectTarget(
            IReadOnlyList<CharacterBase> targets,
            CharacterBase preferred,
            int strike,
            System.Random random)
        {
            if (targets == null || targets.Count == 0) return null;
            if (targets.Count == 1) return targets[0];
            if (strike == 0 && preferred != null)
            {
                for (var i = 0; i < targets.Count; i++)
                    if (targets[i] == preferred)
                        return preferred;
            }
            if (!randomizeMultipleTargets)
                return targets[strike % targets.Count];
            return targets[random.Next(0, targets.Count)];
        }

        private static List<CharacterBase> RemoveInvalidTargets(
            List<CharacterBase> targets,
            Func<CharacterBase, bool> isValid)
        {
            targets ??= new List<CharacterBase>();
            for (var i = targets.Count - 1; i >= 0; i--)
                if (!isValid(targets[i]))
                    targets.RemoveAt(i);
            return targets;
        }

        private static IEnumerator MoveTo(
            HeroAbilityExecutionContext context,
            Vector3 destination,
            float speed)
        {
            while (context.CanContinue())
            {
                var current = context.HeroTransform.position;
                var delta = destination - current;
                if (delta.sqrMagnitude <= DestinationTolerance * DestinationTolerance)
                    break;
                context.Face?.Invoke(delta);
                context.HeroTransform.position = Vector3.MoveTowards(
                    current, destination, speed * Time.deltaTime);
                yield return null;
            }
            if (context.CanContinue())
                context.HeroTransform.position = destination;
        }
    }
}
