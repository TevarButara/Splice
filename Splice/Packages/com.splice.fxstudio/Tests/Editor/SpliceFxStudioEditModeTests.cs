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

        [Test]
        public void MotionPlayer_PulseAnimatesImageScaleAtExactTime()
        {
            var root = new GameObject("Pulse");
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                var pulse = SpliceFxMotionLayer.Create(
                    SpliceFxMotionType.Pulse);
                pulse.speed = 1f;
                pulse.amount = 0.5f;
                pulse.phase = 0f;
                definition.motions.Add(pulse);
                var player = root.AddComponent<SpliceFxMotionPlayer>();
                player.Configure(definition);

                player.EvaluatePreview(0.25f);

                Assert.That(root.transform.localScale.x,
                    Is.EqualTo(1.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void MotionPlayer_SpinUsesDegreesPerSecond()
        {
            var root = new GameObject("Spin");
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                var spin = SpliceFxMotionLayer.Create(
                    SpliceFxMotionType.Spin);
                spin.speed = 90f;
                definition.motions.Add(spin);
                var player = root.AddComponent<SpliceFxMotionPlayer>();
                player.Configure(definition);

                player.EvaluatePreview(1f);

                Assert.That(Quaternion.Angle(
                        Quaternion.identity,
                        root.transform.localRotation),
                    Is.EqualTo(90f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Validator_RejectsMotionWithZeroAxis()
        {
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                var spin = SpliceFxMotionLayer.Create(
                    SpliceFxMotionType.Spin);
                spin.axis = Vector3.zero;
                definition.motions.Add(spin);
                var result = new SpliceFxValidationResult();

                SpliceFxValidator.ValidateSubFx(definition, result);

                Assert.That(result.Issues,
                    Has.Some.Matches<SpliceFxValidationIssue>(
                        issue =>
                            issue.Code ==
                            "FX_MOTION_AXIS_INVALID"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void RadialFive_CreatesFiveEvenlySpacedPoses()
        {
            var layout = SpliceFxInstanceLayout.RadialFive();

            var poses = SpliceFxInstanceLayoutSolver.Build(layout);

            Assert.That(poses, Has.Count.EqualTo(5));
            foreach (var pose in poses)
                Assert.That(pose.Position.magnitude,
                    Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(Vector3.Distance(
                    poses[0].Position, poses[1].Position),
                Is.GreaterThan(1f));
        }

        [Test]
        public void RandomRing_IsDeterministicForSameSeed()
        {
            var layout = new SpliceFxInstanceLayout
            {
                mode = SpliceFxInstanceLayoutMode.RandomRing,
                highCount = 8,
                randomSeed = 90210,
                innerRadius = 1f,
                radius = 3f
            };

            var first = SpliceFxInstanceLayoutSolver.Build(layout);
            var second = SpliceFxInstanceLayoutSolver.Build(layout);

            Assert.That(second, Has.Count.EqualTo(first.Count));
            for (var i = 0; i < first.Count; i++)
                Assert.That(second[i].Position,
                    Is.EqualTo(first[i].Position));
        }

        [Test]
        public void InstanceGroup_UsesQualityCounts()
        {
            var root = new GameObject("Instances");
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            var transforms = new System.Collections.Generic.List<Transform>();
            var enabled = new System.Collections.Generic.List<bool>();
            try
            {
                definition.instanceLayout =
                    SpliceFxInstanceLayout.RadialFive();
                for (var i = 0; i < 5; i++)
                {
                    var child = new GameObject($"Instance {i}");
                    child.transform.SetParent(root.transform);
                    transforms.Add(child.transform);
                    enabled.Add(true);
                }
                var group = root.AddComponent<SpliceFxInstanceGroup>();
                group.ConfigureEditor(definition, transforms, enabled);

                group.EvaluatePreview(0f, SpliceFxQualityTier.Low);
                Assert.That(transforms[0].gameObject.activeSelf, Is.True);
                Assert.That(transforms[2].gameObject.activeSelf, Is.True);
                Assert.That(transforms[3].gameObject.activeSelf, Is.False);

                group.EvaluatePreview(0f, SpliceFxQualityTier.High);
                Assert.That(transforms[4].gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void InstanceGroup_RestartDoesNotAccumulateSelfSpin()
        {
            var root = new GameObject("Instances");
            var child = new GameObject("Instance");
            child.transform.SetParent(root.transform);
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                definition.instanceLayout.selfSpinDegreesPerSecond = 90f;
                var group = root.AddComponent<SpliceFxInstanceGroup>();
                group.ConfigureEditor(
                    definition,
                    new System.Collections.Generic.List<Transform>
                        { child.transform },
                    new System.Collections.Generic.List<bool> { true });

                group.EvaluatePreview(1f, SpliceFxQualityTier.High);
                Assert.That(Quaternion.Angle(
                        Quaternion.identity,
                        child.transform.localRotation),
                    Is.EqualTo(90f).Within(0.1f));

                group.RestartInstances();
                Assert.That(Quaternion.Angle(
                        Quaternion.identity,
                        child.transform.localRotation),
                    Is.EqualTo(0f).Within(0.1f));

                group.EvaluatePreview(1f, SpliceFxQualityTier.High);
                Assert.That(Quaternion.Angle(
                        Quaternion.identity,
                        child.transform.localRotation),
                    Is.EqualTo(90f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void InstanceGroup_StaggersVisibilityAndUsesLocalSpinTime()
        {
            var root = new GameObject("Instances");
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            var transforms = new System.Collections.Generic.List<Transform>();
            var enabled = new System.Collections.Generic.List<bool>();
            try
            {
                definition.instanceLayout =
                    SpliceFxInstanceLayout.RadialFive();
                definition.instanceLayout.highCount = 3;
                definition.instanceLayout.mediumCount = 3;
                definition.instanceLayout.lowCount = 3;
                definition.instanceLayout.activationDelayStep = 0.5f;
                definition.instanceLayout.selfSpinDegreesPerSecond = 90f;
                for (var i = 0; i < 3; i++)
                {
                    var child = new GameObject($"Instance {i}");
                    child.transform.SetParent(root.transform);
                    transforms.Add(child.transform);
                    enabled.Add(true);
                }
                var group = root.AddComponent<SpliceFxInstanceGroup>();
                group.ConfigureEditor(definition, transforms, enabled);

                group.EvaluatePreview(0.25f,
                    SpliceFxQualityTier.High);
                Assert.That(transforms[0].gameObject.activeSelf, Is.True);
                Assert.That(transforms[1].gameObject.activeSelf, Is.False);
                Assert.That(Quaternion.Angle(
                        Quaternion.identity,
                        transforms[0].localRotation),
                    Is.EqualTo(22.5f).Within(0.1f));

                group.EvaluatePreview(0.75f,
                    SpliceFxQualityTier.High);
                Assert.That(transforms[1].gameObject.activeSelf, Is.True);
                Assert.That(transforms[2].gameObject.activeSelf, Is.False);
                Assert.That(Quaternion.Angle(
                        Quaternion.identity,
                        transforms[1].localRotation),
                    Is.EqualTo(22.5f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ProceduralLayout_ConvertsToEditableManualPoses()
        {
            var radial = SpliceFxInstanceLayout.RadialFive();
            radial.motionScope =
                SpliceFxInstanceMotionScope.EachInstance;

            var manual = SpliceFxInstanceLayoutSolver.ToManual(radial);

            Assert.That(manual.mode,
                Is.EqualTo(SpliceFxInstanceLayoutMode.Manual));
            Assert.That(manual.manualInstances, Has.Count.EqualTo(5));
            Assert.That(manual.manualInstances[0].localPosition.magnitude,
                Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(manual.mediumCount, Is.EqualTo(4));
            Assert.That(manual.lowCount, Is.EqualTo(3));
            Assert.That(manual.motionScope,
                Is.EqualTo(SpliceFxInstanceMotionScope.EachInstance));
        }

        [Test]
        public void ReverseStagger_StartsFromLastInstance()
        {
            var layout = SpliceFxInstanceLayout.RadialFive();
            layout.activationDelayStep = 0.2f;
            layout.reverseActivationOrder = true;

            Assert.That(layout.DelayFor(4, 5),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(layout.DelayFor(0, 5),
                Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void Validator_WarnsWhenStaggerExceedsLifetime()
        {
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                definition.lifetime = 1f;
                definition.instanceLayout =
                    SpliceFxInstanceLayout.RadialFive();
                definition.instanceLayout.activationDelayStep = 0.3f;
                var result = new SpliceFxValidationResult();

                SpliceFxValidator.ValidateSubFx(definition, result);

                Assert.That(result.Issues,
                    Has.Some.Matches<SpliceFxValidationIssue>(
                        issue =>
                            issue.Code ==
                            "FX_INSTANCE_STAGGER_EXCEEDS_LIFETIME"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void PreviewSimulation_IsDeterministicAtExactTime()
        {
            var root = new GameObject("Preview Particle");
            try
            {
                var particle = root.AddComponent<ParticleSystem>();
                var main = particle.main;
                main.loop = true;
                main.startLifetime = 1f;
                main.startSpeed = 1f;
                var emission = particle.emission;
                emission.rateOverTime = 24f;
                var motion = root.AddComponent<SpliceFxMotionPlayer>();

                SpliceFxPreviewViewport.ConfigureExternalPreview(root);
                Assert.That(particle.useAutoRandomSeed, Is.False);
                Assert.That(motion.ExternalTimeControl, Is.True);

                SpliceFxPreviewViewport.SimulateVisual(
                    root, 0.5f, SpliceFxQualityTier.High);
                var first = new ParticleSystem.Particle[
                    particle.particleCount];
                var firstCount = particle.GetParticles(first);

                SpliceFxPreviewViewport.SimulateVisual(
                    root, 0.5f, SpliceFxQualityTier.High);
                var second = new ParticleSystem.Particle[
                    particle.particleCount];
                var secondCount = particle.GetParticles(second);

                Assert.That(firstCount, Is.GreaterThan(0));
                Assert.That(secondCount, Is.EqualTo(firstCount));
                for (var i = 0; i < firstCount; i++)
                    Assert.That(second[i].position,
                        Is.EqualTo(first[i].position));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_RejectsEmptyManualLayout()
        {
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                definition.instanceLayout.mode =
                    SpliceFxInstanceLayoutMode.Manual;
                definition.instanceLayout.manualInstances.Clear();
                var result = new SpliceFxValidationResult();

                SpliceFxValidator.ValidateSubFx(definition, result);

                Assert.That(result.Issues,
                    Has.Some.Matches<SpliceFxValidationIssue>(
                        issue =>
                            issue.Code ==
                            "FX_INSTANCE_MANUAL_EMPTY"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }
    }
}
