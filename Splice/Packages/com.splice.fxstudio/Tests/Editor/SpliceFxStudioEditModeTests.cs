using NUnit.Framework;
using UnityEngine;

namespace Splice.FxStudio.Editor.Tests
{
    public sealed class SpliceFxStudioEditModeTests
    {
        [Test]
        public void LuminanceAlpha_ProducesTransparentBlackAndOpaqueWhite()
        {
            var pixels = new[]
            {
                new Color32(0, 0, 0, 255),
                new Color32(255, 255, 255, 255)
            };
            var settings = new SpliceFxAlphaSettings
            {
                mode = SpliceFxAlphaMode.LuminanceToAlpha,
                threshold = 0.05f,
                feather = 0.1f
            };

            SpliceFxAlphaProcessor.ProcessPixels(pixels, settings);

            Assert.That(pixels[0].a, Is.EqualTo(0));
            Assert.That(pixels[1].a, Is.EqualTo(255));
        }

        [Test]
        public void ChromaKey_RemovesKeyWithoutDestroyingForeground()
        {
            var pixels = new[]
            {
                new Color32(0, 255, 0, 255),
                new Color32(255, 64, 32, 255)
            };
            var settings = new SpliceFxAlphaSettings
            {
                mode = SpliceFxAlphaMode.ChromaKey,
                chromaKey = Color.green,
                tolerance = 0.03f,
                softness = 0.08f
            };

            SpliceFxAlphaProcessor.ProcessPixels(pixels, settings);

            Assert.That(pixels[0].a, Is.LessThan(5));
            Assert.That(pixels[1].a, Is.GreaterThan(240));
        }

        [Test]
        public void BlendDuration_IncludesLatestClipAndTail()
        {
            var sequence =
                ScriptableObject.CreateInstance<SpliceFxBlendSequence>();
            try
            {
                sequence.tailSeconds = 0.2f;
                sequence.clips.Add(new SpliceFxSequenceClip
                {
                    startSeconds = 0.5f,
                    durationSeconds = 1.25f
                });
                sequence.clips.Add(new SpliceFxSequenceClip
                {
                    startSeconds = 2f,
                    durationSeconds = 0.5f
                });

                Assert.That(sequence.DurationSeconds,
                    Is.EqualTo(2.7f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(sequence);
            }
        }

        [Test]
        public void Validator_RejectsClipWithoutSubFx()
        {
            var sequence =
                ScriptableObject.CreateInstance<SpliceFxBlendSequence>();
            try
            {
                sequence.sequenceId = "broken";
                sequence.clips.Add(new SpliceFxSequenceClip());
                var result = new SpliceFxValidationResult();

                SpliceFxValidator.ValidateSequence(sequence, result);

                Assert.That(result.Issues,
                    Has.Some.Matches<SpliceFxValidationIssue>(
                        issue =>
                            issue.Code ==
                            "FX_SEQUENCE_CLIP_MISSING_SUB"));
            }
            finally
            {
                Object.DestroyImmediate(sequence);
            }
        }

        [Test]
        public void SkillPackage_FindsExecutionStageDeterministically()
        {
            var package =
                ScriptableObject.CreateInstance<SpliceFxSkillPackage>();
            try
            {
                var impact = new SpliceFxStageBinding
                {
                    stage = SpliceFxStage.Impact
                };
                package.stages.Add(impact);

                Assert.That(package.Find(SpliceFxStage.Impact),
                    Is.SameAs(impact));
                Assert.That(package.Find(SpliceFxStage.Cast), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(package);
            }
        }

        [Test]
        public void Exporter_RejectsPathsOutsideGeneratedFolders()
        {
            var template = new GameObject("Template");
            var subFx =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                subFx.subFxId = "safe-export";
                subFx.templateOverride = template;

                Assert.Throws<System.InvalidOperationException>(
                    () => SpliceFxExporter.ExportSubFx(
                        subFx,
                        "Assets/SpliceFXStudio/UserArt"));
            }
            finally
            {
                Object.DestroyImmediate(subFx);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void PropertyDriver_CanBeAddedWithoutConstructorSideEffects()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                var driver = root.AddComponent<SpliceFxPropertyDriver>();

                Assert.DoesNotThrow(() => driver.Configure(definition));
                Assert.That(driver.Definition, Is.SameAs(definition));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void SequencePreview_RespectsTimeAndQualityMask()
        {
            var root = new GameObject("Sequence");
            var visual = new GameObject("Low Quality Layer");
            visual.transform.SetParent(root.transform);
            var runtime = root.AddComponent<SpliceFxSequenceRuntime>();
            try
            {
                runtime.ConfigureEditor(
                    new System.Collections.Generic.List<
                        SpliceFxRuntimeLayer>
                    {
                        new()
                        {
                            visual = visual,
                            startSeconds = 0.25f,
                            durationSeconds = 0.5f,
                            quality = SpliceFxQualityMask.Low
                        }
                    },
                    1f);

                runtime.EvaluatePreview(0.4f,
                    SpliceFxQualityTier.High);
                Assert.That(visual.activeSelf, Is.False);

                runtime.EvaluatePreview(0.4f,
                    SpliceFxQualityTier.Low);
                Assert.That(visual.activeSelf, Is.True);

                runtime.EvaluatePreview(0.9f,
                    SpliceFxQualityTier.Low);
                Assert.That(visual.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
