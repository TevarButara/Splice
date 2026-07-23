using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Scenes
{
    // Minimal Bootstrap entry. The product build starts in the Town Command hub; RaidArena is entered
    // only after the player selects a target and reviews the stake contract.
    public sealed class RaidSceneEntryLoader : MonoBehaviour
    {
        [SerializeField] private string hubSceneName = "BuildZone";

        private IEnumerator Start()
        {
            if (SceneManager.GetActiveScene().name == hubSceneName) yield break;
            var operation = SceneManager.LoadSceneAsync(hubSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[PrototypeFlow] Hub scene '{hubSceneName}' is not available in Build Settings.", this);
                yield break;
            }
            while (!operation.isDone) yield return null;
        }
    }
}
