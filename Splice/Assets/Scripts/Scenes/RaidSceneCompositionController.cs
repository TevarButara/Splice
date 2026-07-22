using System.Collections;
using Splice.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Scenes
{
    // Composes one authoritative RaidArena with exactly one local presentation scene. Presentation scenes
    // are intentionally loaded with Unity SceneManager rather than Netcode: camera/HUD choice is local.
    public sealed class RaidSceneCompositionController : MonoBehaviour
    {
        public const string ArenaScenePath = "Assets/=======SCENES/RaidArena.unity";
        public const string AttackerScenePath = "Assets/=======SCENES/RaidAttackerPresentation.unity";
        public const string DefenderScenePath = "Assets/=======SCENES/RaidDefenderPresentation.unity";

        public static RaidSceneCompositionController Instance { get; private set; }

        [SerializeField] private bool autoLoadPresentation = true;
        [SerializeField] private RaidPresentationMode defaultMode = RaidPresentationMode.Defender;
        [SerializeField] private Camera fallbackCamera;

        private RaidPresentationMode requestedMode;
        private RaidPresentationRoot activeRoot;
        private Coroutine transition;

        public RaidPresentationMode RequestedMode => requestedMode;
        public RaidPresentationMode ActiveMode => activeRoot != null ? activeRoot.Mode : requestedMode;
        public Camera ActiveCamera => activeRoot != null ? activeRoot.ViewCamera : fallbackCamera;
        public bool IsReady => activeRoot != null && activeRoot.ViewCamera != null && activeRoot.ViewCamera.isActiveAndEnabled;
        public bool IsModeReady(RaidPresentationMode mode) => IsReady && activeRoot.Mode == mode;

        private void Awake()
        {
            Instance = this;
            requestedMode = defaultMode;
        }

        private void Start()
        {
            if (autoLoadPresentation) RequestMode(defaultMode);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(RaidPresentationMode mode, Camera fallback)
        {
            defaultMode = mode;
            requestedMode = mode;
            fallbackCamera = fallback;
        }

        public void RequestMode(RaidPresentationMode mode)
        {
            requestedMode = mode;
            if (IsModeReady(mode))
            {
                ApplyArenaRole(mode);
                return;
            }
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(LoadPresentation(mode));
        }

        private IEnumerator LoadPresentation(RaidPresentationMode mode)
        {
            var sceneName = SceneName(mode);
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    Debug.LogError($"[RaidScenes] Could not start loading presentation scene '{sceneName}'.", this);
                    transition = null;
                    yield break;
                }
                while (!operation.isDone) yield return null;
                scene = SceneManager.GetSceneByName(sceneName);
            }

            var root = FindPresentationRoot(scene, mode);
            if (root == null || root.ViewCamera == null)
            {
                Debug.LogError($"[RaidScenes] '{sceneName}' has no valid {mode} RaidPresentationRoot/camera.", this);
                transition = null;
                yield break;
            }

            if (activeRoot != null && activeRoot != root)
            {
                var previousScene = activeRoot.gameObject.scene;
                activeRoot.SetPresentationActive(false);
                activeRoot = null;
                if (previousScene.isLoaded)
                {
                    var unload = SceneManager.UnloadSceneAsync(previousScene);
                    if (unload != null) while (!unload.isDone) yield return null;
                }
            }

            activeRoot = root;
            DisableOtherCameras(root.ViewCamera);
            root.SetPresentationActive(true);
            ApplyArenaRole(mode);
            transition = null;
            Debug.Log($"[RaidScenes] RaidArena composed with {mode} presentation ({sceneName}).", this);
        }

        private static RaidPresentationRoot FindPresentationRoot(Scene scene, RaidPresentationMode mode)
        {
            if (!scene.isLoaded) return null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var presentation = roots[i].GetComponentInChildren<RaidPresentationRoot>(true);
                if (presentation != null && presentation.Mode == mode) return presentation;
            }
            return null;
        }

        private void DisableOtherCameras(Camera keep)
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < cameras.Length; i++)
            {
                var on = cameras[i] == keep;
                cameras[i].enabled = on;
                var listener = cameras[i].GetComponent<AudioListener>();
                if (listener != null) listener.enabled = on;
            }
            if (fallbackCamera != null && fallbackCamera != keep) fallbackCamera.gameObject.SetActive(false);
        }

        private static void ApplyArenaRole(RaidPresentationMode mode)
        {
            SetPathActive("UI/UI Mon", mode == RaidPresentationMode.Attacker);
            SetPathActive("UI/UI Fort", mode == RaidPresentationMode.Defender);
            SetPathActive("==INV-Manager/DeployInputController", mode == RaidPresentationMode.Attacker);
            // Deployed async snapshots are immutable during combat. Town placement belongs in TownBuild.
            SetPathActive("TOWER/TowerPlacementInputController", false);
        }

        private static void SetPathActive(string path, bool active)
        {
            var target = FindInactivePath(path);
            if (target != null) target.SetActive(active);
        }

        private static GameObject FindInactivePath(string path)
        {
            var segments = path.Split('/');
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].parent != null || transforms[i].name != segments[0]) continue;
                var current = transforms[i];
                for (var segment = 1; segment < segments.Length && current != null; segment++)
                    current = current.Find(segments[segment]);
                if (current != null) return current.gameObject;
            }
            return null;
        }

        private static string SceneName(RaidPresentationMode mode) =>
            mode == RaidPresentationMode.Attacker ? "RaidAttackerPresentation" : "RaidDefenderPresentation";
    }
}
