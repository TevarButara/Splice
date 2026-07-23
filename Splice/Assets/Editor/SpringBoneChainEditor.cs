using System.Collections.Generic;
using Splice.Characters;
using UnityEditor;
using UnityEngine;

namespace Splice.EditorTools
{
    // Custom inspector + Scene handles ให้ SpringBoneChain:
    //  • โชว์เส้นสายกระดูก (chain) ที่จะสปริง — เห็นว่า auto-find เจอสายไหนบ้าง
    //  • ลากย้าย + ปรับรัศมี collider (กันผม/cape ทะลุตัว) ได้ในจอ Scene ตรงๆ
    //  • ปุ่มสร้าง collider เป็น child object ให้พร้อมใช้
    [CustomEditor(typeof(SpringBoneChain))]
    public class SpringBoneChainEditor : UnityEditor.Editor
    {
        private bool showChains = true;
        private bool editColliders = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var chain = (SpringBoneChain)target;
            serializedObject.Update();
            var collidersProp = serializedObject.FindProperty("colliders");

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Spring Bone Tools", EditorStyles.boldLabel);
                showChains = EditorGUILayout.ToggleLeft("โชว์เส้นสาย (chain) ในจอ Scene", showChains);
                editColliders = EditorGUILayout.ToggleLeft("แก้ collider ในจอ Scene (ลากย้าย/ปรับรัศมี)", editColliders);

                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("➕ Add Collider (สร้าง child)"))
                        AddColliderChild(chain, collidersProp);
                    if (GUILayout.Button("➕ Slot ว่าง"))
                    {
                        collidersProp.arraySize++;
                        serializedObject.ApplyModifiedProperties();
                    }
                }

                var roots = GetRoots(chain);
                EditorGUILayout.LabelField(roots.Count > 0
                    ? $"เจอสายที่จะสปริง: {roots.Count} สาย"
                    : "ยังไม่เจอสาย — ลาก Roots เอง หรือให้กระดูกชื่อขึ้นต้นตาม Name Prefix",
                    EditorStyles.miniLabel);

                EditorGUILayout.HelpBox(
                    "• Roots ว่าง + Auto Find เปิด = หาสายจากชื่อ spring_ ให้เอง\n" +
                    "• ปรับ stiffness/damping/gravity แล้วกด Play ดูผล\n" +
                    "• Collider (หัว/ลำตัว) กันผม/cape ทะลุตัว — ลากลูกศรย้าย, ลากจุดวงนอกปรับรัศมี",
                    MessageType.Info);
            }
        }

        private void AddColliderChild(SpringBoneChain chain, SerializedProperty collidersProp)
        {
            var go = new GameObject("SpringCollider");
            Undo.RegisterCreatedObjectUndo(go, "Add Spring Collider");
            go.transform.SetParent(chain.transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);   // ~กลางลำตัวโดยประมาณ (ปรับต่อในจอได้)

            var i = collidersProp.arraySize;
            collidersProp.arraySize++;
            var el = collidersProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("center").objectReferenceValue = go.transform;
            el.FindPropertyRelative("radius").floatValue = 0.15f;
            serializedObject.ApplyModifiedProperties();

            Selection.activeGameObject = chain.gameObject;   // คง chain ไว้ให้ handle โชว์ต่อ
        }

        private void OnSceneGUI()
        {
            var chain = (SpringBoneChain)target;
            serializedObject.Update();
            if (showChains) DrawChains(chain);
            if (editColliders) EditColliders();
        }

        // ---------------- chains ----------------

        private void DrawChains(SpringBoneChain chain)
        {
            Handles.color = new Color(1f, 0.8f, 0.2f, 1f);
            foreach (var root in GetRoots(chain))
                DrawChain(root);
        }

        private void DrawChain(Transform bone)
        {
            if (bone == null || bone.childCount == 0) return;
            var child = bone.GetChild(0);
            Handles.DrawLine(bone.position, child.position, 3f);
            Handles.SphereHandleCap(0, bone.position, Quaternion.identity,
                HandleUtility.GetHandleSize(bone.position) * 0.06f, EventType.Repaint);
            DrawChain(child);
        }

        private List<Transform> GetRoots(SpringBoneChain chain)
        {
            var list = new List<Transform>();
            var rootsProp = serializedObject.FindProperty("roots");
            for (var i = 0; i < rootsProp.arraySize; i++)
            {
                if (rootsProp.GetArrayElementAtIndex(i).objectReferenceValue is Transform t) list.Add(t);
            }
            if (list.Count > 0) return list;

            // auto-find จากชื่อ (จำลอง logic runtime เพื่อ preview ตอน edit)
            var autoProp = serializedObject.FindProperty("autoFindByName");
            var prefixProp = serializedObject.FindProperty("namePrefix");
            if (autoProp.boolValue && !string.IsNullOrEmpty(prefixProp.stringValue))
            {
                var prefix = prefixProp.stringValue;
                foreach (var t in chain.GetComponentsInChildren<Transform>(true))
                {
                    if (t == chain.transform || !t.name.StartsWith(prefix)) continue;
                    if (t.parent != null && t.parent.name.StartsWith(prefix)) continue;
                    list.Add(t);
                }
            }
            return list;
        }

        // ---------------- colliders ----------------

        private void EditColliders()
        {
            var collidersProp = serializedObject.FindProperty("colliders");
            var boneRadiusProp = serializedObject.FindProperty("boneRadius");
            var boneR = boneRadiusProp != null ? boneRadiusProp.floatValue : 0f;

            for (var i = 0; i < collidersProp.arraySize; i++)
            {
                var el = collidersProp.GetArrayElementAtIndex(i);
                var radiusProp = el.FindPropertyRelative("radius");
                if (!(el.FindPropertyRelative("center").objectReferenceValue is Transform center)) continue;

                var pos = center.position;
                var r = radiusProp.floatValue;

                // วงจริง (radius) + วงนอก (radius+boneRadius = ระยะที่สายจะถูกดันออก)
                Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
                Handles.DrawWireDisc(pos, Vector3.up, r);
                Handles.DrawWireDisc(pos, Vector3.right, r);
                Handles.DrawWireDisc(pos, Vector3.forward, r);
                if (boneR > 0f)
                {
                    Handles.color = new Color(0.2f, 0.8f, 1f, 0.25f);
                    Handles.DrawWireDisc(pos, Vector3.up, r + boneR);
                    Handles.DrawWireDisc(pos, Vector3.right, r + boneR);
                    Handles.DrawWireDisc(pos, Vector3.forward, r + boneR);
                }

                // ลากย้าย center (transform)
                EditorGUI.BeginChangeCheck();
                var size = HandleUtility.GetHandleSize(pos) * 0.12f;
                var np = Handles.FreeMoveHandle(pos, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(center, "Move Spring Collider");
                    center.position = np;
                }

                // ปรับรัศมี
                EditorGUI.BeginChangeCheck();
                var nr = Handles.RadiusHandle(Quaternion.identity, pos, r);
                if (EditorGUI.EndChangeCheck())
                {
                    radiusProp.floatValue = Mathf.Max(0f, nr);
                    serializedObject.ApplyModifiedProperties();
                }

                Handles.color = Color.white;
                Handles.Label(pos + Vector3.up * (r + 0.05f), $"collider {i}");
            }
        }
    }
}
