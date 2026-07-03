using System;
using Splice.Characters;
using Splice.Core;
using Unity.Netcode;
using UnityEngine;

namespace Splice.Combat
{
    public enum RaidOutcome
    {
        InProgress,
        MonstersWin,
        FortDefends
    }

    // Why the raid ended — lets the result UI say *how* the Fort won, not just that it did.
    public enum RaidEndReason
    {
        None,
        FortDestroyed,
        TimerExpired,
        InvaderEliminated
    }

    // 1:1 win/lose objective (architecture 5.6):
    //   Invader wins  -> the Fort's core is destroyed.
    //   Fort wins     -> the match timer runs out, OR the invader is eliminated
    //                    (no miners left + gold at 0 + no units on the field — a state the invader
    //                     can never recover from, since making a miner needs gold and gold needs a miner).
    // Server-authoritative; clients read outcome/reason/time via NetworkVariables.
    public class RaidManager : NetworkBehaviour
    {
        public event Action<RaidOutcome> OnRaidEnded;

        [SerializeField] private Team invaderTeam = Team.Invaders;
        [Tooltip("ความยาวแมตช์ (วินาที) — Fort ชนะถ้ารอดจนหมดเวลา. 2-4 นาทีตามดีไซน์ session")]
        [SerializeField] private float matchDurationSeconds = 180f;

        private readonly NetworkVariable<RaidOutcome> outcome = new(
            RaidOutcome.InProgress, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<RaidEndReason> endReason = new(
            RaidEndReason.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> remainingSeconds = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public RaidOutcome Outcome => outcome.Value;
        public RaidEndReason EndReason => endReason.Value;
        public float RemainingSeconds => remainingSeconds.Value;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            remainingSeconds.Value = matchDurationSeconds;
            if (FortCore.Instance != null) FortCore.Instance.OnDeath += HandleFortDestroyed;
        }

        private void Update()
        {
            if (!IsServer || outcome.Value != RaidOutcome.InProgress) return;

            remainingSeconds.Value = Mathf.Max(0f, remainingSeconds.Value - Time.deltaTime);
            if (remainingSeconds.Value <= 0f)
            {
                EndRaid(RaidOutcome.FortDefends, RaidEndReason.TimerExpired);
                return;
            }

            if (IsInvaderEliminated())
            {
                EndRaid(RaidOutcome.FortDefends, RaidEndReason.InvaderEliminated);
            }
        }

        // Invader can no longer threaten the fort: no miners, no gold, no units on the field.
        // Guard on the gold bank existing first — before the economy spawns, "no gold" is a false
        // positive, not a real elimination.
        private bool IsInvaderEliminated()
        {
            var bank = GoldController.For(invaderTeam);
            if (bank == null || bank.CurrentGold > 0) return false;

            if (CountAliveInvaderMiners() > 0) return false;
            if (CountAliveMonsters() > 0) return false;
            return true;
        }

        private int CountAliveInvaderMiners()
        {
            var count = 0;
            var miners = MinerCharacter.Active;
            for (var i = 0; i < miners.Count; i++)
            {
                if (miners[i].Team == invaderTeam && !miners[i].IsDead) count++;
            }
            return count;
        }

        private int CountAliveMonsters()
        {
            var count = 0;
            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
            {
                if (!monsters[i].IsDead) count++;
            }
            return count;
        }

        private void HandleFortDestroyed(CharacterBase _)
        {
            EndRaid(RaidOutcome.MonstersWin, RaidEndReason.FortDestroyed);
        }

        private void EndRaid(RaidOutcome result, RaidEndReason reason)
        {
            if (outcome.Value != RaidOutcome.InProgress) return;
            endReason.Value = reason;
            outcome.Value = result;
            OnRaidEnded?.Invoke(result);
        }
    }
}
