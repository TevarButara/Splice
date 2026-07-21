using System.Collections.Generic;
using UnityEngine;

namespace Splice.Core
{
    // Local prototype collection/selection contract. Replace storage with the profile backend later while
    // preserving these intent-level operations.
    public static class PlayerHeroProfile
    {
        private const string SelectedKey = "Splice.Hero.Selected";
        private const string OwnedKey = "Splice.Hero.Owned";

        [System.Serializable]
        private class HeroIdList { public List<string> ids = new(); }

        public static string SelectedHeroId
        {
            get => PlayerPrefs.GetString(SelectedKey, string.Empty);
            set { PlayerPrefs.SetString(SelectedKey, value ?? string.Empty); PlayerPrefs.Save(); }
        }

        public static IReadOnlyList<string> OwnedHeroIds => LoadOwned().ids;
        public static bool Owns(string heroId) => !string.IsNullOrEmpty(heroId) && LoadOwned().ids.Contains(heroId);

        public static bool Unlock(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId)) return false;
            var owned = LoadOwned();
            if (owned.ids.Contains(heroId)) return false;
            owned.ids.Add(heroId);
            SaveOwned(owned);
            if (string.IsNullOrEmpty(SelectedHeroId)) SelectedHeroId = heroId;
            return true;
        }

        private static HeroIdList LoadOwned()
        {
            if (!PlayerPrefs.HasKey(OwnedKey)) return new HeroIdList();
            return JsonUtility.FromJson<HeroIdList>(PlayerPrefs.GetString(OwnedKey)) ?? new HeroIdList();
        }

        private static void SaveOwned(HeroIdList list)
        {
            PlayerPrefs.SetString(OwnedKey, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }
    }
}
