using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class RowanUltimatePlayModeTests
    {
        [UnityTest]
        public IEnumerator MultiDash_RuntimeTypeSplitsEveryDamagePointExactly()
        {
            var executionType = Type.GetType(
                "Splice.Combat.MultiDashHeroAbilityExecutionSO, Assembly-CSharp");
            Assert.That(executionType, Is.Not.Null);
            var splitDamage = executionType.GetMethod(
                "SplitDamage", BindingFlags.Public | BindingFlags.Static);
            Assert.That(splitDamage, Is.Not.Null);

            var execution = ScriptableObject.CreateInstance(executionType);
            try
            {
                var strikeCount = executionType.GetField("strikeCount");
                Assert.That(strikeCount, Is.Not.Null);
                strikeCount.SetValue(execution, 7);

                var total = 0;
                for (var strike = 0; strike < 7; strike++)
                {
                    total += (int)splitDamage.Invoke(
                        null, new object[] { 703, 7, strike });
                    yield return null;
                }

                Assert.That(total, Is.EqualTo(703),
                    "Seven runtime strikes must neither lose nor create damage.");
                Assert.That((int)splitDamage.Invoke(
                    null, new object[] { 703, 7, 0 }), Is.EqualTo(101));
                Assert.That((int)splitDamage.Invoke(
                    null, new object[] { 703, 7, 3 }), Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.Destroy(execution);
            }
            yield return null;
        }
    }
}
