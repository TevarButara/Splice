using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Splice.Backend;
using Splice.Base;
using Splice.Data;
using Splice.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.EditorTests
{
    public sealed class TownRegionExpansionEditModeTests
    {
        private const string Faction = "test-town-regions";

        [TearDown]
        public void TearDown()
        {
            TownExpansionStore.DeleteForTests(Faction);
            PlayerBaseStore.DeleteFaction(Faction);
        }

        [Test]
        public void NewTown_AlwaysOwnsCoreRegion()
        {
            var state = TownExpansionStore.Load(Faction);

            Assert.That(state.mapTemplateId, Is.EqualTo(TownExpansionPrototypeCatalog.MapTemplateId));
            Assert.That(state.mapVersion, Is.EqualTo(TownExpansionPrototypeCatalog.MapVersion));
            Assert.That(state.unlockedRegionIds, Does.Contain(TownExpansionPrototypeCatalog.CoreRegionId));
        }

        [Test]
        public void SnapshotValidator_RejectsDefenseOutsideUnlockedRegions()
        {
            var layout = ValidLayout();
            layout.towers.Add(new PlacedTowerData
            {
                towerId = "1/test-tower",
                position = new Vector3(65f, 0f, 0f),
            });

            var report = TownSnapshotValidator.Validate(layout, 1, 100);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.errors, Has.Some.Contains("outside unlocked town regions"));
        }

        [Test]
        public void SnapshotValidator_AcceptsPurchasedRegionAndRejectsDuplicateClaim()
        {
            var layout = ValidLayout();
            layout.unlockedRegionIds.Add("east");
            layout.towers.Add(new PlacedTowerData
            {
                towerId = "1/test-tower",
                position = new Vector3(45f, 0f, 0f),
            });
            Assert.That(TownSnapshotValidator.Validate(layout, 1, 100).IsValid, Is.True);

            layout.unlockedRegionIds.Add("east");
            var duplicate = TownSnapshotValidator.Validate(layout, 1, 100);
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(duplicate.errors, Has.Some.Contains("duplicated"));
        }

        [Test]
        public void RaidCompatibility_RejectsDifferentMapVersion()
        {
            var snapshot = new TownDefenseSnapshot
            {
                mapTemplateId = TownExpansionPrototypeCatalog.MapTemplateId,
                mapVersion = 2,
                layout = ValidLayout(),
            };

            var compatible = TownSnapshotValidator.IsMapCompatible(snapshot,
                TownExpansionPrototypeCatalog.MapTemplateId,
                TownExpansionPrototypeCatalog.MapVersion, out var error);

            Assert.That(compatible, Is.False);
            Assert.That(error, Does.Contain("map mismatch").IgnoreCase);
        }

        [Test]
        public void TownMapDefinition_UsesFullFootprintInsideUnlockedRegion()
        {
            var definition = ScriptableObject.CreateInstance<TownMapDefinitionSO>();
            try
            {
                typeof(TownMapDefinitionSO).GetField("regions",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(definition,
                    new List<TownRegionDefinition>
                    {
                        new()
                        {
                            regionId = "core",
                            initiallyUnlocked = true,
                            localCenter = Vector2.zero,
                            size = new Vector2(10f, 10f),
                        },
                    });

                Assert.That(definition.ContainsUnlocked(new Vector3(4f, 0f, 0f),
                    Vector3.zero, 2f, new[] { "core" }), Is.True);
                Assert.That(definition.ContainsUnlocked(new Vector3(4.1f, 0f, 0f),
                    Vector3.zero, 2f, new[] { "core" }), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void PurchasePanel_RestoresAuthoritativeButtonStateAfterBusyRequest()
        {
            var root = new GameObject("TownRegionUiTest");
            try
            {
                var controller = root.AddComponent<TownRegionPurchaseController>();
                var unlocked = CreateRegionButton(root.transform, "east");
                var blocked = CreateRegionButton(root.transform, "outer-north");
                Set(controller, "regionButtons",
                    new List<TownRegionPurchaseButtonView> { unlocked, blocked });
                var status = new GameObject("Status", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                    .GetComponent<TextMeshProUGUI>();
                status.transform.SetParent(root.transform);
                Set(controller, "statusText", status);

                var expansion = new TownExpansionView
                {
                    factionId = Faction,
                    mapTemplateId = TownExpansionPrototypeCatalog.MapTemplateId,
                    mapVersion = TownExpansionPrototypeCatalog.MapVersion,
                    unlockedRegionIds = new List<string> { "core", "east" },
                    availableRegions = new List<TownRegionOfferDto>
                    {
                        new()
                        {
                            regionId = "outer-north",
                            displayName = "Outer North",
                            goldCost = 1200,
                            prerequisiteRegionIds = new List<string> { "north" },
                        },
                    },
                };

                typeof(TownRegionPurchaseController).GetMethod("Render",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(controller, new object[] { expansion, string.Empty });

                Assert.That(unlocked.Button.interactable, Is.False,
                    "An unlocked region must stay disabled after an async request completes.");
                Assert.That(blocked.Button.interactable, Is.False,
                    "A region with an unmet prerequisite must stay disabled.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static TownRegionPurchaseButtonView CreateRegionButton(Transform parent,
            string regionId)
        {
            var root = new GameObject(regionId, typeof(RectTransform), typeof(Button));
            root.transform.SetParent(parent);
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(root.transform);
            var view = root.AddComponent<TownRegionPurchaseButtonView>();
            Set(view, "regionId", regionId);
            Set(view, "button", root.GetComponent<Button>());
            Set(view, "label", label);
            return view;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);

        private static BaseLayout ValidLayout() => new()
        {
            version = 2,
            ownerAccountId = "test-owner",
            factionId = Faction,
            mapTemplateId = TownExpansionPrototypeCatalog.MapTemplateId,
            mapVersion = TownExpansionPrototypeCatalog.MapVersion,
            unlockedRegionIds = new List<string> { TownExpansionPrototypeCatalog.CoreRegionId },
            towers = new List<PlacedTowerData>(),
            garrison = new List<GarrisonMonsterData>
            {
                new()
                {
                    cardId = "1/test-garrison",
                    position = Vector3.zero,
                },
            },
            minerCardIds = new List<string>(),
        };
    }
}
