using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Splice.EditorTools
{
    /// <summary>
    /// UniSplitter — ตัดชีตภาพ (ปุ่ม/ไอคอน) เป็นภาพลูก แบบ pixel เป๊ะ
    /// • Grid auto: กำหนด column/row → สร้างเส้นตัดเว้นเท่ากันอัตโนมัติ
    /// • Manual: ลากเส้นตัดเองได้ (ไม่เท่ากันก็ได้) / เพิ่ม-ลบเส้น
    /// • Split → เขียนภาพลูกด้วย "นามสกุลเดิม" ลง "โฟลเดอร์เดียวกับต้นฉบับ"
    /// grid กับ manual เป็นระบบเดียวกัน: เส้นตัด (vLines/hLines) คือความจริง — grid แค่ช่วย seed
    /// </summary>
    public class UniSplitterTool : ISpliceUtilityTool
    {
        public string Title => "UniSplitter";
        public int Order => 0;

        // ---- state ----
        Texture2D _source;
        int _cols = 2, _rows = 4;
        bool _skipEmpty = true;         // ข้ามช่องที่โปร่งใสทั้งช่อง (ชีตที่มีช่องว่าง)
        bool _copyImportSettings = true; // ก๊อป import settings จากต้นฉบับ (เช่น เป็น Sprite)
        int _jpgQuality = 95;

        // เส้นตัด normalized 0..1 (ไม่รวมขอบ 0 และ 1) — vLines วัดจากซ้าย, hLines วัดจากบน
        readonly List<float> _vLines = new();
        readonly List<float> _hLines = new();

        // drag state
        int _dragKind; // 0=none, 1=vertical, 2=horizontal
        int _dragIndex = -1;

        static Texture2D _checker;

        public void OnEnable() { }
        public void OnDisable() { }

        // ---------------- GUI ----------------
        public void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var newSrc = (Texture2D)EditorGUILayout.ObjectField("Source Sheet", _source, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && newSrc != _source)
            {
                _source = newSrc;
                SeedGrid(); // ตั้งเผ่าเริ่มต้นตาม cols/rows ปัจจุบัน
            }

            if (_source == null)
            {
                EditorGUILayout.HelpBox("ลากไฟล์ชีต (Texture2D) มาวางที่ช่อง Source Sheet\n" +
                                        "รองรับ .png .jpg .jpeg .tga .exr — ภาพลูกจะใช้นามสกุลเดิม", MessageType.Info);
                return;
            }

            DrawGridControls();
            DrawOptions();
            DrawPreview();
            DrawSplitButton();
        }

        void DrawGridControls()
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Grid", GUILayout.Width(34));
                _cols = Mathf.Max(1, EditorGUILayout.IntField("Columns", _cols));
                _rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", _rows));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("↻ จัดเส้นตาม Grid (เว้นเท่ากัน)")) SeedGrid();
                if (GUILayout.Button("＋ เส้นตั้ง")) AddLine(vertical: true);
                if (GUILayout.Button("＋ เส้นนอน")) AddLine(vertical: false);
                if (GUILayout.Button("🗑 ล้างเส้น")) { _vLines.Clear(); _hLines.Clear(); }
            }
            EditorGUILayout.LabelField($"ช่องที่จะได้: {(_vLines.Count + 1)} × {(_hLines.Count + 1)} = " +
                                       $"{(_vLines.Count + 1) * (_hLines.Count + 1)} ภาพ", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("ลากเส้นเพื่อย้าย • คลิกขวาที่เส้นเพื่อลบ", EditorStyles.miniLabel);
        }

        void DrawOptions()
        {
            EditorGUILayout.Space(2);
            _skipEmpty = EditorGUILayout.ToggleLeft("ข้ามช่องที่โปร่งใสทั้งช่อง (skip empty)", _skipEmpty);
            _copyImportSettings = EditorGUILayout.ToggleLeft("ก๊อป Import Settings จากต้นฉบับ (Sprite ฯลฯ)", _copyImportSettings);
            var ext = Path.GetExtension(AssetDatabase.GetAssetPath(_source)).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg")
                _jpgQuality = EditorGUILayout.IntSlider("JPG Quality", _jpgQuality, 1, 100);
        }

        // ---------------- Preview + drag ----------------
        void DrawPreview()
        {
            EditorGUILayout.Space(4);
            var box = GUILayoutUtility.GetRect(64, 440, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var inner = FitRect(box, _source.width, _source.height);

            // checker + ภาพ
            EnsureChecker();
            GUI.DrawTextureWithTexCoords(inner, _checker,
                new Rect(0, 0, inner.width / 16f, inner.height / 16f));
            GUI.DrawTexture(inner, _source, ScaleMode.StretchToFill, true);
            EditorGUI.DrawRect(new Rect(inner.x, inner.y, inner.width, 1), Color.gray);

            HandleMouse(inner);
            DrawLines(inner);
        }

        void DrawLines(Rect r)
        {
            var col = new Color(0.2f, 0.9f, 1f, 0.9f);
            foreach (var x in _vLines)
            {
                var sx = r.x + x * r.width;
                EditorGUI.DrawRect(new Rect(sx - 1, r.y, 2, r.height), col);
            }
            foreach (var y in _hLines)
            {
                var sy = r.y + y * r.height;
                EditorGUI.DrawRect(new Rect(r.x, sy - 1, r.width, 2), col);
            }
        }

        void HandleMouse(Rect r)
        {
            var e = Event.current;
            const float grab = 6f;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!r.Contains(e.mousePosition)) break;
                    // หาเส้นที่อยู่ใกล้เมาส์สุด
                    var (kind, index) = FindNearestLine(r, e.mousePosition, grab);
                    if (kind != 0)
                    {
                        if (e.button == 1) // คลิกขวา = ลบ
                        {
                            (kind == 1 ? _vLines : _hLines).RemoveAt(index);
                            e.Use();
                        }
                        else
                        {
                            _dragKind = kind; _dragIndex = index; e.Use();
                        }
                    }
                    break;

                case EventType.MouseDrag:
                    if (_dragKind != 0 && _dragIndex >= 0)
                    {
                        if (_dragKind == 1)
                            _vLines[_dragIndex] = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width);
                        else
                            _hLines[_dragIndex] = Mathf.Clamp01((e.mousePosition.y - r.y) / r.height);
                        e.Use();
                        GUI.changed = true;
                    }
                    break;

                case EventType.MouseUp:
                    _dragKind = 0; _dragIndex = -1;
                    break;
            }
        }

        (int kind, int index) FindNearestLine(Rect r, Vector2 m, float grab)
        {
            int bestKind = 0, bestIdx = -1; float best = grab;
            for (int i = 0; i < _vLines.Count; i++)
            {
                var d = Mathf.Abs((r.x + _vLines[i] * r.width) - m.x);
                if (d < best) { best = d; bestKind = 1; bestIdx = i; }
            }
            for (int i = 0; i < _hLines.Count; i++)
            {
                var d = Mathf.Abs((r.y + _hLines[i] * r.height) - m.y);
                if (d < best) { best = d; bestKind = 2; bestIdx = i; }
            }
            return (bestKind, bestIdx);
        }

        // ---------------- Split action ----------------
        void DrawSplitButton()
        {
            EditorGUILayout.Space(6);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            using (new EditorGUI.DisabledScope(_source == null))
            {
                if (GUILayout.Button("✂  SPLIT", GUILayout.Height(34)))
                    Split();
            }
            GUI.backgroundColor = prev;
        }

        void Split()
        {
            var path = AssetDatabase.GetAssetPath(_source);
            if (string.IsNullOrEmpty(path)) { EditorUtility.DisplayDialog("UniSplitter", "หา asset path ไม่เจอ", "OK"); return; }

            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();

            var src = GetSourceReadable(path, ext, out var cleanup);
            if (src == null) { EditorUtility.DisplayDialog("UniSplitter", "อ่านไฟล์ต้นฉบับไม่ได้", "OK"); return; }

            try
            {
                int W = src.width, H = src.height;
                var xs = BuildCuts(_vLines, W);   // ซ้าย→ขวา (px)
                var ys = BuildCuts(_hLines, H);   // บน→ล่าง (px, top-down)

                var supportsAlpha = ext != ".jpg" && ext != ".jpeg";
                var written = new List<string>();
                int skipped = 0;

                for (int r = 0; r < ys.Count - 1; r++)
                {
                    int top = ys[r], bottom = ys[r + 1];
                    int ch = bottom - top;
                    if (ch <= 0) continue;
                    int srcY = H - bottom; // แปลง top-down → bottom-up ของ GetPixels

                    for (int c = 0; c < xs.Count - 1; c++)
                    {
                        int x0 = xs[c], x1 = xs[c + 1];
                        int cw = x1 - x0;
                        if (cw <= 0) continue;

                        var block = src.GetPixels(x0, srcY, cw, ch);
                        if (_skipEmpty && supportsAlpha && IsEmpty(block)) { skipped++; continue; }

                        var childExt = ext;
                        var bytes = EncodeCell(block, cw, ch, ext, ref childExt);
                        var outPath = Path.Combine(dir, $"{name}_{r}_{c}{childExt}");
                        outPath = AssetDatabase.GenerateUniqueAssetPath(outPath.Replace('\\', '/'));
                        File.WriteAllBytes(outPath, bytes);
                        written.Add(outPath);
                    }
                }

                AssetDatabase.Refresh();
                if (_copyImportSettings) CopyImportSettings(path, written);

                if (written.Count > 0)
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(written[0]));
                Debug.Log($"[UniSplitter] ตัดสำเร็จ {written.Count} ภาพ" +
                          (skipped > 0 ? $" (ข้ามช่องว่าง {skipped})" : "") + $" → {dir}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UniSplitter] ล้มเหลว: {ex}");
                EditorUtility.DisplayDialog("UniSplitter", "เกิดข้อผิดพลาด — ดู Console", "OK");
            }
            finally
            {
                cleanup?.Invoke();
            }
        }

        // ---------------- helpers ----------------
        void SeedGrid()
        {
            _vLines.Clear(); _hLines.Clear();
            for (int i = 1; i < _cols; i++) _vLines.Add((float)i / _cols);
            for (int j = 1; j < _rows; j++) _hLines.Add((float)j / _rows);
        }

        void AddLine(bool vertical)
        {
            var list = vertical ? _vLines : _hLines;
            list.Add(0.5f);
            list.Sort();
        }

        // เส้นตัด normalized → ตำแหน่ง pixel (รวมขอบ 0 และ size), เรียงและไม่ซ้ำ
        static List<int> BuildCuts(List<float> lines, int size)
        {
            var set = new SortedSet<int> { 0, size };
            foreach (var t in lines) set.Add(Mathf.Clamp(Mathf.RoundToInt(t * size), 0, size));
            return set.ToList();
        }

        static bool IsEmpty(Color[] block)
        {
            for (int i = 0; i < block.Length; i++)
                if (block[i].a > 0.003f) return false;
            return true;
        }

        byte[] EncodeCell(Color[] block, int w, int h, string ext, ref string childExt)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            try
            {
                t.SetPixels(block);
                t.Apply(false, false);
                switch (ext)
                {
                    case ".png": return t.EncodeToPNG();
                    case ".jpg":
                    case ".jpeg": return t.EncodeToJPG(_jpgQuality);
                    case ".tga": return t.EncodeToTGA();
                    case ".exr": return t.EncodeToEXR();
                    default: childExt = ".png"; return t.EncodeToPNG(); // นามสกุลแปลก → เซฟเป็น png
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        // ได้ Texture2D ที่ "อ่าน pixel ได้ + เต็มความละเอียดต้นฉบับ"
        // png/jpg → โหลด byte ตรงจากไฟล์ (เป๊ะสุด) ; อื่นๆ → เปิด isReadable ชั่วคราวแล้วคืนค่า
        static Texture2D GetSourceReadable(string path, string ext, out Action cleanup)
        {
            cleanup = null;
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
            {
                var bytes = File.ReadAllBytes(path);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!t.LoadImage(bytes, false)) { UnityEngine.Object.DestroyImmediate(t); return null; }
                cleanup = () => UnityEngine.Object.DestroyImmediate(t);
                return t;
            }

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            bool prevReadable = imp.isReadable;
            var prevComp = imp.textureCompression;
            if (!prevReadable || prevComp != TextureImporterCompression.Uncompressed)
            {
                imp.isReadable = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            cleanup = () =>
            {
                if (!prevReadable || prevComp != TextureImporterCompression.Uncompressed)
                {
                    imp.isReadable = prevReadable;
                    imp.textureCompression = prevComp;
                    imp.SaveAndReimport();
                }
            };
            return asset;
        }

        static void CopyImportSettings(string srcPath, List<string> childPaths)
        {
            if (AssetImporter.GetAtPath(srcPath) is not TextureImporter si) return;
            foreach (var p in childPaths)
            {
                if (AssetImporter.GetAtPath(p) is not TextureImporter ni) continue;
                ni.textureType = si.textureType;
                ni.spriteImportMode = si.spriteImportMode == SpriteImportMode.Multiple
                    ? SpriteImportMode.Single : si.spriteImportMode;
                ni.alphaIsTransparency = si.alphaIsTransparency;
                ni.mipmapEnabled = si.mipmapEnabled;
                ni.wrapMode = si.wrapMode;
                ni.filterMode = si.filterMode;
                ni.textureCompression = si.textureCompression;
                ni.sRGBTexture = si.sRGBTexture;
                ni.isReadable = false;
                ni.SaveAndReimport();
            }
        }

        static Rect FitRect(Rect box, int texW, int texH)
        {
            if (texW <= 0 || texH <= 0) return box;
            float s = Mathf.Min(box.width / texW, box.height / texH);
            float w = texW * s, h = texH * s;
            return new Rect(box.x + (box.width - w) * 0.5f, box.y + (box.height - h) * 0.5f, w, h);
        }

        static void EnsureChecker()
        {
            if (_checker != null) return;
            _checker = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var a = new Color(0.30f, 0.30f, 0.30f); var b = new Color(0.38f, 0.38f, 0.38f);
            _checker.SetPixels(new[] { a, b, b, a });
            _checker.filterMode = FilterMode.Point;
            _checker.wrapMode = TextureWrapMode.Repeat;
            _checker.Apply();
        }
    }
}
