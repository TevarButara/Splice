#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Veridian.RockGenLite.Editor;
using Veridian.RockGenLite.Runtime;

namespace Veridian.RockGenLite.Editor.Tests
{
    public sealed class RockClusterLayoutEditModeTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private const string ExportTestRoot = "Assets/__RockClusterExportTests";

        [TearDown]
        public void TearDown()
        {
            for (int i = _cleanup.Count - 1; i >= 0; i--)
            {
                if (_cleanup[i] != null) Object.DestroyImmediate(_cleanup[i]);
            }
            _cleanup.Clear();
            if (AssetDatabase.IsValidFolder(ExportTestRoot))
            {
                AssetDatabase.DeleteAsset(ExportTestRoot);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void SameSeedProducesIdenticalLayout()
        {
            RockSettings rock = CreateRockSettings();
            RockClusterSettings cluster = CreateCluster();

            List<RockClusterPlacement> first = RockClusterLayoutGenerator.Generate(cluster, rock, out string firstWarning);
            List<RockClusterPlacement> second = RockClusterLayoutGenerator.Generate(cluster, rock, out string secondWarning);

            Assert.That(firstWarning, Is.Null);
            Assert.That(secondWarning, Is.Null);
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].rockSeed, Is.EqualTo(first[i].rockSeed));
                Assert.That(second[i].localPosition, Is.EqualTo(first[i].localPosition));
                Assert.That(second[i].localRotation, Is.EqualTo(first[i].localRotation));
                Assert.That(second[i].localScale, Is.EqualTo(first[i].localScale));
            }
        }

        [Test]
        public void EveryRockGetsDifferentDerivedSeed()
        {
            RockSettings rock = CreateRockSettings();
            RockClusterSettings cluster = CreateCluster();
            cluster.count = 32;

            List<RockClusterPlacement> placements = RockClusterLayoutGenerator.Generate(cluster, rock, out string warning);
            var seeds = new HashSet<int>();
            for (int i = 0; i < placements.Count; i++) seeds.Add(placements[i].rockSeed);

            Assert.That(warning, Is.Null);
            Assert.That(seeds.Count, Is.EqualTo(placements.Count));
        }

        [Test]
        public void DiskLayoutStaysInsideConfiguredRadius()
        {
            RockSettings rock = CreateRockSettings();
            RockClusterSettings cluster = CreateCluster();
            cluster.shape = RockClusterShape.Disk;
            cluster.radius = 7f;
            cluster.spread = 0.8f;
            cluster.positionVariance = 0f;
            cluster.count = RockClusterSettings.MaxRockCount;

            List<RockClusterPlacement> placements = RockClusterLayoutGenerator.Generate(cluster, rock, out string warning);

            Assert.That(warning, Is.Null);
            for (int i = 0; i < placements.Count; i++)
            {
                Vector2 planar = new Vector2(placements[i].localPosition.x, placements[i].localPosition.z);
                Assert.That(planar.magnitude, Is.LessThanOrEqualTo(cluster.radius * cluster.spread + 0.0001f));
            }
        }

        [Test]
        public void ScaleVariationAlwaysStaysInsideConfiguredRange()
        {
            RockSettings rock = CreateRockSettings();
            RockClusterSettings cluster = CreateCluster();
            cluster.minimumScale = 0.6f;
            cluster.maximumScale = 1.4f;
            cluster.nonUniformScaleVariance = 0f;
            cluster.count = RockClusterSettings.MaxRockCount;

            List<RockClusterPlacement> placements = RockClusterLayoutGenerator.Generate(cluster, rock, out string warning);

            Assert.That(warning, Is.Null);
            for (int i = 0; i < placements.Count; i++)
            {
                Assert.That(placements[i].localScale.x, Is.InRange(0.6f, 1.4f));
                Assert.That(placements[i].localScale.y, Is.EqualTo(placements[i].localScale.x));
                Assert.That(placements[i].localScale.z, Is.EqualTo(placements[i].localScale.x));
            }
        }

        [Test]
        public void MeshSurfaceSamplingNeedsNoColliderAndPlacesRocksOnMesh()
        {
            RockSettings rock = CreateRockSettings();
            rock.targetDiameter = 2f;
            rock.prefabScale = 1f;

            GameObject surface = new GameObject("Test Surface");
            _cleanup.Add(surface);
            MeshFilter filter = surface.AddComponent<MeshFilter>();
            surface.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-2f, 0f, -3f),
                    new Vector3(-2f, 0f, 3f),
                    new Vector3(2f, 0f, 3f),
                    new Vector3(2f, 0f, -3f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateNormals();
            filter.sharedMesh = mesh;
            _cleanup.Add(mesh);

            RockClusterSettings cluster = CreateCluster();
            cluster.shape = RockClusterShape.MeshSurface;
            cluster.surfaceObject = surface;
            cluster.count = 24;
            cluster.heightVariance = 0f;
            cluster.minimumScale = 1f;
            cluster.maximumScale = 1f;
            cluster.nonUniformScaleVariance = 0f;

            List<RockClusterPlacement> placements = RockClusterLayoutGenerator.Generate(cluster, rock, out string warning);

            Assert.That(warning, Is.Null);
            Assert.That(placements.Count, Is.EqualTo(24));
            for (int i = 0; i < placements.Count; i++)
            {
                Assert.That(placements[i].localPosition.x, Is.InRange(-2f, 2f));
                Assert.That(placements[i].localPosition.z, Is.InRange(-3f, 3f));
                Assert.That(placements[i].localPosition.y, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(placements[i].surfaceNormal, Is.EqualTo(Vector3.up));
            }
        }

        [Test]
        public void MissingSurfaceReturnsWarningInsteadOfPartialLayout()
        {
            RockSettings rock = CreateRockSettings();
            RockClusterSettings cluster = CreateCluster();
            cluster.shape = RockClusterShape.MeshSurface;
            cluster.surfaceObject = null;

            List<RockClusterPlacement> placements = RockClusterLayoutGenerator.Generate(cluster, rock, out string warning);

            Assert.That(placements, Is.Empty);
            Assert.That(warning, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GeneratorDoesNotChangeUnityGlobalRandomState()
        {
            RockSettings rock = CreateRockSettings();
            RockClusterSettings cluster = CreateCluster();

            Random.InitState(8877);
            int expected = Random.Range(int.MinValue, int.MaxValue);
            Random.InitState(8877);

            RockClusterLayoutGenerator.Generate(cluster, rock, out _);
            int actual = Random.Range(int.MinValue, int.MaxValue);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void RockCountIsClampedToPreviewSafetyLimit()
        {
            RockSettings rock = CreateRockSettings();
            RockClusterSettings cluster = CreateCluster();
            cluster.count = 9999;

            List<RockClusterPlacement> placements = RockClusterLayoutGenerator.Generate(cluster, rock, out string warning);

            Assert.That(warning, Is.Null);
            Assert.That(placements.Count, Is.EqualTo(RockClusterSettings.MaxRockCount));
        }

        [Test]
        public void VertexColorClusterExportPersistsPrefabMeshesAndMaterial()
        {
            RockSettings rock = CreateRockSettings();
            rock.colorizationMethod = RockColorizationMethod.VertexColors;
            rock.saveFolderPath = ExportTestRoot;
            rock.exportName = "Regression";

            RockClusterSettings cluster = CreateCluster();
            cluster.count = 1;

            GameObject root = new GameObject("Rock_Cluster_Preview");
            _cleanup.Add(root);
            GameObject rockRoot = new GameObject("Rock_000");
            rockRoot.transform.SetParent(root.transform, false);
            GameObject lodObject = new GameObject("LOD0");
            lodObject.transform.SetParent(rockRoot.transform, false);

            Mesh mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.forward },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateNormals();
            _cleanup.Add(mesh);
            MeshFilter filter = lodObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = lodObject.AddComponent<MeshRenderer>();
            LODGroup lodGroup = rockRoot.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[] { new LOD(0.01f, new Renderer[] { renderer }) });

            RockClusterPrefabFactory.SavePreviewAsPrefab(rock, cluster, root);

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ExportTestRoot });
            Assert.That(prefabGuids.Length, Is.EqualTo(1));
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            MeshFilter savedFilter = prefab.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer savedRenderer = prefab.GetComponentInChildren<MeshRenderer>(true);
            RockClusterGroup savedGroup = prefab.GetComponent<RockClusterGroup>();
            Assert.That(savedFilter, Is.Not.Null);
            Assert.That(savedFilter.sharedMesh, Is.Not.Null);
            Assert.That(EditorUtility.IsPersistent(savedFilter.sharedMesh), Is.True);
            Assert.That(savedRenderer.sharedMaterial, Is.Not.Null);
            Assert.That(EditorUtility.IsPersistent(savedRenderer.sharedMaterial), Is.True);
            Assert.That(savedGroup, Is.Not.Null);
            Assert.That(savedGroup.RockCount, Is.EqualTo(1));
            Assert.That(savedGroup.Seed, Is.EqualTo(cluster.seed));
        }

        private RockSettings CreateRockSettings()
        {
            RockSettings rock = ScriptableObject.CreateInstance<RockSettings>();
            rock.targetDiameter = 2f;
            rock.prefabScale = 1f;
            _cleanup.Add(rock);
            return rock;
        }

        private static RockClusterSettings CreateCluster()
        {
            return new RockClusterSettings
            {
                enabled = true,
                count = 16,
                seed = 24680,
                shape = RockClusterShape.Disk,
                radius = 5f,
                spread = 1f,
                heightVariance = 0f,
                minimumScale = 0.8f,
                maximumScale = 1.2f
            };
        }
    }
}
#endif
