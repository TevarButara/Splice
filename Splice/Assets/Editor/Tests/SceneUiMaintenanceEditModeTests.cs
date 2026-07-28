#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Splice.Base;
using Splice.Editor.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Splice.Tests.EditMode
{
    public sealed class SceneUiMaintenanceEditModeTests
    {
        private const string SourceScene = "Assets/=======SCENES/BuildZone.unity";
        private const string TemporaryScene = "Assets/__SpliceSceneUiMaintenanceTest.unity";

[Test]
        public void RepairMissingReference_RestoresBindingWithoutChangingDesignerRectTransform()
        {
            AssetDatabase.DeleteAsset(TemporaryScene);
            Assert.That(AssetDatabase.CopyAsset(SourceScene, TemporaryScene), Is.True);
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(TemporaryScene, OpenSceneMode.Additive);
                var checkout = Find<BaseBuildCheckoutController>(scene);
                Assert.That(checkout, Is.Not.Null);
                Assert.That(checkout.HasEditorAuthoredUi, Is.True);

                var rect = checkout.EditorUiRoot.GetComponent<RectTransform>();
                var customPosition = new Vector2(147f, -91f);
                var customSize = new Vector2(731f, 427f);
                var customAnchors = new Vector2(.37f, .64f);
                rect.anchoredPosition = customPosition;
                rect.sizeDelta = customSize;
                rect.anchorMin = customAnchors;
                rect.anchorMax = customAnchors;

                var serialized = new SerializedObject(checkout);
                var header = serialized.FindProperty("headerSkin");
                Assert.That(header.objectReferenceValue, Is.Not.Null);
                header.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(checkout.HasEditorAuthoredUi, Is.False);

                var repaired = SpliceSceneUiMaintenanceEditor.RepairSceneReferences(scene, true);

                Assert.That(repaired, Is.EqualTo(1));
                Assert.That(checkout.HasEditorAuthoredUi, Is.True);
                Assert.That(rect.anchoredPosition, Is.EqualTo(customPosition));
                Assert.That(rect.sizeDelta, Is.EqualTo(customSize));
                Assert.That(rect.anchorMin, Is.EqualTo(customAnchors));
                Assert.That(rect.anchorMax, Is.EqualTo(customAnchors));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(TemporaryScene);
            }
        }

[Test]
        public void RepairCompleteScene_IsNoOpForEveryRectTransform()
        {
            AssetDatabase.DeleteAsset(TemporaryScene);
            Assert.That(AssetDatabase.CopyAsset(SourceScene, TemporaryScene), Is.True);
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(TemporaryScene, OpenSceneMode.Additive);
                var before = CaptureRects(scene);

                var repaired = SpliceSceneUiMaintenanceEditor.RepairSceneReferences(scene, true);
                var after = CaptureRects(scene);

                Assert.That(repaired, Is.Zero);
                Assert.That(after, Is.EqualTo(before),
                    "Safe repair must not reposition, resize, reparent or restyle complete designer UI.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(TemporaryScene);
            }
        }

        [Test]
        public void RebuildCommand_HasExplicitDestructiveWarning()
        {
            Assert.That(SpliceSceneUiMaintenanceEditor.RebuildMenuPath, Does.Contain("Defaults"));
            Assert.That(SpliceSceneUiMaintenanceEditor.RebuildWarningMessage, Does.Contain("ลบ/สร้าง"));
            Assert.That(SpliceSceneUiMaintenanceEditor.RebuildWarningMessage, Does.Contain("อาจสูญหาย"));
            Assert.That(SpliceSceneUiMaintenanceEditor.RebuildWarningMessage, Does.Contain("ยืนยัน"));
        }

        private static List<string> CaptureRects(Scene scene)
        {
            var values = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                var parentPath = rect.parent != null ? PathOf(rect.parent) : string.Empty;
                values.Add($"{PathOf(rect)}|parent={parentPath}|sibling={rect.GetSiblingIndex()}|" +
                           $"anchor={rect.anchorMin}/{rect.anchorMax}|pivot={rect.pivot}|" +
                           $"position={rect.anchoredPosition}|size={rect.sizeDelta}|scale={rect.localScale}");
            }
            values.Sort();
            return values;
        }

        private static string PathOf(Transform transform)
        {
            var names = new List<string>();
            while (transform != null)
            {
                names.Add(transform.name);
                transform = transform.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }
    }
}
#endif