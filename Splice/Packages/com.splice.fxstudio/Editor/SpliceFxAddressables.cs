using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Splice.FxStudio.Editor
{
    internal static class SpliceFxAddressables
    {
        public static bool Register(Object asset, string address)
        {
            if (asset == null || string.IsNullOrWhiteSpace(address))
                return false;

            var path = AssetDatabase.GetAssetPath(asset);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (string.IsNullOrWhiteSpace(guid) || settings == null ||
                settings.DefaultGroup == null)
            {
                Debug.LogWarning(
                    $"Splice FX Studio could not register '{path}' as Addressable. " +
                    "Create Addressables settings, then export again.",
                    asset);
                return false;
            }

            var entry = settings.CreateOrMoveEntry(
                guid,
                settings.DefaultGroup,
                false,
                false);
            entry.address = address;
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified,
                entry,
                true,
                true);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
