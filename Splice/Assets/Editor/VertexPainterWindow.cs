using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Splice.EditorTools
{
    // ทา vertex color บนพื้นแมปสดๆ ในซีน (ใช้แทน Polybrush ที่เลิก support ตั้งแต่ Unity 6.3)
    // เห็นผลผ่าน shader จริงทันที — คู่กับ Splice/Ground Vertex Blend:
    //   ดำ = Base / แดง = Layer R / เขียว = Layer G / น้ำเงิน = Layer B
    //
    // ปลอดภัยกับไฟล์ต้นฉบับ: mesh ที่ import มา (FBX) แก้ไม่ได้ → ปุ่ม "Make Paintable" จะ clone เป็น .asset
    // แล้วสลับให้ MeshFilter/MeshCollider ใช้ตัวสำเนา (init เป็นสีดำ = Base ให้เลย กันกับดัก default ขาว)
    public class VertexPainterWindow : EditorWindow
    {
        private enum Channel { Base, R, G, B, A }
        private enum Mode { Paint, Smooth }

        [MenuItem("Tools/Splice/Vertex Painter")]
        private static void Open()
        {
            var w = GetWindow<VertexPainterWindow>("Vertex Painter");
            w.minSize = new Vector2(300, 460);
        }

        [SerializeField] private MeshFilter target;
        [SerializeField] private bool painting;
        [SerializeField] private Mode mode = Mode.Paint;
        [SerializeField] private Channel channel = Channel.R;
        [SerializeField] private float brushSize = 2f;
        [SerializeField] private float strength = 0.7f;
        [SerializeField] private float falloff = 1f;
        [SerializeField] private bool showPreview;

        private Mesh mesh;
        private Vector3[] verts;
        private Color[] colors;
        private Collider targetCollider;
        private Renderer targetRenderer;
        private Material previewMat;

        // reuse ทุก stroke — กัน alloc ระหว่างลากเมาส์
        private readonly List<int> brushIdx = new();
        private readonly List<float> brushWeight = new();
        private readonly List<Vector3> brushPos = new();

        private const string PaintedDir = "Assets/Meshes/Painted";

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (previewMat != null) DestroyImmediate(previewMat);   // material ชั่วคราว ไม่ทิ้งขยะไว้
            SceneView.RepaintAll();
        }

        // ---------------------------------------------------------------- window UI

        private void OnGUI()
        {
            EditorGUILayout.LabelField("เป้าหมาย", EditorStyles.boldLabel);

            var newTarget = (MeshFilter)EditorGUILayout.ObjectField("Mesh Filter", target, typeof(MeshFilter), true);
            if (newTarget != target) { target = newTarget; CacheTarget(); }

            if (GUILayout.Button("ใช้ object ที่เลือกในซีน"))
            {
                var sel = Selection.activeGameObject;
                if (sel != null && sel.TryGetComponent<MeshFilter>(out var mf)) { target = mf; CacheTarget(); }
                else ShowNotification(new GUIContent("เลือก object ที่มี MeshFilter ก่อน"));
            }

            if (target == null || target.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("เลือก object ที่มี MeshFilter (พื้นแมป) ก่อน", MessageType.Info);
                painting = false;
                return;
            }

            EditorGUILayout.Space();
            if (!DrawStatusAndGuards()) return;

            EditorGUILayout.Space();
            var newPreview = EditorGUILayout.ToggleLeft(
                "👁 โชว์ vertex color ล้วนบนผิว (realtime)", showPreview, EditorStyles.boldLabel);
            if (newPreview != showPreview) { showPreview = newPreview; SceneView.RepaintAll(); }
            if (showPreview)
                EditorGUILayout.HelpBox("วาดทับผิวจริงในหน้า Scene เพื่อดู mask ที่ทา — ไม่แตะ material ของ object (ปิดแล้วกลับปกติทันที)", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("โหมดแปรง", EditorStyles.boldLabel);
            mode = (Mode)GUILayout.Toolbar((int)mode, new[] { "ทา (Paint)", "เกลี่ย (Smooth)" }, GUILayout.Height(24));

            if (mode == Mode.Paint)
            {
                EditorGUILayout.Space();
                DrawChannelPicker();
            }
            else
            {
                EditorGUILayout.HelpBox("เกลี่ยสีให้กลืนกัน (ไม่สนช่อง) — ใช้ลบขอบแข็งๆ ให้ไล่นุ่มขึ้น", MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("แปรง", EditorStyles.boldLabel);
            brushSize = EditorGUILayout.Slider("ขนาด (Ctrl+Scroll)", brushSize, 0.05f, 50f);
            strength = EditorGUILayout.Slider("แรง (Shift+Scroll)", strength, 0.01f, 1f);
            falloff = EditorGUILayout.Slider("ความคมขอบ", falloff, 0.2f, 5f);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(targetCollider == null))
            {
                var label = painting ? "■ หยุดทา (กำลังทาอยู่)" : "▶ เริ่มทา";
                var old = GUI.backgroundColor;
                if (painting) GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button(label, GUILayout.Height(32))) { painting = !painting; SceneView.RepaintAll(); }
                GUI.backgroundColor = old;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("เติมทั้ง mesh", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Base")) FillAll(Channel.Base);
                if (GUILayout.Button("R")) FillAll(Channel.R);
                if (GUILayout.Button("G")) FillAll(Channel.G);
                if (GUILayout.Button("B")) FillAll(Channel.B);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("💾 บันทึก mesh (Save Assets)")) AssetDatabase.SaveAssets();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "ทา: ลากเมาส์ซ้ายบนพื้น\n" +
                "Ctrl+Scroll = ขนาดแปรง · Shift+Scroll = แรง\n" +
                "Alt+ลาก = หมุนกล้องได้ตามปกติ · Ctrl+Z = undo",
                MessageType.None);
        }

        // เช็คความพร้อม + ปุ่มแก้ให้ตรงจุด (คืน false ถ้ายังทาไม่ได้)
        private bool DrawStatusAndGuards()
        {
            var m = target.sharedMesh;
            EditorGUILayout.LabelField($"Mesh: {m.name}   ({m.vertexCount:N0} จุดยอด)");

            if (IsImportedModelMesh(m))
            {
                painting = false;
                EditorGUILayout.HelpBox(
                    "mesh นี้มาจากไฟล์ที่ import (FBX) → แก้ไม่ได้\n" +
                    "กดปุ่มล่างเพื่อ clone เป็น .asset แล้วสลับให้ใช้ตัวสำเนา (ไฟล์ต้นฉบับไม่ถูกแตะ)",
                    MessageType.Warning);
                if (GUILayout.Button("สร้างสำเนาที่ทาได้ (Make Paintable)", GUILayout.Height(28))) MakePaintable();
                return false;
            }

            if (targetCollider == null)
            {
                painting = false;
                EditorGUILayout.HelpBox("ต้องมี Collider เพื่อยิง ray หาจุดที่เมาส์ชี้", MessageType.Warning);
                if (GUILayout.Button("เพิ่ม Mesh Collider"))
                {
                    Undo.AddComponent<MeshCollider>(target.gameObject);
                    CacheTarget();
                }
                return false;
            }

            if (m.vertexCount < 200)
                EditorGUILayout.HelpBox(
                    "จุดยอดน้อยมาก → ทาได้หยาบ/แทบไม่เห็นผล\n" +
                    "vertex paint ละเอียดได้เท่าความถี่ mesh — ควร subdivide พื้นเป็นตารางก่อน (ช่องละ ~1-2 unit)",
                    MessageType.Warning);

            return true;
        }

        private void DrawChannelPicker()
        {
            EditorGUILayout.LabelField("ช่องที่ทา", EditorStyles.boldLabel);
            for (var i = 0; i < 5; i++)
            {
                var ch = (Channel)i;
                var on = channel == ch;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var old = GUI.backgroundColor;
                    if (on) GUI.backgroundColor = ChannelColor(ch) * 1.6f + Color.gray;
                    if (GUILayout.Button(on ? $"● {ChannelLabel(ch)}" : ChannelLabel(ch), GUILayout.Width(120)))
                        channel = ch;
                    GUI.backgroundColor = old;
                    EditorGUILayout.LabelField(LayerTextureName(ch), EditorStyles.miniLabel);
                }
            }
        }

        private static string ChannelLabel(Channel ch) => ch switch
        {
            Channel.Base => "Base (ดำ)",
            Channel.R => "R (แดง)",
            Channel.G => "G (เขียว)",
            Channel.B => "B (น้ำเงิน)",
            _ => "A (ขาว/alpha)"
        };

        // โชว์ชื่อ texture ที่ช่องนั้นคุมอยู่ (อ่านจาก material) — จะได้รู้ว่ากำลังทาอะไร
        private string LayerTextureName(Channel ch)
        {
            var mat = targetRenderer != null ? targetRenderer.sharedMaterial : null;
            if (mat == null) return "(ไม่มี material)";
            var prop = ch switch
            {
                Channel.Base => "_BaseMap",
                Channel.R => "_RMap",
                Channel.G => "_GMap",
                Channel.B => "_BMap",
                _ => "_AMap"
            };
            // layer A ต้องเปิดใน material ก่อน (Enable Layer A) — เตือนถ้ายังปิด
            if (ch == Channel.A && mat.HasProperty("_UseALayer") && mat.GetFloat("_UseALayer") < 0.5f)
                return "(ยังไม่เปิด Enable Layer A ใน material)";
            if (!mat.HasProperty(prop)) return "(shader ไม่มีช่องนี้)";
            var t = mat.GetTexture(prop);
            return t != null ? $"→ {t.name}" : "→ (ยังไม่ใส่ texture)";
        }

        // ---------------------------------------------------------------- scene view

        private void OnSceneGUI(SceneView sv)
        {
            if (target == null || mesh == null) return;

            // โชว์ vertex color ล้วน — วาดทับ mesh จริง (ไม่แตะ material ของ object → scene ไม่ dirty)
            if (showPreview && Event.current.type == EventType.Repaint)
            {
                EnsurePreviewMat();
                if (previewMat != null)
                {
                    previewMat.SetPass(0);
                    Graphics.DrawMeshNow(mesh, target.transform.localToWorldMatrix);
                }
            }

            if (!painting || targetCollider == null) return;

            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);

            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var hasHit = targetCollider.Raycast(ray, out var hit, 10000f);

            // วงแปรง
            if (hasHit)
            {
                var c = mode == Mode.Smooth ? new Color(1f, 0.85f, 0.2f) : ChannelColor(channel);
                Handles.color = new Color(c.r, c.g, c.b, 0.15f);
                Handles.DrawSolidDisc(hit.point, hit.normal, brushSize);
                Handles.color = new Color(c.r, c.g, c.b, 0.9f);
                Handles.DrawWireDisc(hit.point, hit.normal, brushSize, 2f);
                sv.Repaint();
            }

            // Ctrl/Shift + scroll = ขนาด/แรง
            if (e.type == EventType.ScrollWheel && (e.control || e.shift))
            {
                if (e.control) brushSize = Mathf.Clamp(brushSize - e.delta.y * 0.1f, 0.05f, 50f);
                else strength = Mathf.Clamp(strength - e.delta.y * 0.02f, 0.01f, 1f);
                e.Use();
                Repaint();
                return;
            }

            if (e.alt) return;   // ปล่อยให้ Alt+ลาก หมุนกล้องได้ตามปกติ

            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);   // กันคลิกแล้วไปเลือก object อื่น

            if (e.button != 0 || !hasHit) return;

            if (e.type == EventType.MouseDown)
            {
                Undo.RecordObject(mesh, "Vertex Paint");   // record ครั้งเดียวต่อ 1 stroke (ไม่ให้ undo stack บาน)
                Stroke(hit.point);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag)
            {
                Stroke(hit.point);
                e.Use();
            }
        }

        private void EnsurePreviewMat()
        {
            if (previewMat != null) return;
            var sh = Shader.Find("Hidden/Splice/Vertex Color Preview");
            if (sh == null) return;
            previewMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        // ---------------------------------------------------------------- paint

        private void Stroke(Vector3 worldPoint)
        {
            if (!CollectBrush(worldPoint)) return;

            if (mode == Mode.Paint) ApplyPaint();
            else ApplySmooth();

            mesh.colors = colors;
            EditorUtility.SetDirty(mesh);
        }

        // หาจุดยอดที่อยู่ในวงแปรง + น้ำหนักตาม falloff (ทำครั้งเดียวต่อ event แล้วให้ทั้ง Paint/Smooth ใช้ร่วม)
        private bool CollectBrush(Vector3 worldPoint)
        {
            brushIdx.Clear(); brushWeight.Clear(); brushPos.Clear();

            var m = target.transform.localToWorldMatrix;
            var r = brushSize;
            var r2 = r * r;

            for (var i = 0; i < verts.Length; i++)
            {
                var wp = m.MultiplyPoint3x4(verts[i]);
                var d2 = (wp - worldPoint).sqrMagnitude;
                if (d2 > r2) continue;

                var t = 1f - Mathf.Sqrt(d2) / r;
                var w = Mathf.Pow(Mathf.SmoothStep(0f, 1f, t), falloff);
                if (w <= 0.0001f) continue;

                brushIdx.Add(i);
                brushWeight.Add(w);
                brushPos.Add(wp);
            }
            return brushIdx.Count > 0;
        }

        private void ApplyPaint()
        {
            var dst = ChannelColor(channel);
            for (var k = 0; k < brushIdx.Count; k++)
            {
                var i = brushIdx[k];
                colors[i] = Color.Lerp(colors[i], dst, brushWeight[k] * strength);
            }
        }

        // เกลี่ย: แต่ละจุดวิ่งเข้าหา "ค่าเฉลี่ยของเพื่อนบ้านรอบตัว" (รัศมีย่อยในวงแปรง) → ขอบแข็งกลายเป็นไล่นุ่ม
        // วนเฉพาะจุดในวงแปรง (k²) ไม่ใช่ทั้ง mesh → เร็วพอสำหรับลากเมาส์
        private void ApplySmooth()
        {
            var subR = Mathf.Max(0.01f, brushSize * 0.5f);
            var subR2 = subR * subR;
            var n = brushIdx.Count;

            // อ่านค่าเดิมทั้งหมดก่อน แล้วค่อยเขียน — ไม่งั้นจุดที่เกลี่ยไปแล้วจะไปเพี้ยนค่าเฉลี่ยของจุดถัดไป
            var src = new Color[n];
            for (var k = 0; k < n; k++) src[k] = colors[brushIdx[k]];

            for (var a = 0; a < n; a++)
            {
                Vector4 sum = Vector4.zero;
                var wsum = 0f;
                for (var b = 0; b < n; b++)
                {
                    var d2 = (brushPos[a] - brushPos[b]).sqrMagnitude;
                    if (d2 > subR2) continue;
                    var w = 1f - Mathf.Sqrt(d2) / subR;
                    sum += (Vector4)src[b] * w;
                    wsum += w;
                }
                if (wsum <= 0.0001f) continue;

                var avg = (Color)(sum / wsum);
                var i = brushIdx[a];
                colors[i] = Color.Lerp(colors[i], avg, brushWeight[a] * strength);
            }
        }

        private void FillAll(Channel ch)
        {
            if (mesh == null || colors == null) return;
            Undo.RecordObject(mesh, "Fill Vertex Color");
            var c = ChannelColor(ch);
            for (var i = 0; i < colors.Length; i++) colors[i] = c;
            mesh.colors = colors;
            EditorUtility.SetDirty(mesh);
            SceneView.RepaintAll();
        }

        // ทาช่องไหน = ดันช่องนั้นขึ้น + ช่องอื่นลง (shader ไล่ Base→R→G→B→A ช่องหลังทับช่องหน้า)
        // ⚠️ alpha = mask ของ layer A → ทุกช่องอื่นต้อง alpha 0 (ถ้า Base เป็น (0,0,0,1) ทา Base จะกลายเป็น layer A)
        private static Color ChannelColor(Channel ch) => ch switch
        {
            Channel.Base => new Color(0f, 0f, 0f, 0f),
            Channel.R => new Color(1f, 0f, 0f, 0f),
            Channel.G => new Color(0f, 1f, 0f, 0f),
            Channel.B => new Color(0f, 0f, 1f, 0f),
            _ => new Color(0f, 0f, 0f, 1f)
        };

        // ---------------------------------------------------------------- mesh plumbing

        private void CacheTarget()
        {
            mesh = null; verts = null; colors = null; targetCollider = null; targetRenderer = null;
            painting = false;
            if (target == null || target.sharedMesh == null) return;

            targetCollider = target.GetComponent<Collider>();
            targetRenderer = target.GetComponent<Renderer>();

            var m = target.sharedMesh;
            if (IsImportedModelMesh(m)) return;   // ยังแก้ไม่ได้ ต้อง Make Paintable ก่อน

            mesh = m;
            verts = mesh.vertices;               // cache: mesh.vertices alloc ทุกครั้งที่เรียก
            colors = mesh.colors;
            if (colors == null || colors.Length != mesh.vertexCount)
            {
                colors = new Color[mesh.vertexCount];
                for (var i = 0; i < colors.Length; i++) colors[i] = new Color(0f, 0f, 0f, 0f);   // Base (alpha 0 ด้วย — alpha คือ mask layer A)
                mesh.colors = colors;
                EditorUtility.SetDirty(mesh);
            }
        }

        // mesh จากไฟล์ import (.fbx/.obj/.blend) = sub-asset แก้ไม่ได้. .asset = สร้างเอง แก้ได้
        private static bool IsImportedModelMesh(Mesh m)
        {
            var path = AssetDatabase.GetAssetPath(m);
            if (string.IsNullOrEmpty(path)) return false;     // mesh สร้างเองใน memory
            return Path.GetExtension(path).ToLowerInvariant() != ".asset";
        }

        // clone mesh → .asset + init สีดำ + สลับให้ MeshFilter/MeshCollider ใช้ตัวสำเนา (ไฟล์ต้นฉบับไม่ถูกแตะ)
        private void MakePaintable()
        {
            var src = target.sharedMesh;
            var copy = Instantiate(src);
            copy.name = src.name + "_painted";

            var c = new Color[copy.vertexCount];
            for (var i = 0; i < c.Length; i++) c[i] = new Color(0f, 0f, 0f, 0f);   // Base ทั้ง mesh (alpha 0 = ไม่ติด layer A)
            copy.colors = c;

            if (!Directory.Exists(PaintedDir)) Directory.CreateDirectory(PaintedDir);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{PaintedDir}/{copy.name}.asset");
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(target, "Make Paintable");
            target.sharedMesh = copy;
            if (target.TryGetComponent<MeshCollider>(out var mc))
            {
                Undo.RecordObject(mc, "Make Paintable");
                mc.sharedMesh = copy;
            }
            EditorUtility.SetDirty(target);

            CacheTarget();
            ShowNotification(new GUIContent($"สร้างสำเนาแล้ว: {path}"));
        }
    }
}
