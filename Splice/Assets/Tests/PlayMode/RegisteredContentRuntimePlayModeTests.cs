using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class RegisteredContentRuntimePlayModeTests
    {
        [UnityTest]
        public IEnumerator FactionRegistry_ResolvesCompositeCardIdAtRuntime()
        {
            var factionType = RequireProjectType("Splice.Data.FactionSO");
            var cardType = RequireProjectType("Splice.Data.CardDefinitionSO");
            var registryType = RequireProjectType("Splice.Data.FactionRegistrySO");
            var faction = ScriptableObject.CreateInstance(factionType);
            var card = ScriptableObject.CreateInstance(cardType);
            var registry = ScriptableObject.CreateInstance(registryType);

            try
            {
                factionType.GetField("factionId").SetValue(faction, "runtime_test");
                cardType.GetField("cardId").SetValue(card, "breacher");
                ((IList)factionType.GetField("cards").GetValue(faction)).Add(card);
                var factionsField = registryType.GetField("factions", BindingFlags.Instance | BindingFlags.NonPublic);
                ((IList)factionsField.GetValue(registry)).Add(faction);

                yield return null;

                var resolved = registryType.GetMethod("ResolveCard").Invoke(registry, new object[] { "runtime_test/breacher" });
                Assert.That(resolved, Is.SameAs(card), "Runtime registry failed to resolve a valid composite content id.");
            }
            finally
            {
                UnityEngine.Object.Destroy(registry);
                UnityEngine.Object.Destroy(card);
                UnityEngine.Object.Destroy(faction);
            }
        }

        private static Type RequireProjectType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Project runtime type '{fullName}' was not loaded.");
            return type;
        }
    }
}
