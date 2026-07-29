#if UNITY_EDITOR
using Splice.FxStudio.Editor;
using Splice.Validation;

namespace Splice.Editor.Validation
{
    public static class SpliceFxStudioValidatorAdapter
    {
        public static void Validate(ContentValidationReport report)
        {
            var fxResult = SpliceFxValidator.ValidateProject();
            foreach (var issue in fxResult.Issues)
            {
                if (issue.Severity == SpliceFxValidationSeverity.Error)
                    report.Error(issue.Code, issue.Message, issue.Context);
                else
                    report.Warning(issue.Code, issue.Message, issue.Context);
            }
        }
    }
}
#endif
