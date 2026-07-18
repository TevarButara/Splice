using System.Collections.Generic;
using UnityEngine;

namespace Splice.Base
{
    // โชว์ grid ช่องที่วางได้ ตอนกำลังเลือกจะวาง (armed) — เขียว = ว่าง, แดง = วางไม่ได้/มีของ.
    //  - ขนาดช่อง = footprint ของชิ้นที่กำลังวาง (dynamic จาก SO ผ่าน BaseBuildManager.CurrentFootprint)
    //  - **วาดจาก "จุดกลางจอ" ไล่ออกเป็นรัศมีวงกลม** → grid อยู่กลางจอเสมอ (ช่องน้อยก็ไม่ไปกองขอบใดขอบหนึ่ง)
    //  - view-culled: วาดแค่รัศมีที่ครอบจอ → floor ใหญ่แค่ไหนก็ไม่ค้าง
    // สร้าง Quad ในโค้ด (หงายราบบนพื้น ไม่มี collider, ใต้ container scale=1) + material โปร่งใสให้ alpha ทำงาน.
    public class BuildGridOverlay : MonoBehaviour
    {
        [SerializeField] private BaseBuildManager buildManager;
        [Tooltip("กล้อง Build Mode — เว้นว่าง = Camera.main")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("material ของ tile — เว้นว่าง = สร้าง unlit โปร่งใสให้")]
        [SerializeField] private Material markerMaterial;
        [SerializeField] private Color freeColor = new(0f, 1f, 0f, 0.4f);
        [SerializeField] private Color blockedColor = new(1f, 0f, 0f, 0.4f);
        [SerializeField] private float yOffset = 0.03f;
        [Tooltip("สัดส่วนขนาด tile ต่อ 1 ช่อง (0-1) — <1 เว้นร่องให้เห็นเส้นแบ่ง")]
        [SerializeField] private float cellFill = 0.9f;
        [Tooltip("เพดานจำนวน tile ที่วาดพร้อมกัน (safety)")]
        [SerializeField] private int maxVisibleCells = 2000;

        private readonly List<Renderer> pool = new();
        private Transform cellsRoot;
        private bool shown;

        private Camera Cam => viewCamera != null ? viewCamera : Camera.main;

        private Transform CellsRoot()
        {
            if (cellsRoot == null) cellsRoot = new GameObject("BuildGridOverlayCells").transform;
            return cellsRoot;
        }

        private void OnDestroy()
        {
            if (cellsRoot != null) Destroy(cellsRoot.gameObject);
        }

        private void Update()
        {
            if (buildManager == null) return;
            if (!buildManager.HasArmed) { if (shown) { HideAll(); shown = false; } return; }
            shown = true;
            Rebuild();
        }

        private void Rebuild()
        {
            var cam = Cam;
            var foot = buildManager.CurrentFootprint;
            if (cam == null || foot <= 0f) { HideAll(); return; }

            var origin = buildManager.GridOrigin;
            var groundY = origin.y;

            if (!TryGetFocus(cam, groundY, out var focus, out var radiusWorld)) { HideAll(); return; }

            // ช่องที่อยู่ "กลางจอ" (snap เข้ากริดขนาด foot)
            var fx = Mathf.Round((focus.x - origin.x) / foot);
            var fz = Mathf.Round((focus.z - origin.z) / foot);

            // รัศมีเป็นจำนวนช่องให้ครอบจอ + cap กัน loop ใหญ่
            var radiusCells = Mathf.CeilToInt(radiusWorld / foot) + 1;
            var maxRadius = Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(maxVisibleCells) * 0.5f));
            radiusCells = Mathf.Clamp(radiusCells, 1, maxRadius);

            var tile = foot * Mathf.Clamp01(cellFill);
            var r2 = radiusCells * radiusCells;
            var index = 0;

            for (var dx = -radiusCells; dx <= radiusCells; dx++)
            for (var dz = -radiusCells; dz <= radiusCells; dz++)
            {
                if (dx * dx + dz * dz > r2) continue;    // วงกลม ไม่ใช่สี่เหลี่ยม
                var cx = (fx + dx) * foot + origin.x;
                var cz = (fz + dz) * foot + origin.z;
                var cell = new Vector3(cx, groundY, cz);
                if (!buildManager.IsWithinBuildArea(cell, foot)) continue; // เฉพาะในพื้นที่เมือง

                var r = GetMarker(index++);
                var t = r.transform;
                t.SetPositionAndRotation(new Vector3(cx, groundY + yOffset, cz), Quaternion.Euler(90f, 0f, 0f));
                t.localScale = new Vector3(tile, tile, 1f);
                SetColor(r, buildManager.CanPlaceCell(cell) ? freeColor : blockedColor);

                if (index >= maxVisibleCells) { HideRest(index); return; }
            }

            HideRest(index);
        }

        // จุดกลางจอบนพื้น (focus) + รัศมี world ที่ครอบจอ (ระยะจาก focus ไปมุมจอที่ไกลสุด)
        private bool TryGetFocus(Camera cam, float groundY, out Vector3 focus, out float radiusWorld)
        {
            focus = default; radiusWorld = 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

            var centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!plane.Raycast(centerRay, out var cd) || cd <= 0f) return false;
            focus = centerRay.GetPoint(cd);

            var f2 = new Vector2(focus.x, focus.z);
            for (var i = 0; i < 4; i++)
            {
                var ray = cam.ViewportPointToRay(new Vector3(i & 1, (i >> 1) & 1, 0f));
                if (!plane.Raycast(ray, out var d) || d <= 0f) continue;
                var p = ray.GetPoint(d);
                radiusWorld = Mathf.Max(radiusWorld, Vector2.Distance(f2, new Vector2(p.x, p.z)));
            }
            if (radiusWorld <= 0f) radiusWorld = 20f; // มุมจอไม่ชนพื้น (กล้องชัน) → fallback
            return true;
        }

        private void HideRest(int from)
        {
            for (var i = from; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        private Renderer GetMarker(int i)
        {
            if (i < pool.Count)
            {
                if (!pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(true);
                return pool[i];
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GridCell";
            go.transform.SetParent(CellsRoot(), false);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // กัน tile บัง raycast

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var mat = markerMaterial != null ? new Material(markerMaterial) : new Material(FindShader());
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            MakeTransparent(mat);
            renderer.material = mat;

            pool.Add(renderer);
            return renderer;
        }

        private static Shader FindShader() =>
            Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");

        private static void MakeTransparent(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            var m = renderer.material;
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        }

        private void HideAll()
        {
            foreach (var renderer in pool)
                if (renderer != null) renderer.gameObject.SetActive(false);
        }
    }
}
