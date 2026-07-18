using System;
using Splice.Characters;
using Splice.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

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
        AttackerEliminated
    }

    // Raid win/lose objective (architecture 5.6). "Attacker"/"Defender" = บทบาทต่อ raid (RaidSide) ไม่ใช่ตัวตนถาวร:
    //   Attacker wins -> the Fort's core is destroyed.
    //   Defender wins -> the match timer runs out, OR the attacker is eliminated
    //                    (no miners left + gold at 0 + no units on the field — unrecoverable, since making a
    //                     miner needs gold and gold needs a miner).
    // Server-authoritative; clients read outcome/reason/time via NetworkVariables.
    public class RaidManager : NetworkBehaviour
    {
        public event Action<RaidOutcome> OnRaidEnded;

        [FormerlySerializedAs("invaderTeam")]
        [SerializeField] private RaidSide attackerSide = RaidSide.Attacker;
        [Tooltip("ความยาวแมตช์ (วินาที) — Fort ชนะถ้ารอดจนหมดเวลา. 2-4 นาทีตามดีไซน์ session")]
        [SerializeField] private float matchDurationSeconds = 180f;

        private readonly NetworkVariable<RaidOutcome> outcome = new(
            RaidOutcome.InProgress, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<RaidEndReason> endReason = new(
            RaidEndReason.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> remainingSeconds = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // One raid per match (1:1). Exposed so units (e.g. MonsterCharacter) can freeze when the match ends
        // without each holding a serialized reference. Mirrors FortCore.Instance.
        public static RaidManager Instance { get; private set; }

        public RaidOutcome Outcome => outcome.Value;
        public RaidEndReason EndReason => endReason.Value;
        public float RemainingSeconds => remainingSeconds.Value;

        // True once the raid has been decided by any path — readable on server and clients.
        public bool IsOver => outcome.Value != RaidOutcome.InProgress;

        // Becomes true once the Fort has actually spawned and is alive; lets us tell "Fort not spawned yet"
        // apart from "Fort destroyed" when polling.
        private bool fortSeen;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;
            remainingSeconds.Value = matchDurationSeconds;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsServer || outcome.Value != RaidOutcome.InProgress) return;

            // Fort destroyed = invaders win. Polled (not event-subscribed) so it's robust to scene spawn
            // order — the Fort may spawn after this manager, and on death it despawns (Instance → null).
            if (FortCore.Instance != null && !FortCore.Instance.IsDead)
            {
                fortSeen = true;
            }
            else if (fortSeen)
            {
                EndRaid(RaidOutcome.MonstersWin, RaidEndReason.FortDestroyed);
                return;
            }

            remainingSeconds.Value = Mathf.Max(0f, remainingSeconds.Value - Time.deltaTime);
            if (remainingSeconds.Value <= 0f)
            {
                EndRaid(RaidOutcome.FortDefends, RaidEndReason.TimerExpired);
                return;
            }

            if (IsAttackerEliminated())
            {
                EndRaid(RaidOutcome.FortDefends, RaidEndReason.AttackerEliminated);
            }
        }

        // Invader can no longer threaten the fort: no miners, no gold, no units on the field.
        // Guard on the gold bank existing first — before the economy spawns, "no gold" is a false
        // positive, not a real elimination.
        private bool IsAttackerEliminated()
        {
            var bank = GoldController.For(attackerSide);
            if (bank == null || bank.CurrentGold > 0) return false;

            if (CountAliveAttackerMiners() > 0) return false;
            if (CountAliveAttackerMonsters() > 0) return false;
            return true;
        }

        private int CountAliveAttackerMiners()
        {
            var count = 0;
            var miners = MinerCharacter.Active;
            for (var i = 0; i < miners.Count; i++)
            {
                if (miners[i].Side == attackerSide && !miners[i].IsDead) count++;
            }
            return count;
        }

        // นับเฉพาะมอนฝ่าย Attacker (ทัพผู้บุก) — garrison ฝ่าย Defender ไม่นับ ไม่งั้นจะกันการตกรอบผิดๆ
        private int CountAliveAttackerMonsters()
        {
            var count = 0;
            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
            {
                if (!monsters[i].IsDead && monsters[i].Side == attackerSide) count++;
            }
            return count;
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
