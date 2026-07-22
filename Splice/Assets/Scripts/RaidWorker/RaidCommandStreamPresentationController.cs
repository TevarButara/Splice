using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Splice.Characters;
using Splice.Data;
using Splice.Network;
using Splice.Scenes;
using Splice.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.RaidWorker
{
    // C4C2B read-only visual adapter. It may interpolate and decorate command stream state, but it never
    // writes combat outcomes or settlement data back into the fixed-tick kernel (architecture §4.1).
    public sealed class RaidCommandStreamPresentationController : MonoBehaviour
    {
        [Header("Development Auto Demo")]
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private bool developmentBuildOnly = true;
        [Min(0f)] [SerializeField] private float initialDelaySeconds = 1.2f;
        [Min(.1f)] [SerializeField] private float playbackSpeed = 6f;
        [SerializeField] private bool preferContentPrefabs = true;

        [Header("RaidArena Anchors")]
        [SerializeField] private Vector3 entryPoint = new(0f, 3f, -90f);
        [SerializeField] private Vector3 outerRingPoint = new(0f, 3f, 80f);
        [SerializeField] private Vector3 innerRingPoint = new(0f, 3f, 270f);
        [SerializeField] private Vector3 coreRingPoint = new(0f, 3f, 470f);

        private readonly List<GameObject> ringBarriers = new();
        private readonly List<Material> runtimeMaterials = new();
        private Transform visualRoot;
        private Transform attackersRoot;
        private Canvas overlayCanvas;
        private TMP_Text titleLabel;
        private TMP_Text statusLabel;
        private Image progressFill;
        private Coroutine playback;
        private RaidSimulationResult activeResult;
        private int currentTick;
        private bool suppressSceneRouting;

        public bool IsPlaying { get; private set; }
        public bool IsComplete { get; private set; }
        public int SpawnedActorCount { get; private set; }
        public int VisibleActorCount => attackersRoot != null ? attackersRoot.childCount : 0;
        public string LastCommandType { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        private IEnumerator Start()
        {
            if (!autoStartOnPlay || (developmentBuildOnly && !Application.isEditor && !Debug.isDebugBuild))
                yield break;
            if (initialDelaySeconds > 0f) yield return new WaitForSecondsRealtime(initialDelaySeconds);
            BeginLocalDemo();
        }

        private void OnDestroy()
        {
            if (playback != null) StopCoroutine(playback);
            for (var i = 0; i < runtimeMaterials.Count; i++)
                if (runtimeMaterials[i] != null) Destroy(runtimeMaterials[i]);
        }

        [ContextMenu("Debug/Play Authoritative Command Stream Demo")]
        public void BeginLocalDemo()
        {
            // This scene still contains the legacy developer role picker. An authoritative replay is
            // already a defender-view route, so close that picker before presenting the command stream.
            // EnterIncomingDefenseView also disables placement input without mutating combat authority.
            var sideSelection = FindFirstObjectByType<SideSelectionController>();
            if (sideSelection != null)
            {
                sideSelection.CloseSelectionPanelForReplay();
                if (!suppressSceneRouting) sideSelection.EnterIncomingDefenseView();
            }
            else if (!suppressSceneRouting)
                RaidSceneCompositionController.Instance?.RequestMode(RaidPresentationMode.Defender);
            var input = BuildLocalDemoInput();
            Play(input, FixedTickRaidSimulator.Simulate(input));
        }

        public void Play(FixedTickRaidSimulationInput input, RaidSimulationResult result)
        {
            LastError = string.Empty;
            if (!TryValidateStream(result, out var error))
            {
                LastError = error;
                Debug.LogError("[RaidReplay] " + error, this);
                return;
            }

            if (playback != null) StopCoroutine(playback);
            ClearVisuals();
            BuildOverlay(result);
            ResolveSceneAnchors();
            BuildWorld(input);
            activeResult = result;
            currentTick = 0;
            IsComplete = false;
            IsPlaying = true;
            LastCommandType = string.Empty;
            playback = StartCoroutine(Replay(result));
        }

        public void ConfigureForTests(float speed = 100f)
        {
            autoStartOnPlay = false;
            developmentBuildOnly = false;
            initialDelaySeconds = 0f;
            playbackSpeed = Mathf.Max(.1f, speed);
            preferContentPrefabs = false;
            suppressSceneRouting = true;
        }

        public static void CalculateVisibleRaidAnchors(Vector3 spawn, Vector3 core, out Vector3 entry,
            out Vector3 outer, out Vector3 inner, out Vector3 coreRing)
        {
            // DefenderCamera is deliberately framed tightly around the authored town, while the true world
            // spawn is far beyond its frustum. Project the abstract kernel rings onto the visible final lane.
            entry = Vector3.Lerp(spawn, core, .81f);
            outer = Vector3.Lerp(spawn, core, .84f);
            inner = Vector3.Lerp(spawn, core, .88f);
            coreRing = Vector3.Lerp(spawn, core, .92f);
        }

        private void ResolveSceneAnchors()
        {
            var core = FortCore.Instance != null ? FortCore.Instance : FindFirstObjectByType<FortCore>();
            var spawner = FindFirstObjectByType<RaidHeroSpawner>();
            if (core == null || spawner == null || spawner.SpawnPoint == null) return;
            CalculateVisibleRaidAnchors(spawner.SpawnPoint.position, core.transform.position,
                out entryPoint, out outerRingPoint, out innerRingPoint, out coreRingPoint);
        }

        public static bool TryValidateStream(RaidSimulationResult result, out string error)
        {
            if (result == null || result.simulationVersion != FixedTickRaidSimulator.SimulationVersion)
            {
                error = "Unsupported or missing simulation version.";
                return false;
            }
            if (result.commands == null || result.commands.Count == 0 ||
                result.commandCount != result.commands.Count)
            {
                error = "Command stream is missing or count does not match.";
                return false;
            }
            var previousTick = -1;
            for (var i = 0; i < result.commands.Count; i++)
            {
                var command = result.commands[i];
                if (command == null || command.tick < previousTick || string.IsNullOrWhiteSpace(command.type))
                {
                    error = "Command stream order or payload is invalid.";
                    return false;
                }
                previousTick = command.tick;
            }
            if (result.commands[^1].type != "COMPLETE" || !IsSha256(result.commandStreamHash) ||
                !IsSha256(result.simulationHash))
            {
                error = "Command stream has no valid completion/hash.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private IEnumerator Replay(RaidSimulationResult result)
        {
            var previousTick = 0;
            for (var i = 0; i < result.commands.Count; i++)
            {
                var command = result.commands[i];
                var waitSeconds = (command.tick - previousTick) *
                                  FixedTickRaidSimulator.TickMilliseconds / 1000f / playbackSpeed;
                if (waitSeconds > 0f) yield return new WaitForSecondsRealtime(waitSeconds);
                currentTick = command.tick;
                LastCommandType = command.type;
                Apply(command);
                previousTick = command.tick;
            }
            IsPlaying = false;
            IsComplete = true;
            playback = null;
        }

        private void Apply(RaidSimulationCommand command)
        {
            var targetPosition = TargetPosition(command.target);
            switch (command.type)
            {
                case "SPAWN":
                    SetStatus("DEPLOYED", "Hero and assault army entered the raid.", 0, Gold());
                    break;
                case "MOVE":
                    var duration = command.value * FixedTickRaidSimulator.TickMilliseconds / 1000f /
                                   playbackSpeed;
                    StartCoroutine(MoveAttackers(targetPosition, Mathf.Max(.01f, duration)));
                    SetStatus("ADVANCING", "Moving toward " + DisplayTarget(command.target) + "…",
                        RingIndex(command.target), Cyan());
                    break;
                case "ENGAGE":
                    SetStatus("BREACH IN PROGRESS", DisplayTarget(command.target) +
                              " defense integrity " + command.value, RingIndex(command.target), Orange());
                    Pulse(targetPosition, Orange(), 3.5f);
                    break;
                case "ATTACK":
                    Pulse(command.actor.StartsWith("ring-", StringComparison.Ordinal)
                        ? attackersRoot.position : targetPosition,
                        command.actor.StartsWith("ring-", StringComparison.Ordinal) ? Red() : Cyan(), 2.1f);
                    break;
                case "ABILITY":
                    Pulse(targetPosition, Magenta(), 6f);
                    SetStatus("HERO ABILITY", "Breach Charge dealt " + command.value + " damage.",
                        RingIndex(command.target), Magenta());
                    break;
                case "BREACH":
                    var breached = Mathf.Clamp((int)command.value, 1, 3);
                    if (breached - 1 < ringBarriers.Count && ringBarriers[breached - 1] != null)
                        ringBarriers[breached - 1].SetActive(false);
                    Pulse(targetPosition, Gold(), 8f);
                    SetStatus("RING " + breached + " BREACHED", "The assault force is pushing deeper.",
                        breached, Gold());
                    break;
                case "COMPLETE":
                    var victory = command.target == "FULL_VICTORY";
                    SetStatus(victory ? "FULL VICTORY" : command.target.Replace('_', ' '),
                        "Authoritative result • " + activeResult.commandStreamHash[..12] + "…",
                        activeResult.breachedRings, victory ? Green() : Red());
                    break;
            }
            RefreshProgress();
        }

        private IEnumerator MoveAttackers(Vector3 destination, float duration)
        {
            if (attackersRoot == null) yield break;
            var start = attackersRoot.position;
            var elapsed = 0f;
            while (elapsed < duration && attackersRoot != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                attackersRoot.position = Vector3.LerpUnclamped(start, destination, t);
                yield return null;
            }
            if (attackersRoot != null) attackersRoot.position = destination;
        }

        private void BuildWorld(FixedTickRaidSimulationInput input)
        {
            visualRoot = new GameObject("[Authoritative Command Stream Visuals]").transform;
            attackersRoot = new GameObject("Assault Formation").transform;
            attackersRoot.SetParent(visualRoot, false);
            attackersRoot.position = entryPoint;

            var heroPrefab = ResolveHeroPrefab(input.hero?.contentId);
            CreateActor("Hero Proxy", heroPrefab, new Vector3(0f, 0f, 0f),
                new Color(.15f, .95f, 1f), 2.8f);

            var actorIndex = 0;
            foreach (var entry in input.loadoutEntries ?? new List<RaidWorkerLoadoutEntry>())
            {
                var prefab = ResolveMonsterPrefab(entry.cardId);
                var count = Mathf.Clamp(entry.count, 1, 12 - actorIndex);
                for (var i = 0; i < count; i++)
                {
                    var row = actorIndex / 4;
                    var column = actorIndex % 4;
                    CreateActor("Army " + entry.cardId + " #" + (i + 1), prefab,
                        new Vector3((column - 1.5f) * 2.2f, 0f, -3f - row * 2.3f),
                        new Color(.25f, .85f, .45f), 2.1f);
                    actorIndex++;
                }
                if (actorIndex >= 12) break;
            }

            CreateBarrier("OUTER RING", outerRingPoint, new Color(1f, .55f, .12f), 1);
            CreateBarrier("INNER RING", innerRingPoint, new Color(.75f, .25f, 1f), 2);
            CreateBarrier("CORE RING", coreRingPoint, new Color(1f, .18f, .2f), 3);
        }

        private void CreateActor(string name, GameObject prefab, Vector3 localPosition, Color fallbackColor,
            float targetHeight)
        {
            // Networked content prefabs can legitimately tear themselves down when cloned outside a spawned
            // session. Keep a presentation-owned root + glowing base so a replay actor never disappears with
            // that lifecycle; the optional content model is decoration only.
            var actor = new GameObject(name);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Authority Marker";
            marker.transform.SetParent(actor.transform, false);
            marker.transform.localPosition = new Vector3(0f, .12f, 0f);
            marker.transform.localScale = name.StartsWith("Hero", StringComparison.Ordinal)
                ? new Vector3(2.3f, .12f, 2.3f)
                : new Vector3(1.65f, .1f, 1.65f);
            var markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null) markerCollider.enabled = false;
            ApplyMaterial(marker, fallbackColor, .8f);

            if (preferContentPrefabs && prefab != null)
            {
                var model = Instantiate(prefab, actor.transform, false);
                model.name = "Content Model";
                PrepareVisualProxy(model);
                NormalizeHeight(model, targetHeight);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }
            else
            {
                var body = GameObject.CreatePrimitive(name.StartsWith("Hero", StringComparison.Ordinal)
                    ? PrimitiveType.Capsule : PrimitiveType.Sphere);
                body.name = "Fallback Body";
                body.transform.SetParent(actor.transform, false);
                var collider = body.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                ApplyMaterial(body, fallbackColor, .25f);
                body.transform.localScale = name.StartsWith("Hero", StringComparison.Ordinal)
                    ? new Vector3(1.6f, 2.4f, 1.6f)
                    : Vector3.one * 1.8f;
            }
            actor.transform.SetParent(attackersRoot, false);
            actor.transform.localPosition = localPosition;
            actor.transform.localRotation = Quaternion.identity;
            SpawnedActorCount++;
        }

        private void CreateBarrier(string label, Vector3 position, Color color, int ring)
        {
            var root = new GameObject(label);
            root.transform.SetParent(visualRoot, false);
            root.transform.position = position;
            for (var i = -2; i <= 2; i++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = label + " Segment";
                block.transform.SetParent(root.transform, false);
                block.transform.localPosition = new Vector3(i * 3.3f, .35f, 0f);
                block.transform.localScale = new Vector3(3f, .7f, 1.2f);
                var collider = block.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                ApplyMaterial(block, color, .1f);
            }
            var text = new GameObject(label + " Label").AddComponent<TextMeshPro>();
            text.transform.SetParent(root.transform, false);
            text.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            text.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 5f;
            text.color = color;
            text.text = "RING " + ring;
            ringBarriers.Add(root);
        }

        private void BuildOverlay(RaidSimulationResult result)
        {
            var canvasObject = new GameObject("Authoritative Replay HUD", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlayCanvas = canvasObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 5000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);
            scaler.matchWidthOrHeight = .5f;

            var panel = new GameObject("Replay Status Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(.5f, 1f);
            panelRect.anchorMax = new Vector2(.5f, 1f);
            panelRect.pivot = new Vector2(.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -32f);
            panelRect.sizeDelta = new Vector2(690f, 170f);
            panel.GetComponent<Image>().color = new Color(.025f, .045f, .075f, .94f);

            titleLabel = CreateUiText(panel.transform, "AUTHORITY_TITLE", 30f, FontStyles.Bold,
                new Vector2(24f, -18f), new Vector2(-24f, 52f));
            titleLabel.text = "AUTHORITATIVE RAID REPLAY  •  ×" + playbackSpeed.ToString("0.#");
            titleLabel.color = Cyan();
            statusLabel = CreateUiText(panel.transform, "AUTHORITY_STATUS", 23f, FontStyles.Normal,
                new Vector2(24f, -61f), new Vector2(-24f, 92f));
            statusLabel.text = "Preparing immutable command stream…";

            var progressBack = new GameObject("Progress Track", typeof(RectTransform), typeof(Image));
            progressBack.transform.SetParent(panel.transform, false);
            var backRect = (RectTransform)progressBack.transform;
            backRect.anchorMin = new Vector2(0f, 0f);
            backRect.anchorMax = new Vector2(1f, 0f);
            backRect.pivot = new Vector2(.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 14f);
            backRect.sizeDelta = new Vector2(-48f, 12f);
            progressBack.GetComponent<Image>().color = new Color(.12f, .18f, .24f, 1f);
            var fill = new GameObject("Progress Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(progressBack.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            progressFill = fill.GetComponent<Image>();
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;
            progressFill.color = Cyan();
        }

        private static TMP_Text CreateUiText(Transform parent, string name, float fontSize,
            FontStyles style, Vector2 offsetMin, Vector2 size)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            value.transform.SetParent(parent, false);
            var rect = value.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.offsetMin = new Vector2(offsetMin.x, -size.y);
            rect.offsetMax = new Vector2(size.x, offsetMin.y);
            value.fontSize = fontSize;
            value.fontStyle = style;
            value.alignment = TextAlignmentOptions.TopLeft;
            value.color = Color.white;
            value.enableWordWrapping = false;
            return value;
        }

        private void SetStatus(string title, string detail, int breachedRings, Color color)
        {
            if (titleLabel != null)
            {
                titleLabel.text = title;
                titleLabel.color = color;
            }
            if (statusLabel != null)
                statusLabel.text = detail + "\n" + breachedRings + "/3 rings • tick " + currentTick;
            if (progressFill != null) progressFill.color = color;
        }

        private void RefreshProgress()
        {
            if (progressFill == null || activeResult == null) return;
            progressFill.fillAmount = Mathf.Clamp01(currentTick / Mathf.Max(1f, activeResult.tickCount));
        }

        private void Pulse(Vector3 position, Color color, float maximumScale)
        {
            var pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = "Command Pulse";
            pulse.transform.position = position + Vector3.up * 2f;
            var collider = pulse.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            ApplyMaterial(pulse, color, .55f);
            StartCoroutine(AnimatePulse(pulse, maximumScale));
        }

        private static IEnumerator AnimatePulse(GameObject pulse, float maximumScale)
        {
            var elapsed = 0f;
            const float duration = .28f;
            while (pulse != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                pulse.transform.localScale = Vector3.one * Mathf.Lerp(.3f, maximumScale, t);
                yield return null;
            }
            if (pulse != null) Destroy(pulse);
        }

        private void ApplyMaterial(GameObject target, Color color, float emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var material = new Material(shader);
            runtimeMaterials.Add(material);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", color * emission);
            }
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }

        private static void PrepareVisualProxy(GameObject proxy)
        {
            foreach (var behaviour in proxy.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (var collider in proxy.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in proxy.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (var source in proxy.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
        }

        private static void NormalizeHeight(GameObject actor, float targetHeight)
        {
            var renderers = actor.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= .01f) return;
            var scale = Mathf.Clamp(targetHeight / bounds.size.y, .15f, 3f);
            actor.transform.localScale *= scale;
        }

        private static GameObject ResolveHeroPrefab(string contentId)
        {
            var id = (contentId ?? string.Empty).Replace("hero/", string.Empty);
            foreach (var registry in Resources.FindObjectsOfTypeAll<HeroRegistrySO>())
            {
                var definition = registry.Resolve(id);
                if (definition?.prefab != null) return definition.prefab;
            }
            return null;
        }

        private static GameObject ResolveMonsterPrefab(string cardId)
        {
            foreach (var registry in Resources.FindObjectsOfTypeAll<FactionRegistrySO>())
            {
                var card = registry.ResolveCard(cardId);
                if (card?.linkedMonster?.prefab != null) return card.linkedMonster.prefab;
            }
            return null;
        }

        private FixedTickRaidSimulationInput BuildLocalDemoInput()
        {
            var heroDefinition = Resources.FindObjectsOfTypeAll<HeroRegistrySO>()
                .SelectMany(registry => registry.Heroes).FirstOrDefault(hero => hero != null);
            var heroId = heroDefinition != null ? heroDefinition.heroId : "hero_test";
            var heroCombat = new RaidWorkerCombatPayload
            {
                maxHealth = heroDefinition != null ? heroDefinition.maxHealth : 30000,
                armor = heroDefinition != null ? heroDefinition.armor : 10,
                attackDamage = heroDefinition != null ? heroDefinition.attackDamage : 1000,
                attackCooldownMs = heroDefinition != null
                    ? Mathf.Max(1, Mathf.RoundToInt(heroDefinition.attackCooldown * 1000f)) : 800,
                attackRangeMilli = heroDefinition != null
                    ? Mathf.RoundToInt(heroDefinition.attackRange * 1000f) : 1800,
                moveSpeedMilli = heroDefinition != null
                    ? Mathf.RoundToInt(heroDefinition.moveSpeed * 1000f) : 9000,
                abilityId = heroDefinition?.tacticalAbility != null
                    ? heroDefinition.tacticalAbility.abilityId : "breach_charge",
                abilityDamage = heroDefinition?.tacticalAbility != null
                    ? heroDefinition.tacticalAbility.damage : 300,
            };
            var heroPower = Mathf.Max(1, heroCombat.maxHealth / 20 + heroCombat.armor * 2 +
                heroCombat.attackDamage * 1000 / Mathf.Max(1, heroCombat.attackCooldownMs) +
                heroCombat.abilityDamage / 5);
            var cardId = "1/1";
            var towerId = "1/1";
            foreach (var registry in Resources.FindObjectsOfTypeAll<FactionRegistrySO>())
            {
                var faction = registry.Factions.FirstOrDefault(value => value != null);
                var card = faction?.cards.FirstOrDefault(value => value?.linkedMonster != null);
                var tower = faction?.towers.FirstOrDefault(value => value != null);
                if (card != null) cardId = registry.IdOf(card);
                if (tower != null) towerId = registry.IdOf(tower);
                if (card != null || tower != null) break;
            }
            const long armyPower = 130;
            const long gearPower = 200;
            return new FixedTickRaidSimulationInput
            {
                raidId = "10000000-0000-0000-0000-000000000101",
                targetSnapshotId = "20000000-0000-0000-0000-000000000101",
                loadoutSnapshotId = "30000000-0000-0000-0000-000000000101",
                armyPower = armyPower,
                heroPower = heroPower,
                gearPower = gearPower,
                attackerPower = armyPower + heroPower + gearPower,
                defenderPower = 405,
                hero = new RaidWorkerHeroAuthority
                {
                    contentId = "hero/" + heroId,
                    level = 1,
                    basePower = heroPower,
                    scaledPower = heroPower,
                    combat = heroCombat,
                },
                gearItems = new List<RaidWorkerGearAuthority>
                {
                    new()
                    {
                        instanceId = "61000000-0000-0000-0000-000000000101",
                        contentId = "gear/demo-blade",
                        level = 1,
                        basePower = gearPower,
                        scaledPower = gearPower,
                        combat = new RaidWorkerCombatPayload(),
                    },
                },
                loadoutEntries = new List<RaidWorkerLoadoutEntry>
                {
                    new() { cardId = cardId, count = 8 },
                },
                targetSnapshot = new RaidWorkerBaseLayout
                {
                    version = 1,
                    factionId = cardId.Split('/')[0],
                    towers = new List<RaidWorkerTower>
                    {
                        Tower(towerId, outerRingPoint, 1),
                        Tower(towerId, innerRingPoint, 2),
                        Tower(towerId, coreRingPoint, 3),
                    },
                    garrison = new List<RaidWorkerGarrison>
                    {
                        new() { cardId = cardId, position = Point(innerRingPoint + Vector3.right * 5f) },
                    },
                },
            };
        }

        private static RaidWorkerTower Tower(string id, Vector3 position, int level) => new()
        {
            towerId = id,
            position = Point(position),
            attackLevel = level,
            healthLevel = level,
        };

        private static RaidWorkerVector3 Point(Vector3 value) =>
            new() { x = value.x, y = value.y, z = value.z };

        private Vector3 TargetPosition(string target) => target switch
        {
            "ring-1" => outerRingPoint,
            "ring-2" => innerRingPoint,
            "ring-3" => coreRingPoint,
            "attacker-entry" => entryPoint,
            "attackers" => attackersRoot != null ? attackersRoot.position : entryPoint,
            _ => attackersRoot != null ? attackersRoot.position : entryPoint,
        };

        private static int RingIndex(string target) => target switch
        {
            "ring-1" => 0,
            "ring-2" => 1,
            "ring-3" => 2,
            _ => 0,
        };

        private static string DisplayTarget(string target) => target switch
        {
            "ring-1" => "Outer Ring",
            "ring-2" => "Inner Ring",
            "ring-3" => "Core Ring",
            _ => target,
        };

        private void ClearVisuals()
        {
            if (visualRoot != null) Destroy(visualRoot.gameObject);
            if (overlayCanvas != null) Destroy(overlayCanvas.gameObject);
            ringBarriers.Clear();
            SpawnedActorCount = 0;
            visualRoot = null;
            attackersRoot = null;
            overlayCanvas = null;
        }

        private static bool IsSha256(string value) => value != null && value.Length == 64 &&
            value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static Color Cyan() => new(.2f, .9f, 1f);
        private static Color Gold() => new(1f, .78f, .22f);
        private static Color Orange() => new(1f, .46f, .15f);
        private static Color Magenta() => new(.9f, .25f, 1f);
        private static Color Red() => new(1f, .22f, .2f);
        private static Color Green() => new(.25f, 1f, .45f);
    }
}
