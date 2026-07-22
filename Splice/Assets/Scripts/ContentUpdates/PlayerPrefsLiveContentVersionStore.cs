using UnityEngine;

namespace Splice.ContentUpdates
{
    public sealed class PlayerPrefsLiveContentVersionStore : ILiveContentVersionStore
    {
        private const string ActiveVersionKey = "Splice.LiveContent.ActiveVersion.v1";
        private const string ActiveCatalogKey = "Splice.LiveContent.ActiveCatalog.v1";
        private const string PendingVersionKey = "Splice.LiveContent.PendingVersion.v1";
        private const string PendingCatalogKey = "Splice.LiveContent.PendingCatalog.v1";
        private const string PreviousCatalogKey = "Splice.LiveContent.PreviousCatalog.v1";

        public string ActiveVersion => PlayerPrefs.GetString(
            ActiveVersionKey, LiveContentRuntime.EmbeddedContentVersion);
        public string ActiveCatalogUrl => PlayerPrefs.GetString(ActiveCatalogKey, string.Empty);
        public string PendingVersion => PlayerPrefs.GetString(PendingVersionKey, string.Empty);
        public string PreviousCatalogUrl => PlayerPrefs.GetString(PreviousCatalogKey, string.Empty);

        public void BeginActivation(string version, string catalogUrl)
        {
            PlayerPrefs.SetString(PreviousCatalogKey, ActiveCatalogUrl);
            PlayerPrefs.SetString(PendingVersionKey, version ?? string.Empty);
            PlayerPrefs.SetString(PendingCatalogKey, catalogUrl ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void CommitActivation()
        {
            var pendingVersion = PendingVersion;
            if (string.IsNullOrWhiteSpace(pendingVersion)) return;
            PlayerPrefs.SetString(ActiveVersionKey, pendingVersion);
            PlayerPrefs.SetString(ActiveCatalogKey, PlayerPrefs.GetString(PendingCatalogKey, string.Empty));
            PlayerPrefs.DeleteKey(PendingVersionKey);
            PlayerPrefs.DeleteKey(PendingCatalogKey);
            PlayerPrefs.DeleteKey(PreviousCatalogKey);
            PlayerPrefs.Save();
        }

        public void AbortActivation()
        {
            PlayerPrefs.DeleteKey(PendingVersionKey);
            PlayerPrefs.DeleteKey(PendingCatalogKey);
            PlayerPrefs.DeleteKey(PreviousCatalogKey);
            PlayerPrefs.Save();
        }
    }
}
