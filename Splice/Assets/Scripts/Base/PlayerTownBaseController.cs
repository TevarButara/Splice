using System.Collections;
using Splice.Core;
using Splice.Data;
using Splice.Input;
using UnityEngine;

namespace Splice.Base
{
    /// <summary>
    /// Resolves the player's faction/base level, guarantees one matching town base at the exact BasePoint,
    /// and focuses the BuildZone camera whenever the player returns to the Town tab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTownBaseController : MonoBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [SerializeField] private Transform basePoint;
        [SerializeField] private CameraPanController cameraPan;

        private GameObject spawnedBase;
        private BaseLevelDefinition resolvedLevel;
        private string resolvedFactionId = string.Empty;

        public Transform BasePoint => basePoint;
        public GameObject SpawnedBase => spawnedBase;
        public BaseLevelDefinition ResolvedLevel => resolvedLevel;
        public bool HasRequiredReferences => registry != null && basePoint != null && cameraPan != null;

        private void Awake() => EnsureBase();

        private IEnumerator Start()
        {
            // Canvas/camera systems finish their own Start first; then establish the definitive Town home view.
            yield return null;
            FocusTownBase();
        }

        public void ConfigureEditorReferences(FactionRegistrySO valueRegistry, Transform valueBasePoint,
            CameraPanController valueCameraPan)
        {
            registry = valueRegistry;
            basePoint = valueBasePoint;
            cameraPan = valueCameraPan;
        }

        public bool EnsureBase()
        {
            if (!HasRequiredReferences)
            {
                Debug.LogError("[TownBase] BuildZone requires registry, BasePoint and CameraPanController.", this);
                return false;
            }

            var factionId = EnsureActiveFaction();
            var faction = registry.GetFaction(factionId);
            resolvedLevel = faction?.townBase?.ResolveLevel(PlayerProfile.BaseLevel(factionId));
            if (resolvedLevel?.prefab == null)
            {
                Debug.LogError($"[TownBase] Faction '{factionId}' has no base prefab for its current level.", this);
                return false;
            }

            if (spawnedBase != null && resolvedFactionId == factionId &&
                spawnedBase.name.StartsWith(resolvedLevel.prefab.name))
            {
                PlaceAtBasePoint(spawnedBase);
                return true;
            }

            if (spawnedBase != null) spawnedBase.SetActive(false);
            DisableOtherAuthoredBasePreviews(resolvedLevel.prefab.name);
            spawnedBase = FindReusableSceneBase(resolvedLevel.prefab.name);
            if (spawnedBase == null)
            {
                spawnedBase = Instantiate(resolvedLevel.prefab, basePoint.position, basePoint.rotation, basePoint);
                spawnedBase.name = resolvedLevel.prefab.name;
            }
            PlaceAtBasePoint(spawnedBase);
            resolvedFactionId = factionId;
            return true;
        }

        public void FocusTownBase()
        {
            if (!EnsureBase() || cameraPan == null) return;
            cameraPan.CenterOnWorldPoint(basePoint.position, true);
        }

        private string EnsureActiveFaction()
        {
            if (PlayerProfile.HasActiveFaction && registry.GetFaction(PlayerProfile.ActiveFactionId) != null)
                return PlayerProfile.ActiveFactionId;
            foreach (var faction in registry.Factions)
            {
                if (faction == null || string.IsNullOrWhiteSpace(faction.factionId)) continue;
                PlayerProfile.UnlockFaction(faction.factionId);
                PlayerProfile.ActiveFactionId = faction.factionId;
                return faction.factionId;
            }
            return string.Empty;
        }

        private void DisableOtherAuthoredBasePreviews(string selectedPrefabName)
        {
            foreach (var faction in registry.Factions)
            {
                if (faction?.townBase == null) continue;
                foreach (var level in faction.townBase.levels)
                {
                    if (level?.prefab == null || level.prefab.name == selectedPrefabName) continue;
                    var candidate = FindReusableSceneBase(level.prefab.name);
                    if (candidate != null) candidate.SetActive(false);
                }
            }
        }

        private GameObject FindReusableSceneBase(string prefabName)
        {
            var scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
                if (root != gameObject && root.name == prefabName) return root;
            if (basePoint != null)
            {
                for (var index = 0; index < basePoint.childCount; index++)
                {
                    var child = basePoint.GetChild(index);
                    if (child.name == prefabName) return child.gameObject;
                }
            }
            return null;
        }

        private void PlaceAtBasePoint(GameObject value)
        {
            value.transform.SetParent(basePoint, false);
            value.transform.localPosition = Vector3.zero;
            value.transform.localRotation = Quaternion.identity;
            value.SetActive(true);
        }
    }
}
