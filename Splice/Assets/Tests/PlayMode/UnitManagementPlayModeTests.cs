using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class UnitManagementPlayModeTests
    {
        [UnityTest]
        public IEnumerator RaidArena_EditorAuthoredManagementPopupsBindAtRuntime()
        {
            var operation = SceneManager.LoadSceneAsync("RaidArena", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            while (!operation.isDone) yield return null;
            yield return null;

            var towerType = RequireProjectType("Splice.Input.TowerInteractionController");
            var monsterType = RequireProjectType("Splice.Input.MonsterInteractionController");
            var tower = UnityEngine.Object.FindFirstObjectByType(towerType) as Component;
            var monster = UnityEngine.Object.FindFirstObjectByType(monsterType) as Component;
            Assert.That(tower, Is.Not.Null);
            Assert.That(monster, Is.Not.Null);

            towerType.GetMethod("EnsureBinding")?.Invoke(tower, null);
            monsterType.GetMethod("EnsureBinding")?.Invoke(monster, null);
            Assert.That(Property<bool>(towerType, tower, "HasCompleteBinding"), Is.True);
            Assert.That(Property<bool>(monsterType, monster, "HasCompleteBinding"), Is.True);

            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene.IsValid() && item.scene.name == "RaidArena")
                .ToArray();
            Assert.That(sceneObjects.Count(item => item.name == "Panel_Tower"), Is.EqualTo(1));
            Assert.That(sceneObjects.Count(item => item.name == "Panel_Monster"), Is.EqualTo(1));
            Assert.That(
                sceneObjects.Single(item => item.name == "Panel_Monster").transform.parent,
                Is.Not.Null,
                "The popup must remain an Editor-authored Canvas child, not a runtime overlay root.");
        }

        private static Type RequireProjectType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Project runtime type '{fullName}' was not loaded.");
            return type;
        }

        private static T Property<T>(Type type, Component instance, string name) =>
            (T)type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);
    }
}
