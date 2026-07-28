#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.Materials
{
    public sealed class UrpMaterialDoctorWindow : EditorWindow
    {
        public const string MenuPath = "Splice/Materials/URP Material Doctor (Selected Folder Only)";
        public const string ConversionWarning =
            "Convert only the audited materials inside this selected folder?\n\n" +
            "The operation changes material shader assignments in place. A raw backup is written under Library/SpliceMaterialBackups first.";

        private string _folderPath = string.Empty;
        private string _message = "Select exactly one subfolder under Assets in the Project window.";
        private List<UrpMaterialAudit> _audits = new List<UrpMaterialAudit>();
        private Vector2 _scroll;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<UrpMaterialDoctorWindow>("URP Material Doctor");
            window.minSize = new Vector2(760f, 480f);
            window.UseCurrentSelection();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("URP MATERIAL DOCTOR", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scope-safe workflow: select one Project subfolder → Dry Run Audit → review mappings → confirm conversion. " +
                "There is intentionally no whole-project conversion command.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Selected folder");
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(_folderPath) ? "<none>" : _folderPath,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Use Project Selection", GUILayout.Width(150f)))
                    UseCurrentSelection();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = UrpMaterialDoctorCore.IsAllowedSelectedFolder(_folderPath, out _);
                if (GUILayout.Button("1. Dry Run / Audit", GUILayout.Height(28f)))
                    Scan();
                GUI.enabled = _audits.Any(a => a.CanConvert);
                if (GUILayout.Button("2. Confirm Convert Safe Items", GUILayout.Height(28f)))
                    ConfirmAndConvert();
                GUI.enabled = _audits.Count > 0;
                if (GUILayout.Button("3. Validate Again", GUILayout.Height(28f)))
                    Scan();
                GUI.enabled = true;
            }

            EditorGUILayout.HelpBox(_message, MessageType.None);
            DrawSummary();
            DrawAuditTable();
        }

        private void UseCurrentSelection()
        {
            string selected = UrpMaterialDoctorCore.GetSelectedFolderPath();
            if (!UrpMaterialDoctorCore.IsAllowedSelectedFolder(selected, out string reason))
            {
                _folderPath = string.Empty;
                _audits.Clear();
                _message = reason;
                Repaint();
                return;
            }

            _folderPath = selected;
            _audits.Clear();
            _message = $"Ready to audit only: {_folderPath}";
            Repaint();
        }

        private void Scan()
        {
            if (!UrpMaterialDoctorCore.IsAllowedSelectedFolder(_folderPath, out string reason))
            {
                _message = reason;
                return;
            }

            _audits = UrpMaterialDoctorCore.AuditFolder(_folderPath);
            int compatible = _audits.Count(a => a.Status == UrpMaterialAuditStatus.Compatible);
            int convertible = _audits.Count(a => a.Status == UrpMaterialAuditStatus.SafeToConvert);
            int manual = _audits.Count(a => a.Status == UrpMaterialAuditStatus.ManualReview);
            _message = $"Dry run complete. {_audits.Count} material(s): {compatible} compatible, " +
                       $"{convertible} safe to convert, {manual} manual review.";
        }

        private void ConfirmAndConvert()
        {
            int count = _audits.Count(a => a.CanConvert);
            string warning = $"{ConversionWarning}\n\nFolder: {_folderPath}\nMaterials: {count}";
            if (!EditorUtility.DisplayDialog("Confirm URP Material Conversion", warning, "Convert", "Cancel"))
                return;

            UrpMaterialConversionResult result = UrpMaterialDoctorCore.ConvertAudits(_folderPath, _audits);
            if (!result.Succeeded)
            {
                _message = "Conversion stopped with error(s):\n" + string.Join("\n", result.Errors);
                EditorUtility.DisplayDialog("URP Material Doctor", _message, "OK");
                return;
            }

            Scan();
            _message = $"Converted {result.ConvertedCount} material(s). Backup: {result.BackupDirectory}";
            EditorUtility.DisplayDialog("URP Material Doctor", _message, "OK");
        }

        private void DrawSummary()
        {
            if (_audits.Count == 0) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Total {_audits.Count}", GUILayout.Width(90f));
                GUILayout.Label(
                    $"Compatible {_audits.Count(a => a.Status == UrpMaterialAuditStatus.Compatible)}",
                    GUILayout.Width(130f));
                GUILayout.Label(
                    $"Safe {_audits.Count(a => a.Status == UrpMaterialAuditStatus.SafeToConvert)}",
                    GUILayout.Width(100f));
                GUILayout.Label($"Manual {_audits.Count(a => a.Status == UrpMaterialAuditStatus.ManualReview)}");
            }
        }

        private void DrawAuditTable()
        {
            if (_audits.Count == 0) return;

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Status", GUILayout.Width(105f));
                GUILayout.Label("Material", GUILayout.Width(230f));
                GUILayout.Label("Source → Target", GUILayout.Width(285f));
                GUILayout.Label("Reason");
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (UrpMaterialAudit audit in _audits)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(StatusLabel(audit.Status), GUILayout.Width(105f));
                    if (GUILayout.Button(
                            System.IO.Path.GetFileNameWithoutExtension(audit.AssetPath),
                            EditorStyles.linkLabel,
                            GUILayout.Width(230f)))
                    {
                        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(audit.AssetPath);
                        EditorGUIUtility.PingObject(Selection.activeObject);
                    }

                    string mapping = audit.CanConvert
                        ? $"{audit.SourceShader} → {audit.TargetShader}"
                        : audit.SourceShader;
                    GUILayout.Label(mapping, GUILayout.Width(285f));
                    GUILayout.Label(audit.Reason, EditorStyles.wordWrappedMiniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static string StatusLabel(UrpMaterialAuditStatus status)
        {
            switch (status)
            {
                case UrpMaterialAuditStatus.Compatible: return "✓ URP";
                case UrpMaterialAuditStatus.SafeToConvert: return "↻ SAFE";
                default: return "⚠ MANUAL";
            }
        }
    }
}
#endif
