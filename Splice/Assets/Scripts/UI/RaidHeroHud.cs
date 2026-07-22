using Splice.Characters;
using Splice.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // Owner-facing presentation for the server-authoritative Raid Hero. All values come from replicated
    // state; this component only renders them and forwards button intent to the Hero RPC entry points.
    public class RaidHeroHud : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private RaidHeroCharacter hero;
        [Tooltip("ลูกของ object นี้ที่เปิดเมื่อพบ owned Hero — เว้นว่างได้")]
        [SerializeField] private GameObject contentRoot;

        [Header("Vitals")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider shieldSlider;
        [SerializeField] private TMP_Text heroNameText;
        [SerializeField] private TMP_Text healthText;

        [Header("State")]
        [SerializeField] private TMP_Text modeText;
        [Tooltip("ข้อความบนปุ่มสลับโหมด — แสดงโหมดที่จะเปลี่ยนไป")]
        [SerializeField] private TMP_Text modeButtonText;
        [SerializeField] private TMP_Text lifeStateText;
        [SerializeField] private GameObject downedPanel;

        [Header("Breach Rings")]
        [Tooltip("แสดงชั้นฐาน, defense progress และ breach loot — เว้นว่างได้")]
        [SerializeField] private TMP_Text breachRingText;

        [Header("Squad Order")]
        [Tooltip("แสดงจำนวนยูนิตที่รับคำสั่งและเวลาคงเหลือ — เว้นว่างได้")]
        [SerializeField] private TMP_Text squadOrderText;

        [Header("Tactical Ability")]
        [SerializeField] private GameObject abilityRoot;
        [SerializeField] private Button abilityButton;
        [SerializeField] private TMP_Text abilityNameText;
        [SerializeField] private TMP_Text abilityCooldownText;

        [Header("Interaction Prompt")]
        [SerializeField] private GameObject interactPromptRoot;
        [SerializeField] private TMP_Text interactPromptText;
        [Min(0.03f)] [SerializeField] private float proximityRefreshSeconds = 0.1f;

        [Header("Feedback Toast")]
        [SerializeField] private GameObject feedbackRoot;
        [SerializeField] private TMP_Text feedbackText;
        [Min(0.1f)] [SerializeField] private float feedbackDurationSeconds = 2.25f;

        private RaidHeroCharacter boundHero;
        private HeroInteractionKind nearbyKind;
        private float nextProximityRefresh;
        private float feedbackHideAt;

        private void OnEnable()
        {
            Bind(hero != null ? hero : RaidHeroCharacter.Instance);
        }

        private void OnDisable()
        {
            Bind(null);
        }

        private void Update()
        {
            var current = hero != null ? hero : RaidHeroCharacter.Instance;
            if (current != boundHero) Bind(current);

            var ready = boundHero != null && boundHero.IsSpawned && boundHero.IsOwner;
            // contentRoot must be a child; disabling the GameObject that owns this script would prevent
            // the HUD from detecting a Hero that spawns later.
            if (contentRoot != null && contentRoot != gameObject && contentRoot.activeSelf != ready)
                contentRoot.SetActive(ready);
            if (!ready)
            {
                if (squadOrderText != null) squadOrderText.gameObject.SetActive(false);
                if (breachRingText != null) breachRingText.gameObject.SetActive(false);
                return;
            }

            RefreshVitals();
            RefreshState();
            RefreshBreachRing();
            RefreshAbility();
            RefreshSquadOrder();

            if (Time.unscaledTime >= nextProximityRefresh)
            {
                nearbyKind = boundHero.GetNearbyInteractionKind();
                nextProximityRefresh = Time.unscaledTime + proximityRefreshSeconds;
            }
            RefreshPrompt();

            if (feedbackHideAt > 0f && Time.unscaledTime >= feedbackHideAt)
            {
                if (feedbackRoot != null) feedbackRoot.SetActive(false);
                if (feedbackText != null) feedbackText.text = string.Empty;
                feedbackHideAt = 0f;
            }
        }

        private void Bind(RaidHeroCharacter next)
        {
            if (boundHero == next) return;
            if (boundHero != null) boundHero.FeedbackReceived -= HandleFeedback;

            boundHero = next;
            hero = next;
            nearbyKind = HeroInteractionKind.None;
            nextProximityRefresh = 0f;

            if (boundHero != null) boundHero.FeedbackReceived += HandleFeedback;
        }

        private void RefreshVitals()
        {
            var maxHealth = Mathf.Max(1, boundHero.MaxHealth);
            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = maxHealth;
                healthSlider.value = boundHero.CurrentHealth;
            }

            if (shieldSlider != null)
            {
                shieldSlider.minValue = 0f;
                shieldSlider.maxValue = maxHealth;
                shieldSlider.value = Mathf.Clamp(boundHero.Shield, 0, maxHealth);
                shieldSlider.gameObject.SetActive(boundHero.Shield > 0);
            }

            if (heroNameText != null)
                heroNameText.text = boundHero.Definition != null && !string.IsNullOrWhiteSpace(boundHero.Definition.displayName)
                    ? boundHero.Definition.displayName
                    : "HERO";

            if (healthText != null)
                healthText.text = boundHero.Shield > 0
                    ? $"HP {boundHero.CurrentHealth}/{boundHero.MaxHealth}  SHIELD {boundHero.Shield}"
                    : $"HP {boundHero.CurrentHealth}/{boundHero.MaxHealth}";
        }

        private void RefreshState()
        {
            if (modeText != null)
                modeText.text = boundHero.ControlMode == HeroControlMode.Auto ? "AUTO" : "MANUAL";

            if (modeButtonText != null)
                modeButtonText.text = boundHero.ControlMode == HeroControlMode.Auto ? "MANUAL" : "AUTO";

            var isDowned = boundHero.LifeState == HeroLifeState.Downed;
            if (downedPanel != null && downedPanel.activeSelf != isDowned) downedPanel.SetActive(isDowned);

            if (lifeStateText == null) return;
            lifeStateText.text = boundHero.LifeState switch
            {
                HeroLifeState.Active => $"ACTIVE  •  REVIVE ×{boundHero.RevivesRemaining}",
                HeroLifeState.Downed =>
                    $"DOWNED {Mathf.CeilToInt(boundHero.DownedRemaining)}s  •  REVIVE ×{boundHero.RevivesRemaining}",
                HeroLifeState.Defeated => "DEFEATED",
                _ => string.Empty
            };
        }

        private void RefreshPrompt()
        {
            var show = nearbyKind != HeroInteractionKind.None && boundHero.CanAct;
            if (interactPromptRoot != null && interactPromptRoot.activeSelf != show)
                interactPromptRoot.SetActive(show);
            if (!show || interactPromptText == null) return;

            interactPromptText.text = nearbyKind switch
            {
                HeroInteractionKind.Loot => "[E] COLLECT LOOT",
                HeroInteractionKind.Extraction => "[E] EXTRACT",
                _ => string.Empty
            };
        }

        private void RefreshAbility()
        {
            var ability = boundHero.TacticalAbility;
            var hasAbility = ability != null;
            if (abilityRoot != null && abilityRoot != gameObject && abilityRoot.activeSelf != hasAbility)
                abilityRoot.SetActive(hasAbility);
            if (!hasAbility) return;

            if (abilityNameText != null) abilityNameText.text = ability.displayName;
            if (abilityCooldownText != null)
                abilityCooldownText.text = boundHero.TacticalAbilityCooldownRemaining > 0f
                    ? $"{Mathf.CeilToInt(boundHero.TacticalAbilityCooldownRemaining)}s"
                    : "READY";

            // Keep the button clickable during cooldown so HeroAbilityTargetingController can request a
            // server-confirmed cooldown toast. Only life state disables it completely.
            if (abilityButton != null) abilityButton.interactable = boundHero.CanAct;
        }

        private void RefreshSquadOrder()
        {
            if (squadOrderText == null) return;

            var count = 0;
            var longestRemaining = 0f;
            var monsters = MonsterCharacter.Instances;
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster == null || monster.IsDead || monster.Side != boundHero.Side ||
                    !monster.HasTacticalFocusOrder)
                    continue;
                count++;
                longestRemaining = Mathf.Max(longestRemaining, monster.TacticalFocusOrderRemaining);
            }

            var show = count > 0;
            if (squadOrderText.gameObject.activeSelf != show) squadOrderText.gameObject.SetActive(show);
            if (show) squadOrderText.text = $"SQUAD ORDER ×{count}  •  {Mathf.CeilToInt(longestRemaining)}s";
        }

        private void RefreshBreachRing()
        {
            if (breachRingText == null) return;
            var rings = BreachRingController.Instance;
            var show = rings != null && rings.IsSpawned && rings.HasRingObjectives;
            if (breachRingText.gameObject.activeSelf != show) breachRingText.gameObject.SetActive(show);
            if (!show) return;

            if (!rings.IsConfigurationValid)
            {
                breachRingText.text = "BREACH RINGS • CONFIG INVALID";
                return;
            }

            if (rings.CoreUnlocked)
            {
                breachRingText.text = "RING 3/3 CLEARED  •  CORE EXPOSED";
                return;
            }

            var total = rings.CurrentTotal;
            var destroyed = Mathf.Max(0, total - rings.CurrentRemaining);
            var ringName = rings.CurrentRing.ToString().ToUpperInvariant();
            breachRingText.text =
                $"RING {rings.BreachedRingCount + 1}/3  •  {ringName} {destroyed}/{total}  •  LOOT +{rings.CurrentRingSecuredBonus}";
        }

        private void HandleFeedback(HeroFeedback feedback, int value)
        {
            if (feedbackText == null) return;
            var displayName = boundHero != null && boundHero.TacticalAbility != null
                ? boundHero.TacticalAbility.displayName
                : null;
            var abilityName = string.IsNullOrWhiteSpace(displayName)
                ? "TACTICAL ABILITY"
                : displayName.ToUpperInvariant();
            feedbackText.text = feedback switch
            {
                HeroFeedback.LootCollected => $"LOOT COLLECTED +{value}",
                HeroFeedback.ExtractionCompleted => "EXTRACTION SUCCESSFUL",
                HeroFeedback.ExtractionRejected => "EXTRACTION REJECTED — COLLECT LOOT FIRST",
                HeroFeedback.NothingNearby => "NOTHING TO INTERACT WITH",
                HeroFeedback.InteractionUnavailable => "INTERACTION UNAVAILABLE",
                HeroFeedback.Revived => $"HERO REVIVED +{value} HP",
                HeroFeedback.ReviveRejected => "REVIVE UNAVAILABLE",
                HeroFeedback.AbilityCast => $"{abilityName} HIT {value} TARGET(S)",
                HeroFeedback.AbilityCooldown => $"{abilityName} COOLDOWN {value}s",
                HeroFeedback.AbilityOutOfRange => "TARGET OUT OF RANGE",
                HeroFeedback.AbilityNoTargets => "NO DEFENSIVE TARGET IN BLAST RADIUS",
                HeroFeedback.AbilityUnavailable => "TACTICAL ABILITY UNAVAILABLE",
                HeroFeedback.FocusTargetSet => value > 0
                    ? $"FOCUS TARGET CONFIRMED • SQUAD ×{value}"
                    : "FOCUS TARGET CONFIRMED • HERO ONLY",
                HeroFeedback.FocusTargetCleared => "FOCUS TARGET CLEARED",
                HeroFeedback.FocusTargetRejected => "INVALID FOCUS TARGET",
                HeroFeedback.FocusTargetCompleted => "FOCUS TARGET ELIMINATED",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(feedbackText.text)) return;
            if (feedbackRoot != null) feedbackRoot.SetActive(true);
            feedbackHideAt = Time.unscaledTime + feedbackDurationSeconds;
        }

        // UI Button hooks. The server still validates ownership/state and performs the mutation.
        public void ToggleControlMode()
        {
            if (boundHero == null || !boundHero.IsOwner) return;
            var next = boundHero.ControlMode == HeroControlMode.Auto
                ? HeroControlMode.Manual
                : HeroControlMode.Auto;
            boundHero.RequestSetControlModeServerRpc(next);
        }

        public void Interact()
        {
            if (boundHero != null && boundHero.IsOwner) boundHero.RequestInteractServerRpc();
        }
    }
}
