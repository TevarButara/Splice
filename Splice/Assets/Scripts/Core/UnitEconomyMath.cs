using UnityEngine;

namespace Splice.Core
{
    // Pure economy calculations shared by server authority, UI previews and regression tests.
    // Keeping rounding here prevents the UI from advertising a different price than the server charges.
    public static class UnitEconomyMath
    {
        public static int RepairCost(int buildCost, int currentHealth, int maxHealth, float costFactor)
        {
            if (buildCost <= 0 || maxHealth <= 0 || currentHealth >= maxHealth) return 0;
            var missingFraction = Mathf.Clamp01((maxHealth - Mathf.Max(0, currentHealth)) / (float)maxHealth);
            return Mathf.Max(1, Mathf.CeilToInt(buildCost * missingFraction * Mathf.Max(0f, costFactor)));
        }

        public static int SellRefund(int buildCost, float refundFactor)
        {
            if (buildCost <= 0 || refundFactor <= 0f) return 0;
            return Mathf.Max(0, Mathf.FloorToInt(buildCost * Mathf.Clamp01(refundFactor)));
        }

        public static int RepairAmountAtStep(int missingHealth, int stepIndex, int stepCount)
        {
            if (missingHealth <= 0 || stepCount <= 0 || stepIndex <= 0) return 0;
            var current = Mathf.RoundToInt(missingHealth * Mathf.Clamp01(stepIndex / (float)stepCount));
            var previous = Mathf.RoundToInt(missingHealth * Mathf.Clamp01((stepIndex - 1) / (float)stepCount));
            return Mathf.Max(0, current - previous);
        }
    }

    // Until the backend session exposes a verified player↔RaidSide claim, management mutations are
    // deliberately host-only. Failing closed prevents a remote attacker from selling/upgrading enemy units.
    public static class UnitManagementAuthority
    {
        public static bool IsAuthorized(ulong senderClientId, ulong serverClientId) =>
            senderClientId == serverClientId;
    }
}
