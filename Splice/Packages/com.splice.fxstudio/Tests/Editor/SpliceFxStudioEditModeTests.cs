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
        public void VisualFactory_BuildsMultipleTrailAndParticleLayers()
        {
            var template = new GameObject("Template");
            var definition =
                ScriptableObject.CreateInstance<
                    SpliceFxSubEffectDefinition>();
            var layerTexture = new Texture2D(32, 16);
            var layerSprite = Sprite.Create(layerTexture,
                new Rect(8f, 4f, 16f, 8f),
                new Vector2(0.5f, 0.5f));
            GameObject built = null;
            try
            {
                definition.templateOverride = template;
                var trail =
                    SpliceFxVisualLayerDefinition.CreateTrail();
                trail.sprite = layerSprite;
                trail.instanceLayout.mode =
                    SpliceFxInstanceLayoutMode.Radial;
                trail.instanceLayout.highCount = 3;
                trail.instanceLayout.mediumCount = 2;
                trail.instanceLayout.lowCount = 1;
                var particle =
                    SpliceFxVisualLayerDefinition.CreateParticle();
                particle.texture = Texture2D.whiteTexture;
                definition.visualLayers.Add(trail);
                definition.visualLayers.Add(particle);

                built = SpliceFxVisualFactory.Build(definition);

                Assert.That(
                    built.GetComponentsInChildren<TrailRenderer>(true),
                    Has.Length.EqualTo(3));
                Assert.That(
                    built.GetComponentsInChildren<ParticleSystem>(true),
                    Has.Length.EqualTo(1));
                Assert.That(
                    built.GetComponentsInChildren<
                        SpliceFxAuxiliaryLayerMarker>(true),
                    Has.Length.EqualTo(2));
                var block = new MaterialPropertyBlock();
                built.GetComponentsInChildren<TrailRenderer>(true)[0]
                    .GetPropertyBlock(block);
                var transform = block.GetVector("_BaseMap_ST");
                Assert.That(transform.x,
                    Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(transform.y,
                    Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(transform.z,
                    Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(transform.w,
                    Is.EqualTo(0.25f).Within(0.0001f));
            }
            finally
            {
                if (built != null) Object.DestroyImmediate(built);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(layerSprite);
                Object.DestroyImmediate(layerTexture);
            }
        }

        [Test]
        public void GradientStrokeDriver_SendsShaderProperties()
        {
            var shader = Shader.Find(
                "Splice/FX Studio/Gradient Stroke Card");
            Assert.That(shader, Is.Not.Null);
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(shader);
            var texture = new Texture2D(32, 16);
            var definition =
                ScriptableObject.CreateInstance<
                    SpliceFxSubEffectDefinition>();
            try
            {
                root.GetComponent<Renderer>().sharedMaterial = material;
                definition.sourceTexture = texture;
                definition.mainColor = Color.red;
                definition.gradientMode =
                    SpliceFxGradientMode.Vertical;
                definition.strokeMode = SpliceFxStrokeMode.Solid;
                definition.strokeWidth = 3f;
                definition.outerGlowEnabled = true;
                definition.outerGlowColor = Color.yellow;
                definition.outerGlowIntensity = 2.5f;
                definition.outerGlowRadius = 12f;
                definition.outerGlowSoftness = 1.75f;
                var driver =
                    root.AddComponent<SpliceFxPropertyDriver>();
                driver.Configure(definition);
                var block = new MaterialPropertyBlock();
                root.GetComponent<Renderer>().GetPropertyBlock(block);

                Assert.That(block.GetFloat("_GradientMode"),
                    Is.EqualTo(1f));
                Assert.That(block.GetFloat("_StrokeMode"),
                    Is.EqualTo(1f));
                Assert.That(block.GetFloat("_StrokeWidth"),
                    Is.EqualTo(3f));
                Assert.That(block.GetFloat("_OuterGlowEnabled"),
                    Is.EqualTo(1f));
                Assert.That(block.GetColor("_OuterGlowColor"),
                    Is.EqualTo(Color.yellow));
                Assert.That(block.GetFloat("_OuterGlowIntensity"),
                    Is.EqualTo(2.5f));
                Assert.That(block.GetFloat("_OuterGlowRadius"),
                    Is.EqualTo(12f));
                Assert.That(block.GetFloat("_OuterGlowSoftness"),
                    Is.EqualTo(1.75f));
                Assert.That(block.GetTexture("_GradientMap"),
                    Is.Not.Null);
                Assert.That(block.GetColor("_BaseColor"),
                    Is.EqualTo(Color.white));
                var texelSize =
                    block.GetVector("_BaseMap_TexelSize");
                Assert.That(texelSize.x,
                    Is.EqualTo(1f / 32f).Within(0.0001f));
                Assert.That(texelSize.y,
                    Is.EqualTo(1f / 16f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void VisualFactory_AutoUpgradesMeshMaterialForGradient()
        {
            var template =
                GameObject.CreatePrimitive(PrimitiveType.Quad);
            var originalShader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            Assert.That(originalShader, Is.Not.Null);
            var originalMaterial = new Material(originalShader);
            var definition =
                ScriptableObject.CreateInstance<
                    SpliceFxSubEffectDefinition>();
            GameObject built = null;
            try
            {
                var templateRenderer =
                    template.GetComponent<MeshRenderer>();
                templateRenderer.sharedMaterial = originalMaterial;
                definition.templateOverride = template;
                definition.sourceTexture = Texture2D.whiteTexture;
                definition.gradientMode =
                    SpliceFxGradientMode.RadialInsideOut;
                definition.outerGlowEnabled = true;

                built = SpliceFxVisualFactory.Build(definition);

                var builtRenderer =
                    built.GetComponentInChildren<MeshRenderer>(true);
                Assert.That(builtRenderer, Is.Not.Null);
                Assert.That(
                    builtRenderer.sharedMaterial.HasProperty(
                        "_GradientMap"),
                    Is.True);
                Assert.That(
                    builtRenderer.sharedMaterial.HasProperty(
                        "_OuterGlowEnabled"),
                    Is.True);
                Assert.That(templateRenderer.sharedMaterial,
                    Is.SameAs(originalMaterial),
                    "Preview/export compatibility must not alter the source prefab.");
            }
            finally
            {
                if (built != null) Object.DestroyImmediate(built);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(originalMaterial);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void SpriteSource_UsesSubRectAndSurvivesMotionEvaluation()
        {
            var shader = Shader.Find(
                "Splice/FX Studio/Gradient Stroke Card");
            Assert.That(shader, Is.Not.Null);
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(shader);
            var texture = new Texture2D(32, 16);
            var sprite = Sprite.Create(texture,
                new Rect(8f, 4f, 16f, 8f),
                new Vector2(0.5f, 0.5f));
            var definition =
                ScriptableObject.CreateInstance<
                    SpliceFxSubEffectDefinition>();
            try
            {
                root.GetComponent<Renderer>().sharedMaterial = material;
                definition.sourceSprite = sprite;
                definition.mainColor = Color.red;
                definition.gradientMode =
                    SpliceFxGradientMode.Horizontal;
                var driver =
                    root.AddComponent<SpliceFxPropertyDriver>();
                driver.Configure(definition);
                var motion =
                    root.AddComponent<SpliceFxMotionPlayer>();
                motion.Configure(definition);
                motion.EvaluatePreview(0.25f);

                Assert.That(definition.EffectiveTexture,
                    Is.SameAs(texture));
                Assert.That(definition.EffectivePixelSize,
                    Is.EqualTo(new Vector2Int(16, 8)));
                var block = new MaterialPropertyBlock();
                root.GetComponent<Renderer>().GetPropertyBlock(block);
                var transform = block.GetVector("_BaseMap_ST");
                Assert.That(transform.x,
                    Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(transform.y,
                    Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(transform.z,
                    Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(transform.w,
                    Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(block.GetColor("_BaseColor"),
                    Is.EqualTo(Color.white));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void LayerInlineMotion_RotatesIndependently()
        {
            var root = new GameObject("Layer Motion");
            try
            {
                var spin = SpliceFxMotionLayer.Create(
                    SpliceFxMotionType.Spin);
                spin.speed = 360f;
                spin.durationSeconds = 2f;
                spin.loop = false;
                var player = root.AddComponent<SpliceFxMotionPlayer>();
                player.ConfigureInline(
                    new System.Collections.Generic.List<
                        SpliceFxMotionLayer> { spin });

                player.EvaluatePreview(1f);

                Assert.That(Quaternion.Angle(
                        Quaternion.identity,
                        root.transform.localRotation),
                    Is.EqualTo(180f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LayerInlineMotion_AppliesFadeAndUvScroll()
        {
            var shader = Shader.Find(
                "Splice/FX Studio/Gradient Stroke Card");
            Assert.That(shader, Is.Not.Null);
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(shader);
            try
            {
                root.GetComponent<Renderer>().sharedMaterial = material;
                var fade = SpliceFxMotionLayer.Create(
                    SpliceFxMotionType.FadeOut);
                fade.durationSeconds = 1f;
                fade.amount = 1f;
                var scroll = SpliceFxMotionLayer.Create(
                    SpliceFxMotionType.UvScroll);
                scroll.durationSeconds = 1f;
                scroll.speed = 1f;
                scroll.uvSpeed = new Vector2(0.25f, 0.5f);
                var player = root.AddComponent<SpliceFxMotionPlayer>();
                player.ConfigureInline(
                    new System.Collections.Generic.List<
                        SpliceFxMotionLayer> { fade, scroll });

                player.EvaluatePreview(0.5f);

                var block = new MaterialPropertyBlock();
                root.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor").a,
                    Is.EqualTo(0.5f).Within(0.01f));
                var textureTransform =
                    block.GetVector("_BaseMap_ST");
                Assert.That(textureTransform.z,
                    Is.EqualTo(0.125f).Within(0.001f));
                Assert.That(textureTransform.w,
                    Is.EqualTo(0.25f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void VisualLayerQualityGate_TracksPreviewTier()
        {
            var root = new GameObject("Quality Gate");
            var target = new GameObject("Visuals");
            target.transform.SetParent(root.transform);
            try
            {
                var gate = root.AddComponent<SpliceFxQualityGate>();
                gate.Configure(SpliceFxQualityMask.Low, target);

                gate.Evaluate(SpliceFxQualityTier.High);
                Assert.That(target.activeSelf, Is.False);
                gate.Evaluate(SpliceFxQualityTier.Low);
                Assert.That(target.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_AcceptsConfiguredTrailAndParticleLayers()
        {
            var template = GameObject.CreatePrimitive(
                PrimitiveType.Quad);
            var shader = Shader.Find(
                "Splice/FX Studio/Gradient Stroke Card");
            var material = new Material(shader);
            template.GetComponent<Renderer>().sharedMaterial = material;
            var definition =
                ScriptableObject.CreateInstance<
                    SpliceFxSubEffectDefinition>();
            var preset =
                ScriptableObject.CreateInstance<
                    SpliceFxPresetDefinition>();
            try
            {
                definition.subFxId = "valid-layer-test";
                preset.templatePrefab = template;
                definition.preset = preset;
                definition.templateOverride = template;
                var trail =
                    SpliceFxVisualLayerDefinition.CreateTrail();
                trail.texture = Texture2D.whiteTexture;
                var particle =
                    SpliceFxVisualLayerDefinition.CreateParticle();
                particle.texture = Texture2D.whiteTexture;
                definition.visualLayers.Add(trail);
                definition.visualLayers.Add(particle);
                var result = new SpliceFxValidationResult();

                SpliceFxValidator.ValidateSubFx(definition, result);

                Assert.That(result.IsValid, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(material);
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
        public void MotionPlayer_PulseCompletesCyclesWithinDuration()
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
                pulse.durationSeconds = 2f;
                definition.motions.Add(pulse);
                var player = root.AddComponent<SpliceFxMotionPlayer>();
                player.Configure(definition);

                player.EvaluatePreview(0.5f);

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
        public void MotionPlayer_SpinCompletesAngleWithinDuration()
        {
            var root = new GameObject("Spin");
            var definition =
                ScriptableObject.CreateInstance<SpliceFxSubEffectDefinition>();
            try
            {
                var spin = SpliceFxMotionLayer.Create(
                    SpliceFxMotionType.Spin);
                spin.speed = 360f;
                spin.durationSeconds = 2f;
                spin.loop = false;
                definition.motions.Add(spin);
                var player = root.AddComponent<SpliceFxMotionPlayer>();
                player.Configure(definition);

                player.EvaluatePreview(1f);

                Assert.That(Quaternion.Angle(
                        Quaternion.identity,
                        root.transform.localRotation),
                    Is.EqualTo(180f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void StudioSplitter_ClampsBothPaneMinimumWidths()
        {
            Assert.That(
                SpliceFxStudioWindow.ClampSettingsPaneWidth(
                    100f, 1040f),
                Is.EqualTo(420f));
            Assert.That(
                SpliceFxStudioWindow.ClampSettingsPaneWidth(
                    1000f, 1040f),
                Is.EqualTo(716f));
        }

        [Test]
        public void MotionTimingSummary_ExplainsSpinRate()
        {
            Assert.That(
                SpliceFxStudioWindow.MotionTimingSummary(
                    SpliceFxMotionType.Spin, 360f, 2f),
                Does.Contain("180"));
        }

        [Test]
        public void PreviewDuration_IncludesLongestMotionCycle()
        {
            var definition =
                ScriptableObject.CreateInstance<
                    SpliceFxSubEffectDefinition>();
            try
            {
                definition.lifetime = 1f;
                var spin = SpliceFxMotionLayer.Create(
                    SpliceFxMotionType.Spin);
                spin.delaySeconds = 0.5f;
                spin.durationSeconds = 4f;
                definition.motions.Add(spin);

                Assert.That(
                    SpliceFxPreviewViewport
                        .CalculateSubFxPreviewDuration(definition),
                    Is.EqualTo(4.5f).Within(0.001f));
            }
            finally
            {
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
        public void InstanceCardMaterial_IsConfiguredTwoSided()
        {
            var shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                SpliceFxStarterLibrary.ConfigureTwoSided(material);

                Assert.That(material.doubleSidedGI, Is.True);
                if (material.HasProperty("_Cull"))
                    Assert.That(material.GetFloat("_Cull"),
                        Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ManualPreviewEditing_AppliesMoveRotateAndScaleDeltas()
        {
            var root = new GameObject("Manual Preview Instance");
            var manual = new SpliceFxManualInstance
            {
                localPosition = new Vector3(1f, 0f, 0f),
                localEulerAngles = new Vector3(0f, 10f, 0f),
                localScale = new Vector3(2f, 2f, 2f)
            };
            try
            {
                root.transform.localPosition =
                    new Vector3(1.5f, 0f, 0f);
                root.transform.localRotation =
                    Quaternion.Euler(0f, 25f, 0f);
                root.transform.localScale =
                    new Vector3(4f, 2f, 2f);

                SpliceFxPreviewViewport.ApplyManualPosition(
                    root.transform, manual, new Vector3(3f, 1f, 0f));
                SpliceFxPreviewViewport.ApplyManualRotation(
                    root.transform, manual, new Vector3(0f, 40f, 0f));
                SpliceFxPreviewViewport.ApplyManualScale(
                    root.transform, manual, new Vector3(1f, 3f, 4f));

                Assert.That(root.transform.localPosition,
                    Is.EqualTo(new Vector3(3.5f, 1f, 0f)));
                Assert.That(Quaternion.Angle(
                        root.transform.localRotation,
                        Quaternion.Euler(0f, 55f, 0f)),
                    Is.LessThan(0.01f));
                Assert.That(root.transform.localScale,
                    Is.EqualTo(new Vector3(2f, 3f, 4f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
