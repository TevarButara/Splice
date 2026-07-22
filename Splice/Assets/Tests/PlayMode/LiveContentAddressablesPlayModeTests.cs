using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Splice.Tests.PlayMode
{
    public sealed class LiveContentAddressablesPlayModeTests
    {
        [UnityTest]
        public IEnumerator ValidationProbe_LoadsByAddressWithoutPlayerRebuild()
        {
            var handle = Addressables.LoadAssetAsync<ScriptableObject>("livecontent/probe");
            yield return handle;
            try
            {
                Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded),
                    handle.OperationException?.ToString());
                Assert.That(handle.Result, Is.Not.Null);
                var version = handle.Result.GetType().GetField("contentVersion")?.GetValue(handle.Result) as string;
                Assert.That(version, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }
    }
}
