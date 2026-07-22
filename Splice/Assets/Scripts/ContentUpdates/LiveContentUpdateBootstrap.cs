using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Splice.ContentUpdates
{
    [DefaultExecutionOrder(-10000)]
    public sealed class LiveContentUpdateBootstrap : MonoBehaviour
    {
        private const string LocalManifestUrl = "http://127.0.0.1:8081/live-content-manifest.json";
        private CancellationTokenSource lifetime;
        private LiveContentProgress progress;
        private bool showOverlay;

        public LiveContentUpdateResult LastResult { get; private set; }
        public LiveContentProgress CurrentProgress => progress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<LiveContentUpdateBootstrap>() != null) return;
            var host = new GameObject("LiveContentUpdateBootstrap");
            DontDestroyOnLoad(host);
            host.AddComponent<LiveContentUpdateBootstrap>();
        }

        private async void Awake()
        {
            if (FindObjectsByType<LiveContentUpdateBootstrap>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            lifetime = new CancellationTokenSource();
            try
            {
                await RunAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal during editor stop/application shutdown.
            }
            catch (Exception exception)
            {
                progress = new LiveContentProgress(LiveContentUpdateState.Failed, 0f, 0, 0,
                    exception.Message);
                showOverlay = true;
                Debug.LogError($"[LiveContent] {exception}");
            }
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
        }

        public async Task<LiveContentUpdateResult> RunAsync(CancellationToken cancellationToken)
        {
            var store = new PlayerPrefsLiveContentVersionStore();
            var coordinator = new LiveContentUpdateCoordinator(
                new AddressablesLiveContentOperations(), store);
            coordinator.ProgressChanged += OnProgress;
            try
            {
                await coordinator.RecoverInterruptedActivationAsync(cancellationToken);
                var manifest = await LoadManifestAsync(cancellationToken);
                var requireSignature = !Debug.isDebugBuild && !Application.isEditor;
                LastResult = await coordinator.RunAsync(manifest, NormalizeClientVersion(Application.version),
                    requireSignature, cancellationToken);
                return LastResult;
            }
            finally
            {
                coordinator.ProgressChanged -= OnProgress;
            }
        }

        private void OnProgress(LiveContentProgress value)
        {
            progress = value;
            showOverlay = value.State is LiveContentUpdateState.Downloading or
                LiveContentUpdateState.Validating or LiveContentUpdateState.Activating or
                LiveContentUpdateState.RollingBack or LiveContentUpdateState.Failed or
                LiveContentUpdateState.StoreUpdateRequired;
        }

        private static async Task<LiveContentManifest> LoadManifestAsync(
            CancellationToken cancellationToken)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                var remote = await GetTextAsync(LocalManifestUrl, 2, cancellationToken);
                return LiveContentManifestValidator.Parse(remote);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Debug.Log($"[LiveContent] Local server unavailable; using embedded manifest. {exception.Message}");
            }
#endif
            var embeddedPath = Path.Combine(Application.streamingAssetsPath,
                "LiveContent/live-content-manifest.json");
            var embedded = await GetTextAsync(embeddedPath, 5, cancellationToken);
            return LiveContentManifestValidator.Parse(embedded);
        }

        private static async Task<string> GetTextAsync(string urlOrPath, int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            var url = urlOrPath.Contains("://", StringComparison.Ordinal)
                ? urlOrPath
                : new Uri(urlOrPath).AbsoluteUri;
            using var request = UnityWebRequest.Get(url);
            request.timeout = timeoutSeconds;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException(request.error);
            return request.downloadHandler.text;
        }

        private static string NormalizeClientVersion(string value)
        {
            if (SemanticVersion.TryParse(value, out _)) return value;
            return "0.0.0";
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            var width = Mathf.Min(620f, Screen.width - 40f);
            var height = 150f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f,
                width, height);
            GUI.Box(rect, GUIContent.none);
            var title = progress.State == LiveContentUpdateState.StoreUpdateRequired
                ? "UPDATE REQUIRED"
                : progress.State == LiveContentUpdateState.Failed ? "CONTENT UPDATE FAILED" : "UPDATING CONTENT";
            GUI.Label(new Rect(rect.x + 24f, rect.y + 18f, rect.width - 48f, 28f), title);
            GUI.Label(new Rect(rect.x + 24f, rect.y + 52f, rect.width - 48f, 42f), progress.Message);
            if (progress.State == LiveContentUpdateState.Downloading)
            {
                GUI.HorizontalSlider(new Rect(rect.x + 24f, rect.y + 108f, rect.width - 48f, 18f),
                    progress.Normalized, 0f, 1f);
                GUI.Label(new Rect(rect.x + 24f, rect.y + 126f, rect.width - 48f, 20f),
                    $"{progress.DownloadedBytes / 1048576f:0.0} / {progress.TotalBytes / 1048576f:0.0} MB");
            }
        }
    }
}
