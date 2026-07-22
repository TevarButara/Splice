using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Splice.ContentUpdates
{
    [Serializable]
    public sealed class LiveContentPackManifest
    {
        public string label;
        public bool mandatory;
        public long expectedBytes;
    }

    [Serializable]
    public sealed class LiveContentManifest
    {
        public int schemaVersion = 1;
        public string contentVersion = LiveContentRuntime.EmbeddedContentVersion;
        public string minimumClientVersion = "0.0.0";
        public string serverRulesVersion = "content-c3-v1";
        public string catalogUrl;
        public string catalogSha256;
        public string signature;
        public string rollbackCatalogUrl;
        public string validationAddress = "livecontent/probe";
        public LiveContentPackManifest[] packs = Array.Empty<LiveContentPackManifest>();

        public IEnumerable<string> MandatoryLabels()
        {
            if (packs == null) yield break;
            foreach (var pack in packs)
                if (pack != null && pack.mandatory && !string.IsNullOrWhiteSpace(pack.label))
                    yield return pack.label.Trim();
        }
    }

    public static class LiveContentRuntime
    {
        public const string EmbeddedContentVersion = "1.0.0";
        public const int SupportedManifestSchema = 1;
        public static string ActiveContentVersion { get; internal set; } = EmbeddedContentVersion;
    }

    public readonly struct LiveContentManifestValidation
    {
        public bool IsValid { get; }
        public bool RequiresStoreUpdate { get; }
        public string Error { get; }

        public LiveContentManifestValidation(bool isValid, bool requiresStoreUpdate, string error)
        {
            IsValid = isValid;
            RequiresStoreUpdate = requiresStoreUpdate;
            Error = error ?? string.Empty;
        }
    }

    public static class LiveContentManifestValidator
    {
        public static LiveContentManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Live-content manifest is empty.");
            var manifest = JsonUtility.FromJson<LiveContentManifest>(json);
            if (manifest == null) throw new FormatException("Live-content manifest JSON is invalid.");
            manifest.packs ??= Array.Empty<LiveContentPackManifest>();
            return manifest;
        }

        public static LiveContentManifestValidation Validate(LiveContentManifest manifest,
            string clientVersion, bool requireProductionSignature)
        {
            if (manifest == null) return Invalid("Manifest is missing.");
            if (manifest.schemaVersion != LiveContentRuntime.SupportedManifestSchema)
                return Invalid($"Manifest schema {manifest.schemaVersion} is unsupported.");
            if (!SemanticVersion.TryParse(manifest.contentVersion, out _))
                return Invalid("contentVersion must use major.minor.patch.");
            if (!SemanticVersion.TryParse(manifest.minimumClientVersion, out _))
                return Invalid("minimumClientVersion must use major.minor.patch.");
            if (!SemanticVersion.TryParse(clientVersion, out _))
                return Invalid("The client version must use major.minor.patch.");

            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pack in manifest.packs)
            {
                if (pack == null || string.IsNullOrWhiteSpace(pack.label))
                    return Invalid("Every content pack must have a label.");
                if (!labels.Add(pack.label.Trim())) return Invalid($"Duplicate content-pack label: {pack.label}");
                if (pack.expectedBytes < 0) return Invalid($"Pack {pack.label} has a negative size.");
            }

            if (!string.IsNullOrWhiteSpace(manifest.catalogSha256) && !IsSha256(manifest.catalogSha256))
                return Invalid("catalogSha256 must contain 64 hexadecimal characters.");
            if (requireProductionSignature)
            {
                if (string.IsNullOrWhiteSpace(manifest.catalogUrl) || !IsSha256(manifest.catalogSha256))
                    return Invalid("Production manifests require a catalog URL and SHA-256.");
                if (string.IsNullOrWhiteSpace(manifest.signature))
                    return Invalid("Production manifests require a detached signature.");
            }

            var storeUpdate = SemanticVersion.Compare(clientVersion, manifest.minimumClientVersion) < 0;
            return new LiveContentManifestValidation(true, storeUpdate, string.Empty);
        }

        public static bool CatalogMatches(byte[] catalogBytes, string expectedSha256)
        {
            if (catalogBytes == null || !IsSha256(expectedSha256)) return false;
            using var sha = SHA256.Create();
            var actual = Hex(sha.ComputeHash(catalogBytes));
            return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static string Sha256(string value)
        {
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            foreach (var c in value)
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        private static string Hex(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        private static LiveContentManifestValidation Invalid(string error) => new(false, false, error);
    }

    public readonly struct SemanticVersion
    {
        private readonly int major;
        private readonly int minor;
        private readonly int patch;

        private SemanticVersion(int major, int minor, int patch)
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
        }

        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var core = value.Trim().Split('-', '+')[0].Split('.');
            if (core.Length != 3 || !int.TryParse(core[0], out var major) ||
                !int.TryParse(core[1], out var minor) || !int.TryParse(core[2], out var patch) ||
                major < 0 || minor < 0 || patch < 0) return false;
            version = new SemanticVersion(major, minor, patch);
            return true;
        }

        public static int Compare(string left, string right)
        {
            if (!TryParse(left, out var a) || !TryParse(right, out var b))
                throw new FormatException("Versions must use major.minor.patch.");
            var result = a.major.CompareTo(b.major);
            if (result != 0) return result;
            result = a.minor.CompareTo(b.minor);
            return result != 0 ? result : a.patch.CompareTo(b.patch);
        }
    }
}
