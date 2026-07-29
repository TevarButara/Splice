using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace Splice.FxStudio.Editor
{
    internal enum SpliceFxPreviewSourceKind
    {
        None,
        Preset,
        SubFx,
        Blend,
        SkillStage
    }

    internal sealed class SpliceFxPreviewViewport : IDisposable
    {
        private readonly Action requestRepaint;
        private readonly List<Material> transientMaterials = new();
        private PreviewRenderUtility utility;
        private GameObject previewRoot;
        private GameObject contentRoot;
        private Object source;
        private SpliceFxPreviewSourceKind sourceKind;
        private int stageIndex = -1;
        private int sourceSignature;
        private float duration = 1f;
        private float previewTime;
        private double lastTick;
        private bool playing = true;
        private bool showHeroReference;
        private SpliceFxQualityTier quality = SpliceFxQualityTier.High;
        private Color background = new(0.025f, 0.035f, 0.055f, 1f);
        private Vector2 cameraAngles = new(28f, -32f);
        private Vector3 cameraPivot = new(0f, 0.7f, 0f);
        private float cameraDistance = 6f;

        public SpliceFxPreviewViewport(Action repaint)
        {
            requestRepaint = repaint;
            EditorApplication.update += Tick;
        }

        public void Dispose()
        {
            EditorApplication.update -= Tick;
            Cleanup();
        }

        public void SetSource(Object value,
            SpliceFxPreviewSourceKind kind, int selectedStage = -1)
        {
            var signature = ComputeSignature(value, kind, selectedStage);
            if (source == value && sourceKind == kind &&
                stageIndex == selectedStage &&
                sourceSignature == signature)
                return;

            source = value;
            sourceKind = kind;
            stageIndex = selectedStage;
            sourceSignature = signature;
            previewTime = 0f;
            Rebuild();
        }

        public void Draw(float width = 410f)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox, GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField("LIVE PREVIEW",
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 13,
                        normal =
                        {
                            textColor = new Color(0.25f, 0.82f, 1f)
                        }
                    });
                DrawToolbar();

                var rect = GUILayoutUtility.GetRect(
                    width - 18f, 350f, GUILayout.ExpandWidth(true));
                DrawPreview(rect);
                HandleCamera(rect);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(
                        "Drag: orbit  •  Wheel: zoom",
                        EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Focus", GUILayout.Width(58f)))
                        FocusContent();
                }

                var next = EditorGUILayout.Slider(
                    "Time", previewTime, 0f, Mathf.Max(0.05f, duration));
                if (!Mathf.Approximately(next, previewTime))
                {
                    previewTime = next;
                    playing = false;
                    requestRepaint?.Invoke();
                }

                DrawSourceInfo();
            }
        }

        public void Replay()
        {
            previewTime = 0f;
            playing = true;
            RestartVisuals();
            requestRepaint?.Invoke();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "Pause" : "Play",
                        GUILayout.Width(58f)))
                {
                    playing = !playing;
                    lastTick = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("Replay", GUILayout.Width(58f)))
                    Replay();
                var nextQuality =
                    (SpliceFxQualityTier)EditorGUILayout.EnumPopup(
                        quality, GUILayout.Width(80f));
                if (nextQuality != quality)
                {
                    quality = nextQuality;
                    requestRepaint?.Invoke();
                }
                var nextReference = GUILayout.Toggle(
                    showHeroReference, "Hero Scale", "Button",
                    GUILayout.Width(82f));
                if (nextReference != showHeroReference)
                {
                    showHeroReference = nextReference;
                    Rebuild();
                }
                background = EditorGUILayout.ColorField(
                    GUIContent.none, background, false, false, false,
                    GUILayout.Width(42f));
            }
        }

        private void DrawPreview(Rect rect)
        {
            EnsureUtility();
            if (Event.current.type != EventType.Repaint)
            {
                GUI.Box(rect, GUIContent.none);
                return;
            }

            utility.camera.backgroundColor = background;
            UpdateCamera();
            EvaluateAt(previewTime);
            utility.BeginPreview(rect, GUIStyle.none);
            try
            {
                utility.Render(true);
            }
            finally
            {
                utility.EndAndDrawPreview(rect);
            }

            if (contentRoot == null)
            {
                var labelRect = new Rect(
                    rect.x + 24f, rect.center.y - 20f,
                    rect.width - 48f, 40f);
                GUI.Label(labelRect,
                    "Select an asset to preview",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        fontSize = 13
                    });
            }
        }

        private void DrawSourceInfo()
        {
            var label = source != null ? source.name : "Nothing selected";
            var stage = string.Empty;
            if (sourceKind == SpliceFxPreviewSourceKind.SkillStage &&
                source is SpliceFxSkillPackage package &&
                stageIndex >= 0 && stageIndex < package.stages.Count)
            {
                var binding = package.stages[stageIndex];
                stage = binding != null
                    ? $" • {binding.stage} • {binding.placement} • {binding.scaleMode}"
                    : " • Empty stage";
            }
            EditorGUILayout.HelpBox(
                $"{label}{stage}\nDuration {duration:0.00}s • " +
                $"{quality} quality • preview only (Scene is not modified)",
                MessageType.None);
        }

        private void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            if (lastTick <= 0d) lastTick = now;
            var delta = Mathf.Min(0.1f, (float)(now - lastTick));
            lastTick = now;
            if (!playing || previewRoot == null) return;

            previewTime += delta;
            if (previewTime > Mathf.Max(0.05f, duration))
            {
                previewTime = 0f;
                RestartVisuals();
            }
            requestRepaint?.Invoke();
        }

        private void Rebuild()
        {
            Cleanup();
            EnsureUtility();

            previewRoot = new GameObject("[Splice FX Preview Stage]");
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
            CreateEnvironment(previewRoot.transform);
            contentRoot = BuildContent(previewRoot.transform);
            SetHideFlagsRecursive(previewRoot);
            utility.AddSingleGO(previewRoot);
            FocusContent();
            RestartVisuals();
            requestRepaint?.Invoke();
        }

        private GameObject BuildContent(Transform parent)
        {
            GameObject result = sourceKind switch
            {
                SpliceFxPreviewSourceKind.Preset =>
                    BuildPreset(source as SpliceFxPresetDefinition),
                SpliceFxPreviewSourceKind.SubFx =>
                    BuildSubFx(source as SpliceFxSubEffectDefinition),
                SpliceFxPreviewSourceKind.Blend =>
                    BuildBlend(source as SpliceFxBlendSequence),
                SpliceFxPreviewSourceKind.SkillStage =>
                    BuildSkillStage(source as SpliceFxSkillPackage,
                        stageIndex),
                _ => null
            };
            if (result == null)
            {
                duration = 1f;
                return null;
            }
            result.name = "[Preview Content]";
            result.transform.SetParent(parent, false);
            result.SetActive(true);
            return result;
        }

        private GameObject BuildPreset(SpliceFxPresetDefinition preset)
        {
            duration = 2f;
            return preset?.templatePrefab != null
                ? Object.Instantiate(preset.templatePrefab)
                : null;
        }

        private GameObject BuildSubFx(SpliceFxSubEffectDefinition value)
        {
            if (value?.EffectiveTemplate == null) return null;
            duration = Mathf.Max(0.05f, value.lifetime);
            var result = Object.Instantiate(value.EffectiveTemplate);
            var driver = result.GetComponent<SpliceFxPropertyDriver>() ??
                         result.AddComponent<SpliceFxPropertyDriver>();
            driver.Configure(value);
            var motion = result.GetComponent<SpliceFxMotionPlayer>() ??
                         result.AddComponent<SpliceFxMotionPlayer>();
            motion.Configure(value);
            var allowed =
                (value.quality & SpliceFxQuality.MaskFor(quality)) != 0;
            result.SetActive(allowed);
            return result;
        }

        private GameObject BuildBlend(SpliceFxBlendSequence value)
        {
            if (value == null) return null;
            duration = Mathf.Max(0.05f, value.DurationSeconds);
            var result = new GameObject("Preview Blend");
            var layers = new List<SpliceFxRuntimeLayer>();
            foreach (var clip in value.clips)
            {
                if (clip?.subFx?.EffectiveTemplate == null) continue;
                var visual = BuildSubFx(clip.subFx);
                if (visual == null) continue;
                visual.transform.SetParent(result.transform, false);
                visual.transform.localPosition = clip.localPosition;
                visual.transform.localRotation =
                    Quaternion.Euler(clip.localEulerAngles);
                visual.transform.localScale = SanitizeScale(clip.localScale);
                foreach (var motion in visual.GetComponentsInChildren<
                             SpliceFxMotionPlayer>(true))
                    motion.CaptureCurrentAsBase();
                visual.SetActive(false);
                layers.Add(new SpliceFxRuntimeLayer
                {
                    label = clip.label,
                    visual = visual,
                    startSeconds = Mathf.Max(0f, clip.startSeconds),
                    durationSeconds =
                        Mathf.Max(0.01f, clip.durationSeconds),
                    quality = clip.quality,
                    loop = clip.loop
                });
            }
            var runtime = result.AddComponent<SpliceFxSequenceRuntime>();
            runtime.ConfigureEditor(layers, duration);
            return result;
        }

        private GameObject BuildSkillStage(SpliceFxSkillPackage package,
            int selectedStage)
        {
            if (package == null || selectedStage < 0 ||
                selectedStage >= package.stages.Count)
                return null;
            var binding = package.stages[selectedStage];
            if (binding?.sequence == null) return null;
            var result = BuildBlend(binding.sequence);
            if (result == null) return null;
            result.transform.localPosition = binding.localOffset;
            result.transform.localScale =
                PreviewScale(binding.scaleMode);
            duration += Mathf.Max(0f, binding.delaySeconds);
            return result;
        }

        private void EvaluateAt(float time)
        {
            if (contentRoot == null) return;
            var runtimes = contentRoot.GetComponentsInChildren<
                SpliceFxSequenceRuntime>(true);
            if (runtimes.Length > 0)
            {
                foreach (var runtime in runtimes)
                    runtime.EvaluatePreview(time, quality);
                return;
            }
            SimulateVisual(contentRoot, time);
        }

        private static void SimulateVisual(GameObject root, float time)
        {
            foreach (var motion in
                     root.GetComponentsInChildren<SpliceFxMotionPlayer>(
                         true))
                motion.EvaluatePreview(time);
            foreach (var particle in
                     root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(time, true, true, true);
                particle.Pause(true);
            }
            foreach (var visual in
                     root.GetComponentsInChildren<VisualEffect>(true))
            {
                visual.Reinit();
                if (time > 0f) visual.Simulate(time, 1);
                visual.pause = true;
            }
        }

        private void RestartVisuals()
        {
            if (contentRoot == null) return;
            foreach (var motion in contentRoot.GetComponentsInChildren<
                         SpliceFxMotionPlayer>(true))
                motion.RestartMotion();
            foreach (var runtime in contentRoot.GetComponentsInChildren<
                         SpliceFxSequenceRuntime>(true))
                runtime.RestartSequence();
            foreach (var particle in contentRoot.GetComponentsInChildren<
                         ParticleSystem>(true))
            {
                particle.Stop(true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }
            foreach (var visual in contentRoot.GetComponentsInChildren<
                         VisualEffect>(true))
            {
                visual.pause = false;
                visual.Reinit();
                visual.Play();
            }
        }

        private void EnsureUtility()
        {
            if (utility != null) return;
            utility = new PreviewRenderUtility();
            utility.camera.fieldOfView = 42f;
            utility.camera.nearClipPlane = 0.03f;
            utility.camera.farClipPlane = 200f;
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.ambientColor = new Color(0.22f, 0.24f, 0.3f);
            utility.lights[0].intensity = 1.1f;
            utility.lights[0].transform.rotation =
                Quaternion.Euler(35f, -35f, 0f);
            if (utility.lights.Length > 1)
            {
                utility.lights[1].intensity = 0.65f;
                utility.lights[1].color = new Color(0.3f, 0.55f, 1f);
                utility.lights[1].transform.rotation =
                    Quaternion.Euler(-20f, 145f, 0f);
            }
        }

        private void Cleanup()
        {
            if (utility != null)
            {
                utility.Cleanup();
                utility = null;
            }
            if (previewRoot != null)
                Object.DestroyImmediate(previewRoot);
            previewRoot = null;
            contentRoot = null;
            foreach (var material in transientMaterials)
                if (material != null)
                    Object.DestroyImmediate(material);
            transientMaterials.Clear();
        }

        private void CreateEnvironment(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Preview Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = Vector3.one * 0.8f;
            var collider = ground.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            ground.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial(new Color(0.055f, 0.075f, 0.105f), false);

            var gridMaterial =
                CreateMaterial(new Color(0.14f, 0.45f, 0.62f, 0.45f), true);
            for (var i = -4; i <= 4; i++)
            {
                CreateGridLine(parent, gridMaterial,
                    new Vector3(i, 0.012f, -4f),
                    new Vector3(i, 0.012f, 4f));
                CreateGridLine(parent, gridMaterial,
                    new Vector3(-4f, 0.012f, i),
                    new Vector3(4f, 0.012f, i));
            }

            if (!showHeroReference) return;
            CreateHeroReference(parent,
                CreateMaterial(new Color(0.18f, 0.72f, 1f, 0.8f), true));
        }

        private void CreateGridLine(Transform parent, Material material,
            Vector3 from, Vector3 to)
        {
            var lineObject = new GameObject("Grid");
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.012f;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startColor = Color.white;
            line.endColor = Color.white;
        }

        private void CreateHeroReference(Transform parent,
            Material material)
        {
            var root = new GameObject("Hero Scale Reference (2m)");
            root.transform.SetParent(parent, false);
            CreateReferenceLine(root.transform, material,
                new[]
                {
                    new Vector3(0f, 0.1f, 0f),
                    new Vector3(0f, 1.48f, 0f)
                }, false);
            CreateReferenceLine(root.transform, material,
                new[]
                {
                    new Vector3(-0.55f, 1.2f, 0f),
                    new Vector3(0f, 1.4f, 0f),
                    new Vector3(0.55f, 1.2f, 0f)
                }, false);
            CreateReferenceLine(root.transform, material,
                new[]
                {
                    new Vector3(-0.42f, 0f, 0f),
                    new Vector3(0f, 0.72f, 0f),
                    new Vector3(0.42f, 0f, 0f)
                }, false);

            const int segments = 24;
            var head = new Vector3[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                head[i] = new Vector3(
                    Mathf.Cos(angle) * 0.25f,
                    1.72f + Mathf.Sin(angle) * 0.25f,
                    0f);
            }
            CreateReferenceLine(root.transform, material, head, true);
        }

        private static void CreateReferenceLine(Transform parent,
            Material material, IReadOnlyList<Vector3> points, bool loop)
        {
            var lineObject = new GameObject("Hero Wire");
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.018f;
            line.loop = loop;
            line.positionCount = points.Count;
            for (var i = 0; i < points.Count; i++)
                line.SetPosition(i, points[i]);
            line.startColor = Color.white;
            line.endColor = Color.white;
        }

        private Material CreateMaterial(Color color, bool transparent)
        {
            var shader = Shader.Find(
                             "Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = 3000;
            }
            transientMaterials.Add(material);
            return material;
        }

        private void FocusContent()
        {
            if (contentRoot == null)
            {
                cameraPivot = new Vector3(0f, 0.7f, 0f);
                cameraDistance = 6f;
                return;
            }
            var renderers =
                contentRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                cameraPivot = new Vector3(0f, 0.7f, 0f);
                cameraDistance = 5f;
                return;
            }
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            cameraPivot = bounds.center;
            cameraDistance = Mathf.Clamp(
                Mathf.Max(2.5f, bounds.extents.magnitude * 2.6f),
                2.5f, 30f);
        }

        private void UpdateCamera()
        {
            var rotation = Quaternion.Euler(
                cameraAngles.x, cameraAngles.y, 0f);
            utility.camera.transform.position =
                cameraPivot + rotation * Vector3.back * cameraDistance;
            utility.camera.transform.rotation = rotation;
        }

        private void HandleCamera(Rect rect)
        {
            var current = Event.current;
            if (!rect.Contains(current.mousePosition)) return;
            if (current.type == EventType.MouseDrag &&
                current.button == 0)
            {
                cameraAngles.y += current.delta.x * 0.45f;
                cameraAngles.x =
                    Mathf.Clamp(cameraAngles.x - current.delta.y * 0.45f,
                        -10f, 82f);
                current.Use();
                requestRepaint?.Invoke();
            }
            else if (current.type == EventType.ScrollWheel)
            {
                cameraDistance = Mathf.Clamp(
                    cameraDistance * (1f + current.delta.y * 0.06f),
                    0.5f, 50f);
                current.Use();
                requestRepaint?.Invoke();
            }
        }

        private static int ComputeSignature(Object value,
            SpliceFxPreviewSourceKind kind, int selectedStage)
        {
            unchecked
            {
                var hash = value != null
                    ? GlobalObjectId.GetGlobalObjectIdSlow(value)
                          .GetHashCode() * 397 ^
                      EditorUtility.GetDirtyCount(value)
                    : 0;
                hash = hash * 397 ^ (int)kind;
                hash = hash * 397 ^ selectedStage;
                if (value is SpliceFxSubEffectDefinition sub)
                    return CombineSubFx(hash, sub);
                if (value is SpliceFxBlendSequence blend)
                    return CombineBlend(hash, blend);
                if (value is SpliceFxSkillPackage package &&
                    selectedStage >= 0 &&
                    selectedStage < package.stages.Count)
                {
                    var sequence = package.stages[selectedStage]?.sequence;
                    return CombineBlend(hash, sequence);
                }
                return hash;
            }
        }

        private static int CombineBlend(int hash,
            SpliceFxBlendSequence blend)
        {
            unchecked
            {
                if (blend == null) return hash;
                hash = hash * 397 ^ EditorUtility.GetDirtyCount(blend);
                foreach (var clip in blend.clips)
                    if (clip?.subFx != null)
                        hash = CombineSubFx(hash, clip.subFx);
                return hash;
            }
        }

        private static int CombineSubFx(int hash,
            SpliceFxSubEffectDefinition sub)
        {
            unchecked
            {
                if (sub == null) return hash;
                hash = hash * 397 ^ EditorUtility.GetDirtyCount(sub);
                if (sub.EffectiveTemplate != null)
                    hash = hash * 397 ^
                           EditorUtility.GetDirtyCount(
                               sub.EffectiveTemplate);
                if (sub.EffectiveTexture != null)
                    hash = hash * 397 ^
                           EditorUtility.GetDirtyCount(
                               sub.EffectiveTexture);
                return hash;
            }
        }

        private static void SetHideFlagsRecursive(GameObject root)
        {
            foreach (var transform in
                     root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.hideFlags =
                    HideFlags.HideAndDontSave;
        }

        private static Vector3 SanitizeScale(Vector3 scale) =>
            new(Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                Mathf.Max(0.001f, Mathf.Abs(scale.z)));

        private static Vector3 PreviewScale(SpliceFxScaleMode mode) =>
            mode switch
            {
                SpliceFxScaleMode.AbilityCastRange => Vector3.one * 3f,
                SpliceFxScaleMode.AbilityEffectRadius =>
                    Vector3.one * 2f,
                _ => Vector3.one
            };
    }
}
