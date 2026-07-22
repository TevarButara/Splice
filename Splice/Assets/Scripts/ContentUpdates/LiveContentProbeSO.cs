using UnityEngine;

namespace Splice.ContentUpdates
{
    [CreateAssetMenu(fileName = "LiveContentProbe", menuName = "Splice/Live Content/Probe")]
    public sealed class LiveContentProbeSO : ScriptableObject
    {
        public string contentId = "livecontent/probe";
        public string contentVersion = LiveContentRuntime.EmbeddedContentVersion;
        [TextArea] public string message = "This asset was loaded from an Addressables content pack.";
    }
}
