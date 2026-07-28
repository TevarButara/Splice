using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Splice.Tests.PlayMode
{
    public sealed class WorldForestPlayModeTests
    {
        [TearDown]
        public void TearDown() =>
            Type.GetType("Splice.World.ForestHuntProgressStore, Assembly-CSharp")
                ?.GetMethod("DeleteForTests")?.Invoke(null, null);

        [UnityTest]
        public IEnumerator WorldMap_LoadsWithWorkingEditorAuthoredController()
        {
            yield return SceneManager.LoadSceneAsync("WorldMap", LoadSceneMode.Single);
            yield return null;

            var type = Type.GetType("Splice.World.WorldMapController, Assembly-CSharp");
            var controller = UnityEngine.Object.FindFirstObjectByType(type) as Behaviour;
            Assert.That(controller, Is.Not.Null);
            Assert.That((bool)type.GetProperty("HasEditorAuthoredUi").GetValue(controller), Is.True);
            Assert.That(controller.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator WorldMap_RaidNodeOpensRaidTargetPanel()
        {
            yield return SceneManager.LoadSceneAsync("WorldMap", LoadSceneMode.Single);
            yield return null;

            var worldType = Type.GetType("Splice.World.WorldMapController, Assembly-CSharp");
            var world = UnityEngine.Object.FindFirstObjectByType(worldType) as Component;
            var raidButton = worldType.GetField("raidButton",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(world) as Button;
            Assert.That(raidButton, Is.Not.Null);
            raidButton.onClick.Invoke();

            var frames = 0;
            while (SceneManager.GetActiveScene().name != "BuildZone" && frames++ < 120)
                yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("BuildZone"));
            var hubType = Type.GetType("Splice.UI.PrototypeMetaHubController, Assembly-CSharp");
            var hub = UnityEngine.Object.FindFirstObjectByType(hubType);
            Assert.That(hub, Is.Not.Null);
            Assert.That((bool)hubType.GetProperty("IsRaidPanelVisible").GetValue(hub), Is.True);
        }

        [UnityTest]
        public IEnumerator Forest_KillAndExtractionSecurePersistentFragments()
        {
            yield return SceneManager.LoadSceneAsync("ForestZone", LoadSceneMode.Single);
            yield return null;

            var encounterType = Type.GetType("Splice.World.ForestEncounterController, Assembly-CSharp");
            var heroType = Type.GetType("Splice.World.ForestHeroController, Assembly-CSharp");
            var targetType = Type.GetType("Splice.World.ForestMonsterTarget, Assembly-CSharp");
            var encounter = UnityEngine.Object.FindFirstObjectByType(encounterType) as Component;
            var hero = UnityEngine.Object.FindFirstObjectByType(heroType) as Component;
            var target = UnityEngine.Object.FindFirstObjectByType(targetType) as Component;
            Assert.That(encounter, Is.Not.Null);
            Assert.That(hero, Is.Not.Null);
            Assert.That(target, Is.Not.Null);
            hero.transform.position = target.transform.position + Vector3.back;

            encounterType.GetMethod("AttackNearest").Invoke(encounter, null);
            encounterType.GetMethod("AttackNearest").Invoke(encounter, null);
            yield return null;

            Assert.That((int)encounterType.GetProperty("CarriedFragments").GetValue(encounter),
                Is.GreaterThan(0));
            encounterType.GetMethod("Extract").Invoke(encounter, null);
            Assert.That((bool)encounterType.GetProperty("HasEnded").GetValue(encounter), Is.True);
            var store = Type.GetType("Splice.World.ForestHuntProgressStore, Assembly-CSharp");
            var progress = store.GetMethod("Load").Invoke(null, new object[] { null });
            Assert.That((long)progress.GetType().GetField("revision").GetValue(progress), Is.EqualTo(1));
        }
    }
}
