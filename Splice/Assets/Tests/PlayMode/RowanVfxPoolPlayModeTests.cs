using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class RowanVfxPoolPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExpiredEffect_IsReusedWithoutAnotherInstantiation()
        {
            var service = Type.GetType("Splice.Combat.VfxPoolService, Assembly-CSharp");
            Assert.That(service, Is.Not.Null);
            var spawn = service.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
            var releaseAll = service.GetMethod("ReleaseAllForTests",
                BindingFlags.Public | BindingFlags.Static);
            var active = service.GetProperty("ActiveCount",
                BindingFlags.Public | BindingFlags.Static);
            var inactive = service.GetProperty("InactiveCount",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(spawn, Is.Not.Null);
            Assert.That(releaseAll, Is.Not.Null);
            releaseAll.Invoke(null, null);
            var baselineInactive = (int)inactive.GetValue(null);

            var source = new GameObject("VfxPoolRegressionSource");
            source.AddComponent<ParticleSystem>();
            source.SetActive(false);
            GameObject first = null;
            GameObject second = null;
            try
            {
                first = spawn.Invoke(null, new object[]
                {
                    source, Vector3.zero, Quaternion.identity, 0.05f, null,
                    Vector3.zero, 1f
                }) as GameObject;
                Assert.That(first, Is.Not.Null);
                Assert.That((int)active.GetValue(null), Is.EqualTo(1));
                yield return new WaitForSeconds(0.08f);
                Assert.That((int)active.GetValue(null), Is.Zero);
                Assert.That((int)inactive.GetValue(null),
                    Is.EqualTo(baselineInactive + 1));

                second = spawn.Invoke(null, new object[]
                {
                    source, Vector3.one, Quaternion.identity, 0.05f, null,
                    Vector3.zero, 1f
                }) as GameObject;
                Assert.That(second, Is.SameAs(first),
                    "The pool must reuse the expired instance.");
                Assert.That(second.transform.position, Is.EqualTo(Vector3.one));
            }
            finally
            {
                releaseAll?.Invoke(null, null);
                UnityEngine.Object.Destroy(source);
                var pool = GameObject.Find("[Splice VFX Pool]");
                if (pool != null) UnityEngine.Object.Destroy(pool);
            }
            yield return null;
        }
    }
}
