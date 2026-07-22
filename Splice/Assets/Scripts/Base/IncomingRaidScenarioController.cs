using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Splice.Backend;
using Splice.Characters;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Splice.Network;
using Splice.Scenes;
using Splice.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Splice.Base
{
    // Step 6E visible proof of the async-war loop. The local player watches their latest immutable town
    // snapshot from the defender camera while a synthetic remote Hero and army use the real raid runtime.
    // The scenario records a Defense Report but deliberately never mutates either local wallet.
    public sealed class IncomingRaidScenarioController : MonoBehaviour
    {
        [Header("Automatic Play Mode Demo")]
        [SerializeField] private bool autoStartOnPlay = true;
        [Min(0f)] [SerializeField] private float startupDelaySeconds = .8f;
        [Min(2f)] [SerializeField] private float serverReadyTimeoutSeconds = 15f;
        [Min(10f)] [SerializeField] private float scenarioTimeLimitSeconds = 150f;
        [SerializeField] private string simulatedAttackerAccountId = "sim_raider_001";

        [Header("Raid Wave")]
        [Min(1)] [SerializeField] private int initialUnitCount = 5;
        [Min(0)] [SerializeField] private int reinforcementWaveCount = 2;
        [Min(1)] [SerializeField] private int unitsPerReinforcement = 3;
        [Min(.1f)] [SerializeField] private float waveIntervalSeconds = 4f;
        [Min(0f)] [SerializeField] private float delayBetweenUnits = .18f;

        [Header("Scene References (auto-resolved when empty)")]
        [SerializeField] private RaidSceneAdapter sceneAdapter;
        [SerializeField] private RaidSnapshotLoader snapshotLoader;
        [SerializeField] private DeploymentManager deploymentManager;
        [SerializeField] private SideSelectionController sideSelectionController;
        [SerializeField] private RaidManager raidManager;
        [SerializeField] private BreachRingController breachRingController;
        [SerializeField] private RaidSceneCompositionController sceneCompositionController;

        private TMP_Text statusLabel;
        private readonly List<string> raidCardIds = new();
        private bool starting;
        private bool running;
        private bool subscribed;
        private int currentWave;
        private int spawnedUnitCount;
        private string activeRaidId = string.Empty;
        private string lastError = string.Empty;
        private float nextStatusRefresh;
        private CancellationTokenSource lifetimeCancellation;

        public bool IsStarting => starting;
        public bool IsRunning => running;
        public int CurrentWave => currentWave;
        public int SpawnedUnitCount => spawnedUnitCount;
        public string LastError => lastError;

        private void Awake() => lifetimeCancellation = new CancellationTokenSource();

        private IEnumerator Start()
        {
            ResolveReferences();
            EnsureStatusBanner();
            if (!autoStartOnPlay) yield break;
            if (startupDelaySeconds > 0f) yield return new WaitForSecondsRealtime(startupDelaySeconds);
            BeginIncomingRaid();
        }

        private void Update()
        {
            if (!running || Time.unscaledTime < nextStatusRefresh) return;
            nextStatusRefresh = Time.unscaledTime + .2f;
            RefreshRunningStatus();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        [ContextMenu("Debug/Begin Incoming Raid")]
        public void BeginIncomingRaid()
        {
            if (!isActiveAndEnabled || starting || running) return;
            StartCoroutine(BeginRoutine());
        }

        private IEnumerator BeginRoutine()
        {
            starting = true;
            lastError = string.Empty;
            ResolveReferences();
            sceneCompositionController?.RequestMode(RaidPresentationMode.Defender);
            EnsureStatusBanner();
            SetStatus("<color=#FFBD66>INCOMING RAID</color>\nConnecting defender command…");

            var deadline = Time.realtimeSinceStartup + serverReadyTimeoutSeconds;
            while (!ServerRuntimeReady() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!ServerRuntimeReady())
            {
                Fail("Network raid runtime did not become server-ready in time.");
                yield break;
            }

            var defenderTask = ResolveDefenderSnapshotAsync(lifetimeCancellation.Token);
            while (!defenderTask.IsCompleted) yield return null;
            if (defenderTask.IsCanceled)
            {
                starting = false;
                yield break;
            }
            if (defenderTask.IsFaulted)
            {
                Fail(defenderTask.Exception?.GetBaseException().Message ?? "Defender snapshot service failed.");
                yield break;
            }
            var defenderSnapshot = defenderTask.Result;

            if (!ResolveRaidCards(defenderSnapshot.factionId, out var cardError))
            {
                Fail(cardError);
                yield break;
            }

            var attackerTask = CreateSimulatedAttackerSnapshotAsync(
                defenderSnapshot, lifetimeCancellation.Token);
            while (!attackerTask.IsCompleted) yield return null;
            if (attackerTask.IsCanceled)
            {
                starting = false;
                yield break;
            }
            if (attackerTask.IsFaulted)
            {
                Fail("Could not create the simulated attacker snapshot: " +
                     attackerTask.Exception?.GetBaseException().Message);
                yield break;
            }
            var attackerSnapshot = attackerTask.Result;

            if (!IncomingRaidScenarioContract.TryBuildTarget(defenderSnapshot, attackerSnapshot,
                    simulatedAttackerAccountId, out var target, out var targetError))
            {
                Fail(targetError);
                yield break;
            }

            if (!sceneAdapter.ValidateScene(out var sceneContract))
            {
                Fail("Raid scene contract failed: " + sceneContract.ErrorSummary);
                yield break;
            }

            RaidContext.Clear();
            RaidSessionContext.Clear();
            if (!RaidContext.TrySelectTarget(target, attackerSnapshot.factionId,
                    simulatedAttackerAccountId, out var selectError))
            {
                Fail(selectError);
                yield break;
            }
            if (!RaidSessionContext.TryPrepare(target, simulatedAttackerAccountId,
                    SceneManager.GetActiveScene().name, sceneContract, out var prepareError))
            {
                Fail(prepareError);
                yield break;
            }

            activeRaidId = "incoming_" + Guid.NewGuid().ToString("N");
            Subscribe();
            if (!RaidSessionContext.TryBindAndStart(activeRaidId, target, sceneContract, out var startError))
            {
                Unsubscribe();
                Fail(startError);
                yield break;
            }
            if (!snapshotLoader.TryLoadSelectedTarget(out var loadError))
            {
                RaidSessionContext.AbortBeforeGameplay(loadError);
                Unsubscribe();
                Fail(loadError);
                yield break;
            }

            raidManager.TrySetScenarioTimeLimit(scenarioTimeLimitSeconds);
            sideSelectionController.EnterIncomingDefenseView();
            SetStatus("<color=#FF695E>⚠ INCOMING RAID</color>\nDeploying enemy Hero and assault wave…");

            deadline = Time.realtimeSinceStartup + serverReadyTimeoutSeconds;
            while (!snapshotLoader.HasCompletedLoading && Time.realtimeSinceStartup < deadline) yield return null;
            if (!snapshotLoader.HasCompletedLoading)
            {
                Fail("Defender snapshot loading timed out.");
                yield break;
            }

            starting = false;
            running = true;
            currentWave = 1;
            yield return SpawnWave(initialUnitCount);

            // Keep the raid alive until its first visible threats exist, then remove artificial attacker Gold
            // so Army Eliminated can still end the simulation correctly.
            GoldController.For(RaidSide.Attacker)?.SetBalance(0);

            for (var reinforcement = 0; reinforcement < reinforcementWaveCount; reinforcement++)
            {
                var waitUntil = Time.realtimeSinceStartup + waveIntervalSeconds;
                while (!raidManager.IsOver && Time.realtimeSinceStartup < waitUntil) yield return null;
                if (raidManager.IsOver) yield break;
                currentWave++;
                yield return SpawnWave(unitsPerReinforcement);
            }
        }

        private IEnumerator SpawnWave(int count)
        {
            for (var i = 0; i < count && !raidManager.IsOver; i++)
            {
                var cardId = raidCardIds[(spawnedUnitCount + i) % raidCardIds.Count];
                var lane = deploymentManager.LaneCount > 0 ? (spawnedUnitCount + i) % deploymentManager.LaneCount : 0;
                if (deploymentManager.TrySpawnScenarioMonster(cardId, lane, out var error))
                    spawnedUnitCount++;
                else
                    Debug.LogWarning("[IncomingRaid] " + error, this);
                RefreshRunningStatus();
                if (delayBetweenUnits > 0f) yield return new WaitForSeconds(delayBetweenUnits);
            }
        }

        private async Task<TownDefenseSnapshot> ResolveDefenderSnapshotAsync(
            CancellationToken cancellationToken)
        {
            var registeredIds = new List<string>();
            var registry = snapshotLoader != null ? snapshotLoader.Registry : null;
            if (registry != null)
            {
                for (var i = 0; i < registry.Factions.Count; i++)
                    if (registry.Factions[i] != null) registeredIds.Add(registry.Factions[i].factionId);
            }
            var snapshots = await SpliceServiceHub.TownSnapshots.GetLatestManyAsync(
                registeredIds, cancellationToken);
            var availableIds = new HashSet<string>();
            for (var i = 0; i < snapshots.Count; i++)
                if (snapshots[i] != null) availableIds.Add(snapshots[i].factionId);
            for (var i = 0; i < registeredIds.Count; i++)
            {
                if (availableIds.Contains(registeredIds[i])) continue;
                var draftCandidate = await SpliceServiceHub.TownSnapshots.GetCheckedOutDraftAsync(
                    registeredIds[i], cancellationToken);
                if (draftCandidate?.exists == true) availableIds.Add(registeredIds[i]);
            }

            var factionId = IncomingRaidScenarioContract.ResolveDefenderFactionId(
                PlayerProfile.ActiveFactionId, registeredIds, availableIds.Contains);
            if (string.IsNullOrWhiteSpace(factionId))
                throw new InvalidOperationException(
                    "No playable town was found. Choose a faction, build and Checkout at least one defense first.");
            if (string.IsNullOrWhiteSpace(PlayerProfile.ActiveFactionId))
                PlayerProfile.ActiveFactionId = factionId;

            TownDefenseSnapshot snapshot = null;
            for (var i = 0; i < snapshots.Count; i++)
                if (snapshots[i] != null && snapshots[i].factionId == factionId)
                    snapshot = snapshots[i];
            if (snapshot != null) return snapshot;

            // Friendly first-run fallback: a checked-out layout can be deployed automatically, but a Draft
            // is never captured. This preserves the immutable snapshot rule while keeping Play useful.
            var draft = await SpliceServiceHub.TownSnapshots.GetCheckedOutDraftAsync(
                factionId, cancellationToken);
            var layout = draft?.checkedOutLayout;
            var used = (layout?.towers?.Count ?? 0) + (layout?.garrison?.Count ?? 0);
            if (layout == null || used <= 0)
                throw new InvalidOperationException(
                    "No deployed town snapshot exists. Build and Checkout at least one defense first.");
            if (layout.ownerAccountId != PlayerProfile.AccountId)
                throw new InvalidOperationException(
                    "The checked-out town does not belong to the active local account.");

            var deployed = await SpliceServiceHub.TownSnapshots.DeployAsync(new DeployTownRequest
            {
                checkedOutLayout = layout,
                usedCapacity = used,
                maxCapacity = Mathf.Max(used, 20),
            }, "incoming_defender_" + Guid.NewGuid().ToString("N"), cancellationToken);
            if (!deployed.success)
                throw new InvalidOperationException(
                    "The checked-out town cannot be deployed: " + deployed.error);
            return deployed.snapshot;
        }

        private async Task<TownDefenseSnapshot> CreateSimulatedAttackerSnapshotAsync(
            TownDefenseSnapshot defender, CancellationToken cancellationToken)
        {
            var layout = JsonUtility.FromJson<BaseLayout>(JsonUtility.ToJson(defender.layout));
            layout.ownerAccountId = simulatedAttackerAccountId;
            layout.factionId = "sim-incoming-" + defender.factionId;
            layout.storedGold = Mathf.Max(400, layout.storedGold);
            var deployed = await SpliceServiceHub.TownSnapshots.DeployAsync(new DeployTownRequest
            {
                checkedOutLayout = layout,
                usedCapacity = defender.usedCapacity,
                maxCapacity = Mathf.Max(defender.usedCapacity, defender.maxCapacity),
            }, "incoming_attacker_" + Guid.NewGuid().ToString("N"), cancellationToken);
            if (!deployed.success) throw new InvalidOperationException(deployed.error);
            return deployed.snapshot;
        }

        private bool ResolveRaidCards(string factionId, out string error)
        {
            raidCardIds.Clear();
            var registry = snapshotLoader != null ? snapshotLoader.Registry : null;
            var faction = registry != null ? registry.GetFaction(factionId) : null;
            if (faction != null)
            {
                foreach (var card in faction.cards)
                {
                    if (card == null || card.linkedMonster == null || card.linkedMonster.prefab == null) continue;
                    var id = registry.IdOf(card);
                    if (!string.IsNullOrWhiteSpace(id)) raidCardIds.Add(id);
                }
            }
            if (raidCardIds.Count == 0 && registry != null)
            {
                foreach (var card in registry.AllCards())
                {
                    if (card == null || card.linkedMonster == null || card.linkedMonster.prefab == null) continue;
                    var id = registry.IdOf(card);
                    if (!string.IsNullOrWhiteSpace(id)) raidCardIds.Add(id);
                }
            }

            error = raidCardIds.Count > 0
                ? string.Empty
                : "No registered Monster card is available for the incoming assault wave.";
            return raidCardIds.Count > 0;
        }

        private bool ServerRuntimeReady()
        {
            var net = NetworkManager.Singleton;
            return net != null && net.IsServer && sceneAdapter != null && snapshotLoader != null &&
                   snapshotLoader.IsSpawned && deploymentManager != null && deploymentManager.IsSpawned &&
                   deploymentManager.LaneCount > 0 && sideSelectionController != null &&
                   raidManager != null && raidManager.IsSpawned &&
                   (sceneCompositionController == null ||
                    sceneCompositionController.IsModeReady(RaidPresentationMode.Defender));
        }

        private void ResolveReferences()
        {
            if (sceneAdapter == null) sceneAdapter = FindFirstObjectByType<RaidSceneAdapter>();
            if (snapshotLoader == null) snapshotLoader = FindFirstObjectByType<RaidSnapshotLoader>();
            if (deploymentManager == null) deploymentManager = FindFirstObjectByType<DeploymentManager>();
            if (sideSelectionController == null) sideSelectionController = FindFirstObjectByType<SideSelectionController>();
            if (raidManager == null) raidManager = FindFirstObjectByType<RaidManager>();
            if (breachRingController == null) breachRingController = FindFirstObjectByType<BreachRingController>();
            if (sceneCompositionController == null)
                sceneCompositionController = FindFirstObjectByType<RaidSceneCompositionController>();
        }

        private void Subscribe()
        {
            if (subscribed || raidManager == null) return;
            raidManager.OnRaidEnded += HandleRaidEnded;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || raidManager == null) return;
            raidManager.OnRaidEnded -= HandleRaidEnded;
            subscribed = false;
        }

        private void HandleRaidEnded(RaidOutcome outcome) => _ = HandleRaidEndedAsync(outcome);

        private async Task HandleRaidEndedAsync(RaidOutcome outcome)
        {
            if (string.IsNullOrWhiteSpace(activeRaidId)) return;
            var rings = breachRingController != null ? breachRingController.BreachedRingCount : 0;
            RaidSessionContext.MarkCompleted(outcome, rings);
            var transaction = IncomingRaidScenarioContract.BuildSimulatedAttackerSettlement(
                activeRaidId, outcome, rings, RaidContext.Target?.displayName ?? "Local Town");
            RaidReportWriteResult write;
            try
            {
                write = await SpliceServiceHub.RaidReports.RecordCompletedAsync(
                    RaidSessionContext.Current, transaction, 0,
                    lifetimeCancellation?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            running = false;
            starting = false;
            var headline = outcome switch
            {
                RaidOutcome.FullVictory => "<color=#FF695E>DEFENSE BREACHED</color>",
                RaidOutcome.Extracted => "<color=#FFBD66>RAIDERS ESCAPED</color>",
                _ => "<color=#62F2B0>DEFENSE HELD</color>",
            };
            SetStatus(headline + "\n" + (write.success
                ? $"Defense Report saved • {ShortId(write.report.reportId)}"
                : "Report pending • " + write.error));
            activeRaidId = string.Empty;
            Unsubscribe();
        }

        private void RefreshRunningStatus()
        {
            if (statusLabel == null || raidManager == null) return;
            var alive = 0;
            var monsters = MonsterCharacter.Active;
            for (var i = 0; i < monsters.Count; i++)
                if (monsters[i] != null && monsters[i].Side == RaidSide.Attacker && !monsters[i].IsDead) alive++;
            var heroState = RaidHeroCharacter.Instance != null
                ? RaidHeroCharacter.Instance.LifeState.ToString().ToUpperInvariant()
                : "DEPLOYING";
            var rings = breachRingController != null ? breachRingController.BreachedRingCount : 0;
            SetStatus($"<color=#FF695E>⚠ INCOMING RAID • WAVE {currentWave}</color>\n" +
                      $"ENEMY HERO {heroState}  •  RAIDERS {alive}  •  BREACH {rings}/3  •  " +
                      $"TIME {Mathf.CeilToInt(raidManager.RemainingSeconds)}s");
        }

        private void EnsureStatusBanner()
        {
            if (statusLabel != null) return;
            Canvas canvas = null;
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == "CanvasTOP") { canvas = canvases[i]; break; }
                if (canvas == null && canvases[i].isRootCanvas) canvas = canvases[i];
            }
            if (canvas == null) return;
            canvas.overrideSorting = true;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 300);

            var banner = new GameObject("IncomingRaidStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            banner.transform.SetParent(canvas.transform, false);
            banner.transform.SetAsLastSibling();
            var rect = (RectTransform)banner.transform;
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -105f);
            rect.sizeDelta = new Vector2(920f, 98f);
            var image = banner.GetComponent<Image>();
            image.raycastTarget = false;
            if (!SpliceUiSkinLibrary.ApplyHeader(image, new Color(1f, .9f, .94f, .97f)))
                image.color = new Color(.08f, .09f, .16f, .94f);

            var labelObject = new GameObject("Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(banner.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(34f, 13f);
            labelRect.offsetMax = new Vector2(-34f, -13f);
            statusLabel = labelObject.GetComponent<TextMeshProUGUI>();
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.fontSize = 25f;
            statusLabel.fontStyle = FontStyles.Bold;
            statusLabel.color = Color.white;
            statusLabel.enableAutoSizing = true;
            statusLabel.fontSizeMin = 16f;
            statusLabel.fontSizeMax = 25f;
            statusLabel.raycastTarget = false;
        }

        private void SetStatus(string value)
        {
            EnsureStatusBanner();
            if (statusLabel != null) statusLabel.text = value;
        }

        private void Fail(string error)
        {
            starting = false;
            running = false;
            lastError = error ?? "Incoming raid setup failed.";
            SetStatus("<color=#FF695E>INCOMING RAID UNAVAILABLE</color>\n" + lastError);
            Debug.LogError("[IncomingRaid] " + lastError, this);
        }

        private static string ShortId(string value) =>
            string.IsNullOrWhiteSpace(value) ? "unknown" : value.Substring(0, Mathf.Min(18, value.Length));
    }
}
