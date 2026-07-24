#if UNITY_EDITOR
using Splice.Data;
using UnityEditor;
using UnityEngine;

namespace Splice.Editor.Data
{
    [CustomEditor(typeof(HeroAnimSetSO))]
    public sealed class HeroAnimSetSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (!GUILayout.Button("Add / Fill Standard Hero States")) return;

            var set = (HeroAnimSetSO)target;
            Undo.RecordObject(set, "Fill Hero Animation States");
            Fill(ref set.attack1, "Attack");
            Fill(ref set.attack2, "Attack");
            Fill(ref set.idle, "Idle");
            Fill(ref set.death, "Death");
            Fill(ref set.win, "Win");
            Fill(ref set.lose, "Lose");
            Fill(ref set.dance, "Dance");
            Fill(ref set.landing, "Landing");
            Fill(ref set.sprint, "Sprint");
            Fill(ref set.walk, "Walk");
            Fill(ref set.skill1, "Skill1");
            Fill(ref set.skill2, "Skill2");
            Fill(ref set.skill3, "Skill3");
            EditorUtility.SetDirty(set);
        }

        private static void Fill(ref string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) value = fallback;
        }
    }
}
#endif
