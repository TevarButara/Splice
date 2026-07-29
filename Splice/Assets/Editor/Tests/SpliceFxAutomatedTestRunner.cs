#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Splice.Editor.Tests
{
    public static class SpliceFxAutomatedTestRunner
    {
        [MenuItem("Splice/FX Studio/Run EditMode Tests",
            priority = 1730)]
        public static void RunEditMode()
        {
            Run(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[]
                {
                    "Splice.FxStudio.Editor.Tests"
                }
            }, "EditMode");
        }

        [MenuItem("Splice/FX Studio/Run PlayMode Regression",
            priority = 1731)]
        public static void RunPlayMode()
        {
            Run(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[]
                {
                    "Splice.Tests.PlayMode.SpliceFxStudioPlayModeTests"
                }
            }, "PlayMode");
        }

        private static void Run(Filter filter, string label)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks(label));
            Debug.Log($"[Splice FX Tests] START {label}");
            api.Execute(new ExecutionSettings(filter));
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly string label;

            public Callbacks(string label) => this.label = label;

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var message =
                    $"[Splice FX Tests] RESULT {label}: " +
                    $"{result.TestStatus} | Passed {result.PassCount}, " +
                    $"Failed {result.FailCount}, Skipped {result.SkipCount}";
                if (result.FailCount > 0) Debug.LogError(message);
                else Debug.Log(message);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus != TestStatus.Failed) return;
                Debug.LogError(
                    $"[Splice FX Tests] FAIL {result.FullName}: " +
                    $"{result.Message}\n{result.StackTrace}");
            }
        }
    }
}
#endif
