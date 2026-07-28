using System;
using UnityEngine;

namespace Splice.World
{
    [Serializable]
    public sealed class ForestHuntProgress
    {
        public int schemaVersion = 1;
        public int fragments;
        public int diamonds;
        public int diamondsEarnedThisWeek;
        public int fragmentsPerDiamond = 100;
        public int weeklyDiamondCap = 3;
        public string weekKey;
        public long revision;
    }

    public sealed class ForestHuntSettlement
    {
        public int securedFragments;
        public int convertedDiamonds;
        public bool weeklyCapReached;
        public ForestHuntProgress progress;
    }

    public static class ForestHuntProgressStore
    {
        private const string Key = "Splice.World.ForestProgress.v1";

        public static ForestHuntProgress Load(DateTime? utcNow = null)
        {
            var progress = PlayerPrefs.HasKey(Key)
                ? JsonUtility.FromJson<ForestHuntProgress>(PlayerPrefs.GetString(Key))
                : null;
            progress ??= new ForestHuntProgress();
            var currentWeek = WeekKey(utcNow ?? DateTime.UtcNow);
            if (!string.Equals(progress.weekKey, currentWeek, StringComparison.Ordinal))
            {
                progress.weekKey = currentWeek;
                progress.diamondsEarnedThisWeek = 0;
            }
            progress.fragmentsPerDiamond = Mathf.Max(1, progress.fragmentsPerDiamond);
            progress.weeklyDiamondCap = Mathf.Max(0, progress.weeklyDiamondCap);
            return progress;
        }

        public static ForestHuntSettlement Settle(int carriedFragments, int fragmentsPerDiamond,
            int weeklyDiamondCap, DateTime? utcNow = null)
        {
            var progress = Load(utcNow);
            progress.fragmentsPerDiamond = Mathf.Max(1, fragmentsPerDiamond);
            progress.weeklyDiamondCap = Mathf.Max(0, weeklyDiamondCap);
            var secured = Mathf.Max(0, carriedFragments);
            progress.fragments += secured;
            var convertible = progress.weeklyDiamondCap - progress.diamondsEarnedThisWeek;
            var converted = Mathf.Min(Mathf.Max(0, convertible),
                progress.fragments / progress.fragmentsPerDiamond);
            if (converted > 0)
            {
                progress.fragments -= converted * progress.fragmentsPerDiamond;
                progress.diamonds += converted;
                progress.diamondsEarnedThisWeek += converted;
            }
            progress.revision++;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
            return new ForestHuntSettlement
            {
                securedFragments = secured,
                convertedDiamonds = converted,
                weeklyCapReached = progress.diamondsEarnedThisWeek >= progress.weeklyDiamondCap,
                progress = progress,
            };
        }

        public static void DeleteForTests()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        private static string WeekKey(DateTime utc)
        {
            var date = utc.Date;
            var offset = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-offset).ToString("yyyy-MM-dd");
        }
    }
}
