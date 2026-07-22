using System;
using Splice.Core;
using UnityEngine;

namespace Splice.Base
{
    // Stable local identity for the selected attacker loadout slot. C2 currently pins this UUID in a quote;
    // a later loadout API can retain the same ID while moving the actual army contents to server authority.
    public static class AttackerLoadoutIdentity
    {
        private const string KeyPrefix = "Splice.AttackerLoadout.Id.";

        public static string ForFaction(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
                throw new ArgumentException("Attacker faction is required.", nameof(factionId));
            var key = KeyPrefix + PlayerProfile.AccountId + "." + factionId.Trim();
            var value = PlayerPrefs.GetString(key, string.Empty);
            if (Guid.TryParse(value, out var existing)) return existing.ToString("D");
            var created = Guid.NewGuid().ToString("D");
            PlayerPrefs.SetString(key, created);
            PlayerPrefs.Save();
            return created;
        }
    }
}
