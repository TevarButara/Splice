using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class BuildZoneUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator BuildZone_DoesNotCreateDuplicateMetaUiAtRuntime()
        {
            var load = SceneManager.LoadSceneAsync("BuildZone", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
            yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var controllerType = Type.GetType("Splice.UI.PrototypeMetaHubController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            Component controller = null;
            var rootCount = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Prototype Meta UI") rootCount++;
                if (controller == null)
                    controller = root.GetComponentInChildren(controllerType, true) as Component;
            }

            Assert.That(scene.name, Is.EqualTo("BuildZone"));
            Assert.That(controller, Is.Not.Null);
            Assert.That((bool)controllerType.GetProperty("HasEditorAuthoredUi").GetValue(controller), Is.True);
            Assert.That(rootCount, Is.EqualTo(1),
                "Awake must bind the serialized shell and must not create another runtime Canvas.");
        }
    }
}
