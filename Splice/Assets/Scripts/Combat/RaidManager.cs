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
        FullVictory,
        Extracted,
        Defeat
    }

    // Why the raid ended — kept separate from the high-level outcome so result/reward systems can
    // distinguish a full breach, a safe extraction, and each failure path.
    public enum RaidEndReason
    {
        None,
        CoreDestroyed,
        ExtractionCompleted,
        TimerExpired,
        AttackerEliminated
    }

    // Raid objective contract. "Attacker"/"Defender" = บทบาทต่อ raid (RaidSide) ไม่ใช่ตัวตนถาวร:
    //   Full victory -> the Fort's core is destroyed.
    //   Extracted -> a server-validated extraction checkpoint completes (ขั้น 2 wires the real point).
    //   Defeat -> the match timer runs out, OR the attacker is eliminated
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
            outcome.OnValueChanged += HandleOutcomeChanged;
            if (!IsServer) return;
            remainingSeconds.Value = matchDurationSeconds;
        }

        public override void OnNetworkDespawn()
        {
            outcome.OnValueChanged -= HandleOutcomeChanged;
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
                EndRaid(RaidOutcome.FullVictory, RaidEndReason.CoreDestroyed);
                return;
            }

            remainingSeconds.Value = Mathf.Max(0f, remainingSeconds.Value - Time.deltaTime);
            if (remainingSeconds.Value <= 0f)
            {
                EndRaid(RaidOutcome.Defeat, RaidEndReason.TimerExpired);
                return;
            }

            if (IsAttackerEliminated())
            {
                EndRaid(RaidOutcome.Defeat, RaidEndReason.AttackerEliminated);
            }
        }

        // Server-only entry point for the future ExtractionPoint. Keeping the decision in RaidManager
        // prevents a client or presentation component from declaring its own result. Returns false when
        // called on a client or after another end condition has already settled the raid.
        public bool TryCompleteExtraction()
        {
            if (!IsServer || IsOver) return false;
            EndRaid(RaidOutcome.Extracted, RaidEndReason.ExtractionCompleted);
            return true;
        }

        // Step-1 Editor smoke test until ExtractionPoint exists in step 2.
        [ContextMenu("Debug/Complete Extraction")]
        private void DebugCompleteExtraction()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Raid] Extraction debug command works only in Play Mode.", this);
                return;
            }

            if (!TryCompleteExtraction())
                Debug.LogWarning("[Raid] Extraction rejected: run this on the active server before the raid ends.", this);
        }

        // Invader can no longer threaten the fort: no miners, no gold, no units, and no viable Hero.
        // Guard on the gold bank existing first — before the economy spawns, "no gold" is a false
        // positive, not a real elimination.
        private bool IsAttackerEliminated()
        {
            var bank = GoldController.For(attackerSide);
            if (bank == null || bank.CurrentGold > 0) return false;

            if (CountAliveAttackerMiners() > 0) return false;
            if (CountAliveAttackerMonsters() > 0) return false;
            if (HasViableAttackerHero()) return false;
            return true;
        }

        // A living Hero can still breach the base alone. A Downed Hero also keeps the raid alive during
        // its revive window; once Defeated, only the remaining army/economy can prevent elimination.
        private bool HasViableAttackerHero()
        {
            var hero = RaidHeroCharacter.Instance;
            return hero != null && hero.Side == attackerSide && hero.LifeState != HeroLifeState.Defeated;
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
        }

        // NetworkVariable change notifications run on the host and remote clients. Using this single path
        // keeps result UI/presentation events to exactly once per RaidManager instance on every peer.
        private void HandleOutcomeChanged(RaidOutcome previous, RaidOutcome current)
        {
            if (previous == current || current == RaidOutcome.InProgress) return;
            OnRaidEnded?.Invoke(current);
        }
    }
}
