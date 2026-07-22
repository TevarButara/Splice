using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Scenes
{
    // Minimal Bootstrap entry. Runtime systems live in RaidArena; Bootstrap only chooses the destination.
    public sealed class RaidSceneEntryLoader : MonoBehaviour
    {
        [SerializeField] private string arenaSceneName = "RaidArena";

        private IEnumerator Start()
        {
            if (SceneManager.GetActiveScene().name == arenaSceneName) yield break;
            var operation = SceneManager.LoadSceneAsync(arenaSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[RaidScenes] Arena scene '{arenaSceneName}' is not available in Build Settings.", this);
                yield break;
            }
            while (!operation.isDone) yield return null;
        }
    }
}
