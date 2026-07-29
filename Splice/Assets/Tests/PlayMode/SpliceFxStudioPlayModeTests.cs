using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class SpliceFxStudioPlayModeTests
    {
        [UnityTest]
        public IEnumerator HeroAbility_RecognizesExportedStudioPackage()
        {
            var abilityType = Type.GetType(
                "Splice.Data.HeroAbilityDefinitionSO, Assembly-CSharp");
            var packageType = Type.GetType(
                "Splice.FxStudio.SpliceFxSkillPackage, Splice.FxStudio.Runtime");
            var bindingType = Type.GetType(
                "Splice.FxStudio.SpliceFxStageBinding, Splice.FxStudio.Runtime");
            var stageType = Type.GetType(
                "Splice.FxStudio.SpliceFxStage, Splice.FxStudio.Runtime");
            Assert.That(abilityType, Is.Not.Null);
            Assert.That(packageType, Is.Not.Null);
            Assert.That(bindingType, Is.Not.Null);
            Assert.That(stageType, Is.Not.Null);

            var ability = ScriptableObject.CreateInstance(abilityType);
            var package = ScriptableObject.CreateInstance(packageType);
            var prefab = new GameObject("FX Studio Regression Prefab");
            try
            {
                var binding = Activator.CreateInstance(bindingType);
                bindingType.GetField("stage")?.SetValue(binding,
                    Enum.Parse(stageType, "Impact"));
                bindingType.GetField("exportedPrefab")?.SetValue(binding,
                    prefab);
                var stages = packageType.GetField("stages")?.GetValue(package);
                stages?.GetType().GetMethod("Add")?.Invoke(stages,
                    new[] { binding });
                abilityType.GetField("fxStudioPackage")?.SetValue(ability,
                    package);

                var hasStaged = (bool)abilityType.GetProperty(
                        "HasStagedVfx",
                        BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(ability);
                Assert.That(hasStaged, Is.True,
                    "An exported Studio stage must enter the existing pooled presentation path.");
            }
            finally
            {
                UnityEngine.Object.Destroy(ability);
                UnityEngine.Object.Destroy(package);
                UnityEngine.Object.Destroy(prefab);
            }
            yield return null;
        }
    }
}
