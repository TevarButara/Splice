using Splice.Input;
using UnityEngine;

namespace Splice.Scenes
{
    // Local-only presentation scene root. It contains no combat authority or NetworkObject: both attacker
    // and defender views render the same RaidArena simulation.
    public sealed class RaidPresentationRoot : MonoBehaviour
    {
        [SerializeField] private RaidPresentationMode mode;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private GameObject[] localUiRoots;

        public RaidPresentationMode Mode => mode;
        public Camera ViewCamera => viewCamera;

        public void Configure(RaidPresentationMode presentationMode, Camera camera)
        {
            mode = presentationMode;
            viewCamera = camera;
        }

        public void SetPresentationActive(bool active)
        {
            if (viewCamera != null)
            {
                viewCamera.gameObject.SetActive(active);
                viewCamera.enabled = active;
                var listener = viewCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = active;
                var pan = viewCamera.GetComponent<CameraPanController>();
                if (pan != null) pan.SetHeroFollowEnabled(active && mode == RaidPresentationMode.Attacker);
            }
            if (localUiRoots == null) return;
            for (var i = 0; i < localUiRoots.Length; i++)
                if (localUiRoots[i] != null) localUiRoots[i].SetActive(active);
        }
    }
}
