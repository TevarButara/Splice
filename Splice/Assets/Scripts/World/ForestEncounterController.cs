using System.Collections.Generic;
using Splice.Data;
using Splice.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Splice.World
{
    public sealed class ForestEncounterController : MonoBehaviour
    {
        [SerializeField] private ForestZoneDefinitionSO definition;
        [SerializeField] private ForestHeroController hero;
        [SerializeField] private Transform monsterRoot;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text lootText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button extractButton;
        [SerializeField] private Button returnButton;
        [Min(0.5f), SerializeField] private float attackRange = 4f;
        [Min(1), SerializeField] private int attackDamage = 50;

        private readonly List<ForestMonsterTarget> monsters = new();
        private float remainingSeconds;
        private int carriedFragments;
        private bool ended;

        public bool HasEditorAuthoredUi =>
            hero != null && monsterRoot != null && timerText != null && lootText != null &&
            objectiveText != null && attackButton != null && extractButton != null &&
            returnButton != null;
        public int AliveMonsterCount
        {
            get
            {
                var count = 0;
                foreach (var monster in monsters) if (monster != null && monster.IsAlive) count++;
                return count;
            }
        }
        public int CarriedFragments => carriedFragments;
        public bool HasEnded => ended;

        private void Awake()
        {
            if (!HasEditorAuthoredUi)
            {
                Debug.LogError("[Forest] Editor-authored scene contract is incomplete.", this);
                enabled = false;
                return;
            }
            remainingSeconds = definition != null ? definition.EncounterDurationSeconds : 60;
            monsters.AddRange(monsterRoot.GetComponentsInChildren<ForestMonsterTarget>(true));
            foreach (var monster in monsters) monster.Defeated += OnMonsterDefeated;
            Bind(attackButton, AttackNearest);
            Bind(extractButton, Extract);
            Bind(returnButton, PrototypeFlowRouter.LoadWorldMap);
            returnButton.gameObject.SetActive(false);
            Render();
        }

        private void OnDestroy()
        {
            foreach (var monster in monsters)
                if (monster != null) monster.Defeated -= OnMonsterDefeated;
        }

        private void Update()
        {
            if (ended) return;
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                AttackNearest();
            if (remainingSeconds <= 0f) FailTimeout();
            Render();
        }

        public void AttackNearest()
        {
            if (ended || hero == null) return;
            ForestMonsterTarget nearest = null;
            var best = attackRange * attackRange;
            foreach (var monster in monsters)
            {
                if (monster == null || !monster.IsAlive) continue;
                var distance = (monster.transform.position - hero.transform.position).sqrMagnitude;
                if (distance > best) continue;
                best = distance;
                nearest = monster;
            }
            if (nearest == null)
            {
                objectiveText.text = "NO TARGET IN RANGE • MOVE CLOSER";
                return;
            }
            var direction = nearest.transform.position - hero.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                hero.transform.rotation = Quaternion.LookRotation(direction);
            nearest.TakeDamage(attackDamage);
        }

        public void Extract()
        {
            if (ended) return;
            ended = true;
            var settlement = ForestHuntProgressStore.Settle(carriedFragments,
                definition != null ? definition.FragmentsPerDiamond : 100,
                definition != null ? definition.WeeklyDiamondCap : 3);
            objectiveText.text = settlement.convertedDiamonds > 0
                ? $"EXTRACTED • +{settlement.securedFragments} FRAGMENTS • +{settlement.convertedDiamonds} DIAMOND"
                : $"EXTRACTED • +{settlement.securedFragments} FRAGMENTS SECURED";
            attackButton.interactable = false;
            extractButton.interactable = false;
            returnButton.gameObject.SetActive(true);
            Render();
        }

        private void FailTimeout()
        {
            ended = true;
            carriedFragments = 0;
            objectiveText.text = "HUNT FAILED • TIME EXPIRED • CARRIED LOOT LOST";
            attackButton.interactable = false;
            extractButton.interactable = false;
            returnButton.gameObject.SetActive(true);
            Render();
        }

        private void OnMonsterDefeated(ForestMonsterTarget target, int fragments)
        {
            carriedFragments += Mathf.Max(0, fragments);
            objectiveText.text = AliveMonsterCount == 0
                ? "AREA CLEARED • EXTRACT NOW"
                : $"+{fragments} FRAGMENTS • {AliveMonsterCount} HOSTILES REMAIN";
        }

        private void Render()
        {
            timerText.text = $"TIME  {Mathf.CeilToInt(remainingSeconds):00}";
            lootText.text = $"CARRIED  {carriedFragments:N0}  •  HOSTILES  {AliveMonsterCount}";
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }
}
