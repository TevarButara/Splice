#if UNITY_EDITOR
using NUnit.Framework;
using Splice.Editor.Validation;
using Splice.Validation;

namespace Splice.Tests.EditMode
{
    public sealed class RaidSceneArchitectureEditModeTests
    {
        [Test]
        public void RequiredRaidScenes_ArePresentEnabledAndBootstrapIsFirst()
        {
            var report = new ContentValidationReport();
            RaidSceneArchitectureValidator.Validate(report);
            Assert.That(report.ErrorCount, Is.Zero, report.DetailedSummary());
        }
    }
}
#endif
