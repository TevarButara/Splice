#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using Splice.Combat;
using Splice.Core;
using Splice.Data;
using Splice.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Splice.Tests.EditMode
{
    public sealed class UnitManagementEditModeTests
    {
        private const string RaidArenaPath = "Assets/=======SCENES/RaidArena.unity";

        [Test]
        public void RepairCostAndTimedSteps_AreDeterministic()
        {
            Assert.That(UnitEconomyMath.RepairCost(100, 50, 100, 0.5f), Is.EqualTo(25));
            Assert.That(UnitEconomyMath.RepairCost(100, 99, 100, 0.5f), Is.EqualTo(1));
            Assert.That(UnitEconomyMath.RepairCost(100, 100, 100, 0.5f), Is.Zero);

            var distributed = Enumerable.Range(1, 5)
                .Sum(step => UnitEconomyMath.RepairAmountAtStep(50, step, 5));
            Assert.That(distributed, Is.EqualTo(50));
            Assert.That(UnitEconomyMath.RepairAmountAtStep(50, 1, 5), Is.EqualTo(10));
        }

        [Test]
        public void SellRefund_IsConfiguredPercentageOfBuildPrice()
        {
            Assert.That(UnitEconomyMath.SellRefund(200, 0.5f), Is.EqualTo(100));
            Assert.That(UnitEconomyMath.SellRefund(200, 0f), Is.Zero);
            Assert.That(UnitEconomyMath.SellRefund(200, 2f), Is.EqualTo(200));
        }

        [Test]
        public void EconomyAndManagementAuthority_FailClosedOnInvalidIntent()
        {
            Assert.That(GoldController.IsValidSpendAmount(-1), Is.False,
                "Negative spend must never mint gold.");
            Assert.That(GoldController.IsValidSpendAmount(0), Is.True);
            Assert.That(UnitManagementAuthority.IsAuthorized(0, 0), Is.True);
            Assert.That(UnitManagementAuthority.IsAuthorized(1, 0), Is.False,
                "Remote clients remain blocked until a verified backend side-claim exists.");
        }

        [Test]
        public void NaturalTowerAndRaptorTiers_AreConnected()
        {
            var tower1 = AssetDatabase.LoadAssetAtPath<TowerDefinitionSO>(
                "Assets/Prefabs/Natural/Tower/Nat-tw1-lv1-SO.asset");
            var tower2 = AssetDatabase.LoadAssetAtPath<TowerDefinitionSO>(
                "Assets/Prefabs/Natural/Tower/Nat-tw1-lv2-SO.asset");
            var tower3 = AssetDatabase.LoadAssetAtPath<TowerDefinitionSO>(
                "Assets/Prefabs/Natural/Tower/Nat-tw1-lv3-SO.asset");
            var raptor1 = AssetDatabase.LoadAssetAtPath<MonsterDefinitionSO>(
                "Assets/Prefabs/Natural/Charactor/SO/1_SO_raptor_lv1.asset");
            var raptor2 = AssetDatabase.LoadAssetAtPath<MonsterDefinitionSO>(
                "Assets/Prefabs/Natural/Charactor/SO/1_SO_raptor_lv2.asset");
            var raptor3 = AssetDatabase.LoadAssetAtPath<MonsterDefinitionSO>(
                "Assets/Prefabs/Natural/Charactor/SO/1_SO_raptor_lv3.asset");

            Assert.That(tower1, Is.Not.Null);
            Assert.That(tower2, Is.Not.Null);
            Assert.That(tower3, Is.Not.Null);
            Assert.That(raptor1, Is.Not.Null);
            Assert.That(raptor2, Is.Not.Null);
            Assert.That(raptor3, Is.Not.Null);
            Assert.That(tower1.nextTier, Is.SameAs(tower2));
            Assert.That(tower2.nextTier, Is.SameAs(tower3));
            Assert.That(tower3.nextTier, Is.Null);
            Assert.That(raptor1.nextTier, Is.SameAs(raptor2));
            Assert.That(raptor2.nextTier, Is.SameAs(raptor3));
            Assert.That(raptor3.nextTier, Is.Null);
        }

        [Test]
        public void RaidArena_UsesEditorAuthoredTowerAndMonsterPopups()
        {
            var existing = SceneManager.GetSceneByPath(RaidArenaPath);
            var openedForTest = !existing.isLoaded;
            var scene = openedForTest
                ? EditorSceneManager.OpenScene(RaidArenaPath, OpenSceneMode.Additive)
                : existing;
            try
            {
                var transforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<UnityEngine.Transform>(true))
                    .ToArray();
                var tower = transforms.Single(item => item.name == "TowerInteractionController")
                    .GetComponent<TowerInteractionController>();
                var monster = transforms.Single(item => item.name == "MonsterInteractionController")
                    .GetComponent<MonsterInteractionController>();

                tower.EnsureBinding();
                monster.EnsureBinding();
                Assert.That(tower.HasCompleteBinding, Is.True);
                Assert.That(monster.HasCompleteBinding, Is.True);
                Assert.That(transforms.Count(item => item.name == "Panel_Tower"), Is.EqualTo(1));
                Assert.That(transforms.Count(item => item.name == "Panel_Monster"), Is.EqualTo(1));
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
