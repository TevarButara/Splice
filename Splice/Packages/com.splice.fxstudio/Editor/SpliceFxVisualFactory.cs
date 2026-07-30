using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Splice.FxStudio.Editor
{
    internal static class SpliceFxVisualFactory
    {
        public static GameObject Build(
            SpliceFxSubEffectDefinition subFx)
        {
            if (subFx == null)
                throw new ArgumentNullException(nameof(subFx));
            if (subFx.EffectiveTemplate == null)
                throw new InvalidOperationException(
                    $"SubFX '{subFx.name}' has no effective template.");
            if (subFx.instanceLayout?.MaximumCount > 64)
                throw new InvalidOperationException(
                    $"SubFX '{subFx.name}' requests " +
                    $"{subFx.instanceLayout.MaximumCount} instances; " +
                    "the supported maximum is 64.");

            var root = new GameObject(
                $"SubFX_{SpliceFxPresetDefinition.SanitizeId(subFx.subFxId)}");
            var transforms = new List<Transform>();
            var enabledStates = new List<bool>();
            var poses = SpliceFxInstanceLayoutSolver.Build(
                subFx.instanceLayout);
            if (poses.Count == 0)
                poses.Add(new SpliceFxInstancePose(
                    Vector3.zero, Quaternion.identity, Vector3.one));

            for (var i = 0; i < poses.Count; i++)
            {
                var clone = PrefabUtility.InstantiatePrefab(
                    subFx.EffectiveTemplate) as GameObject;
                if (clone == null)
                    clone = Object.Instantiate(
                        subFx.EffectiveTemplate);
                var authoredPosition = clone.transform.localPosition;
                var authoredRotation = clone.transform.localRotation;
                var authoredScale = clone.transform.localScale;
                clone.name = $"Instance_{i + 1:00}";
                clone.transform.SetParent(root.transform, false);
                var pose = poses[i];
                clone.transform.localPosition =
                    authoredPosition + pose.Position;
                clone.transform.localRotation =
                    pose.Rotation * authoredRotation;
                clone.transform.localScale = Vector3.Scale(
                    authoredScale, pose.Scale);
                clone.SetActive(pose.Enabled);
                transforms.Add(clone.transform);
                enabledStates.Add(pose.Enabled);
                if (subFx.instanceLayout?.motionScope ==
                    SpliceFxInstanceMotionScope.EachInstance)
                {
                    var itemMotion =
                        clone.GetComponent<SpliceFxMotionPlayer>() ??
                        clone.AddComponent<SpliceFxMotionPlayer>();
                    itemMotion.Configure(subFx);
                }
            }

            var driver = root.AddComponent<SpliceFxPropertyDriver>();
            driver.Configure(subFx);
            if (subFx.instanceLayout?.motionScope !=
                SpliceFxInstanceMotionScope.EachInstance)
            {
                var motion = root.AddComponent<SpliceFxMotionPlayer>();
                motion.Configure(subFx);
            }
            var group = root.AddComponent<SpliceFxInstanceGroup>();
            group.ConfigureEditor(subFx, transforms, enabledStates);
            root.SetActive(true);
            return root;
        }
    }
}
