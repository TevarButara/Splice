using System;
using System.Collections.Generic;
using UnityEngine;

namespace Splice.FxStudio
{
    [Serializable]
    public sealed class SpliceFxSequenceClip
    {
        public string label = "Layer";
        public SpliceFxSubEffectDefinition subFx;
        [Min(0f)] public float startSeconds;
        [Min(0.01f)] public float durationSeconds = 1f;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public SpliceFxQualityMask quality = SpliceFxQualityMask.All;
        public bool loop;

        public float EndSeconds =>
            Mathf.Max(0f, startSeconds) +
            Mathf.Max(0.01f, durationSeconds);
    }

    [CreateAssetMenu(fileName = "FxBlend",
        menuName = "Splice/FX Studio/Blend Sequence")]
    public sealed class SpliceFxBlendSequence : ScriptableObject
    {
        public string sequenceId = "new_sequence";
        public string displayName = "New Blend Sequence";
        [Min(1)] public int schemaVersion = 1;
        [Min(0f)] public float tailSeconds = 0.1f;
        public List<SpliceFxSequenceClip> clips = new();

        public float DurationSeconds
        {
            get
            {
                var duration = 0f;
                foreach (var clip in clips)
                    if (clip != null)
                        duration = Mathf.Max(duration, clip.EndSeconds);
                return duration + Mathf.Max(0f, tailSeconds);
            }
        }

        private void OnValidate()
        {
            sequenceId = SpliceFxPresetDefinition.SanitizeId(sequenceId);
            schemaVersion = Mathf.Max(1, schemaVersion);
        }
    }
}
