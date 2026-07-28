#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using Splice.Editor.Materials;
using UnityEditor;
using UnityEngine;

namespace Splice.Tests.EditMode
{
    public sealed class UrpMaterialDoctorEditModeTests
    {
        private const string TemporaryFolder = "Assets/__UrpMaterialDoctorTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TemporaryFolder);
            AssetDatabase.CreateFolder("Assets", "__UrpMaterialDoctorTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TemporaryFolder);
        }

        [Test]
        public void ScopeGuard_BlocksAssetsRootAndAllowsOnlyChildFolder()
        {
            Assert.That(UrpMaterialDoctorCore.IsAllowedSelectedFolder("Assets", out _), Is.False);
            Assert.That(UrpMaterialDoctorCore.IsAllowedSelectedFolder(TemporaryFolder, out _), Is.True);
            Assert.That(UrpMaterialDoctorCore.IsAllowedSelectedFolder("Packages", out _), Is.False);
            Assert.That(UrpMaterialDoctorCore.IsAllowedSelectedFolder("Assets/NotARealFolder", out _), Is.False);
        }

        [Test]
        public void AuditFolder_DoesNotIncludeMaterialOutsideSelectedFolder()
        {
            CreateMaterial(TemporaryFolder + "/Inside.mat", "Standard");
            CreateMaterial("Assets/OutsideUrpDoctorTest.mat", "Standard");
            try
            {
                var audits = UrpMaterialDoctorCore.AuditFolder(TemporaryFolder);
                Assert.That(audits.Count, Is.EqualTo(1));
                Assert.That(audits[0].AssetPath, Is.EqualTo(TemporaryFolder + "/Inside.mat"));
            }
            finally
            {
                AssetDatabase.DeleteAsset("Assets/OutsideUrpDoctorTest.mat");
            }
        }

        [Test]
        public void StandardMaterial_IsSafeAndPreservesBaseTextureColorAndUv()
        {
            Shader standard = Shader.Find("Standard");
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(standard, Is.Not.Null);
            Assert.That(urpLit, Is.Not.Null);

            var material = new Material(standard);
            var texture = new Texture2D(2, 2);
            try
            {
                var expectedColor = new Color(.2f, .4f, .7f, .8f);
                var expectedScale = new Vector2(2f, 3f);
                var expectedOffset = new Vector2(.25f, .5f);
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", expectedScale);
                material.SetTextureOffset("_MainTex", expectedOffset);
                material.SetColor("_Color", expectedColor);

                var audit = UrpMaterialDoctorCore.AuditMaterial(material);
                Assert.That(audit.Status, Is.EqualTo(UrpMaterialAuditStatus.SafeToConvert));

                UrpMaterialDoctorCore.ConvertMaterialInPlace(material, urpLit, standard.name);
                Assert.That(material.shader, Is.EqualTo(urpLit));
                Assert.That(material.GetTexture("_BaseMap"), Is.EqualTo(texture));
                Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(expectedScale));
                Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(expectedOffset));
                Color actualColor = material.GetColor("_BaseColor");
                Assert.That(actualColor.r, Is.EqualTo(expectedColor.r).Within(.0001f));
                Assert.That(actualColor.g, Is.EqualTo(expectedColor.g).Within(.0001f));
                Assert.That(actualColor.b, Is.EqualTo(expectedColor.b).Within(.0001f));
                Assert.That(actualColor.a, Is.EqualTo(expectedColor.a).Within(.0001f));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void KnownLegacyVfxShader_MapsToPropertyCompatibleUrpReplacement()
        {
            Shader legacy = Shader.Find("VFX/UniversalShader");
            Shader replacement = Shader.Find(UrpMaterialDoctorCore.UrpUniversalVfxShader);
            Assert.That(legacy, Is.Not.Null);
            Assert.That(replacement, Is.Not.Null);

            var material = new Material(legacy);
            try
            {
                material.SetFloat("_DissolveAmount", .42f);
                material.EnableKeyword("ENABLE_DISSOLVE");
                var audit = UrpMaterialDoctorCore.AuditMaterial(material);

                Assert.That(audit.Status, Is.EqualTo(UrpMaterialAuditStatus.SafeToConvert));
                UrpMaterialDoctorCore.ConvertMaterialInPlace(material, replacement, legacy.name);
                Assert.That(material.GetFloat("_DissolveAmount"), Is.EqualTo(.42f).Within(.0001f));
                Assert.That(material.IsKeywordEnabled("ENABLE_DISSOLVE"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ConvertAudits_BlocksForgedOutOfScopeAsset()
        {
            var forged = new UrpMaterialAudit
            {
                AssetPath = "Assets/Outside.mat",
                SourceShader = "Standard",
                TargetShader = "Universal Render Pipeline/Lit",
                Status = UrpMaterialAuditStatus.SafeToConvert
            };

            UrpMaterialConversionResult result =
                UrpMaterialDoctorCore.ConvertAudits(TemporaryFolder, new[] { forged });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors[0], Does.Contain("Out-of-scope"));
        }

        [Test]
        public void ConvertAudits_ChangesOnlySelectedFolderAndCreatesRawBackup()
        {
            string insidePath = TemporaryFolder + "/Inside.mat";
            string outsidePath = "Assets/OutsideUrpDoctorTest.mat";
            string backupDirectory = string.Empty;
            CreateMaterial(insidePath, "Standard");
            CreateMaterial(outsidePath, "Standard");
            try
            {
                var audits = UrpMaterialDoctorCore.AuditFolder(TemporaryFolder);
                UrpMaterialConversionResult result =
                    UrpMaterialDoctorCore.ConvertAudits(TemporaryFolder, audits);
                backupDirectory = result.BackupDirectory;

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.ConvertedCount, Is.EqualTo(1));
                Assert.That(File.Exists(Path.Combine(result.BackupDirectory, insidePath)), Is.True);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Material>(insidePath).shader.name,
                    Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Material>(outsidePath).shader.name,
                    Is.EqualTo("Standard"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(outsidePath);
                if (!string.IsNullOrEmpty(backupDirectory) && Directory.Exists(backupDirectory))
                    Directory.Delete(backupDirectory, true);
            }
        }

        [Test]
        public void WindowExposesNoWholeProjectConversionCommandAndWarnsBeforeMutation()
        {
            Assert.That(UrpMaterialDoctorWindow.MenuPath, Does.Contain("Selected Folder Only"));
            Assert.That(UrpMaterialDoctorWindow.ConversionWarning, Does.Contain("backup"));
            Assert.That(UrpMaterialDoctorWindow.ConversionWarning, Does.Contain("selected folder"));
        }

        private static void CreateMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null);
            AssetDatabase.CreateAsset(new Material(shader), path);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
