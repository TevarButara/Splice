using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Splice.EditorTools
{
    /// <summary>
    /// หน้าต่างรวมเครื่องมือ dev ของโปรเจกต์ Splice — เปิดจาก Tools ▸ Splice ▸ Splice Utility
    /// sidebar ซ้าย = รายชื่อ tool (auto-discovery), ขวา = UI ของ tool ที่เลือก
    /// เพิ่ม tool ใหม่ = สร้าง class implement ISpliceUtilityTool (ดู UniSplitterTool เป็นตัวอย่าง)
    /// </summary>
    public class SpliceUtilityWindow : EditorWindow
    {
        const string SelectedPrefKey = "Splice.Utility.SelectedTool";
        const float SidebarWidth = 148f;

        readonly List<ISpliceUtilityTool> _tools = new();
        int _selected;
        Vector2 _contentScroll;

        [MenuItem("Tools/Splice/Splice Utility")]
        public static void Open()
        {
            var win = GetWindow<SpliceUtilityWindow>("Splice Utility");
            win.minSize = new Vector2(560, 420);
            win.Show();
        }

        void OnEnable()
        {
            BuildTools();
            var lastTitle = EditorPrefs.GetString(SelectedPrefKey, "");
            var idx = _tools.FindIndex(t => t.Title == lastTitle);
            _selected = Mathf.Clamp(idx < 0 ? 0 : idx, 0, Mathf.Max(0, _tools.Count - 1));
            if (_tools.Count > 0) _tools[_selected].OnEnable();
        }

        void OnDisable()
        {
            if (_tools.Count > 0 && _selected >= 0 && _selected < _tools.Count)
                _tools[_selected].OnDisable();
        }

        void BuildTools()
        {
            _tools.Clear();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ISpliceUtilityTool>())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericType) continue;
                if (type.GetConstructor(System.Type.EmptyTypes) == null) continue;
                if (System.Activator.CreateInstance(type) is ISpliceUtilityTool tool)
                    _tools.Add(tool);
            }
            _tools.Sort((a, b) => a.Order != b.Order
                ? a.Order.CompareTo(b.Order)
                : string.CompareOrdinal(a.Title, b.Title));
        }

        void Select(int index)
        {
            if (index == _selected) return;
            if (_selected >= 0 && _selected < _tools.Count) _tools[_selected].OnDisable();
            _selected = index;
            _tools[_selected].OnEnable();
            EditorPrefs.SetString(SelectedPrefKey, _tools[_selected].Title);
        }

        void OnGUI()
        {
            if (_tools.Count == 0)
            {
                EditorGUILayout.HelpBox("ไม่พบเครื่องมือ (ISpliceUtilityTool)", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidebar();
                DrawContent();
            }
        }

        void DrawSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label("SPLICE UTILITY", EditorStyles.boldLabel);
                GUILayout.Space(4);
                for (int i = 0; i < _tools.Count; i++)
                {
                    var on = i == _selected;
                    var prev = GUI.backgroundColor;
                    if (on) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                    if (GUILayout.Button(_tools[i].Title, GUILayout.Height(26)))
                        Select(i);
                    GUI.backgroundColor = prev;
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("v0.1", EditorStyles.miniLabel);
            }
        }

        void DrawContent()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label(_tools[_selected].Title, EditorStyles.largeLabel);
                DrawSeparator();
                using var scroll = new EditorGUILayout.ScrollViewScope(_contentScroll);
                _contentScroll = scroll.scrollPosition;
                _tools[_selected].OnGUI();
            }
        }

        static void DrawSeparator()
        {
            var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0, 0, 0, 0.25f));
            GUILayout.Space(4);
        }
    }
}
