using System;
using UnityEngine;

namespace Splice.Lair
{
    // Idle resource generation between raids; PvE save is local (architecture 6).
    // Hatching/crafting odds must stay visible to the player when built on top of this (architecture 5.3).
    public class LairManager : MonoBehaviour
    {
        private const string CurrencyKey = "Lair.Currency";
        private const string LastTickKey = "Lair.LastTickUnixSeconds";

        [SerializeField] private float currencyPerSecond = 1f;

        public int Currency { get; private set; }

        private void Awake()
        {
            Currency = PlayerPrefs.GetInt(CurrencyKey, 0);
            ApplyOfflineProgress();
        }

        private void ApplyOfflineProgress()
        {
            var lastTick = PlayerPrefs.GetString(LastTickKey, string.Empty);
            if (DateTimeOffset.TryParse(lastTick, out var lastTime))
            {
                var elapsedSeconds = (DateTimeOffset.UtcNow - lastTime).TotalSeconds;
                Currency += Mathf.FloorToInt((float)elapsedSeconds * currencyPerSecond);
            }

            Save();
        }

        public void CollectNow(float deltaSeconds)
        {
            Currency += Mathf.FloorToInt(deltaSeconds * currencyPerSecond);
            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetInt(CurrencyKey, Currency);
            PlayerPrefs.SetString(LastTickKey, DateTimeOffset.UtcNow.ToString("o"));
            PlayerPrefs.Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }
    }
}
