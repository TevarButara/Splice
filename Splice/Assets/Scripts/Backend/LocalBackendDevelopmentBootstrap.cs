using System;
using Splice.Core;
using UnityEngine;

namespace Splice.Backend
{
    // Opt-in only. Normal Editor/Player runs remain local until a developer explicitly enables this profile.
    // The dev bearer is forbidden by the ASP.NET API outside Development and must never be used in production.
    public static class LocalBackendDevelopmentBootstrap
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:5080";
        private const string EnabledKey = "Splice.Backend.LocalDevelopment.Enabled";
        private const string BaseUrlKey = "Splice.Backend.LocalDevelopment.BaseUrl";
        private const string PlayerIdKey = "Splice.Backend.LocalDevelopment.PlayerId";

        public static bool Enabled => PlayerPrefs.GetInt(EnabledKey, 0) == 1;
        public static string BaseUrl => PlayerPrefs.GetString(BaseUrlKey, DefaultBaseUrl);
        public static string PlayerId => PlayerPrefs.GetString(PlayerIdKey, string.Empty);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeSceneLoad()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Enabled) return;
            if (!Guid.TryParse(PlayerId, out var playerId))
            {
                Debug.LogError("[Backend] Local remote mode is enabled but Player ID is not a UUID.");
                return;
            }

            try
            {
                var token = new StaticBackendAccessTokenProvider("dev:" + playerId.ToString("D"));
                SpliceServiceHub.ConfigureRemoteMeta(
                    new UnityWebRequestBackendTransport(BaseUrl, token));
                Debug.Log($"[Backend] Remote meta enabled for local Development at {BaseUrl}.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Backend] Local remote configuration failed: " + exception.Message);
            }
#endif
        }

        public static void Enable(string baseUrl, string playerId)
        {
            UnityWebRequestBackendTransport.ValidateBaseUri(baseUrl);
            if (!Guid.TryParse(playerId, out var parsed))
                throw new ArgumentException("Local backend player ID must be a UUID.", nameof(playerId));
            PlayerPrefs.SetString(BaseUrlKey, baseUrl.TrimEnd('/'));
            PlayerPrefs.SetString(PlayerIdKey, parsed.ToString("D"));
            PlayerPrefs.SetInt(EnabledKey, 1);
            PlayerPrefs.Save();
        }

        public static void EnableForCurrentPlayer(string baseUrl = DefaultBaseUrl) =>
            Enable(baseUrl, PlayerProfile.AccountId);

        public static void Disable()
        {
            PlayerPrefs.SetInt(EnabledKey, 0);
            PlayerPrefs.Save();
            SpliceServiceHub.ResetToLocalDefaults();
        }
    }
}
