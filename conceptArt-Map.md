# Concept Art Prompts — MAP / พื้นแมป (Tileable Ground Textures) 🗺️

> texture พื้นแบบ **tile ซ้ำได้ (seamless)** ใช้กับ **CartoonShader (Shader Graph)** + **vertex paint** ผสมหญ้า↔ดิน
> ทำไมต้อง tileable: กล้องเราซูมเข้าได้ → ใช้ texture ใบเดียวยืดทั้งแมปจะแตก/เบลอ. tile ซ้ำ = **texture เล็กแต่คมทุกระดับซูม** + reuse ได้ทุกแมป

---

## วิธีใช้

1. **1 prompt = 1 texture — ต้อง gen ทีละใบ** (ห้ามจัด layout ตารางเหมือนไฟล์ตัวละคร เพราะจะ tile ไม่ได้)
2. copy **TEXTURE STYLE BLOCK** + prompt ของใบนั้น ส่งพร้อมกัน
3. สั่งให้ออกเป็น **จัตุรัส 1:1** (ขอ 1024×1024 หรือ 2048×2048)
4. ได้ภาพมาแล้ว → **ต้องเช็ค/แก้ seam** (ดูหัวข้อ ⚠️ ด้านล่าง)

---

> ⚠️ **กฎ no-lighting (สำคัญที่สุดของ texture พื้น):** **ห้ามมีแสงตกกระทบ/เงา/ไฮไลต์/vignette (ขอบมืด) อบมาในภาพ** — เพราะแสงเงาจะมาจาก shader ในเกม. ถ้ามีเงาอบมา เวลา tile จะเห็น "เงาซ้ำเป็นตาราง" ชัดมาก และชนกับแสงจริงในเกม
>
> ⚠️ **กฎ no-focal-point:** **ห้ามมีจุดเด่น/ของชิ้นใหญ่/ลวดลายเอกลักษณ์** (ก้อนหินใหญ่, ดอกไม้เด่น, รอยแตกยาว) — เพราะพอ tile จะเห็นซ้ำเป็นแพตเทิร์นทันที. ให้รายละเอียด **กระจายสม่ำเสมอ ขนาดเล็ก ใกล้เคียงกัน**
>
> ⚠️ **กฎ muted-background (สำคัญที่สุดของงานพื้น):** พื้นคือ **ฉากหลัง** ที่ตัวละครสีสดจะยืนทับ → ต้อง **หม่น ซีด อมเทา/อมดิน คอนทราสต์ต่ำ ค่าสีอยู่กลางๆ แคบๆ**
> **ถ้าพื้นสดใสเท่าตัวละคร = ตัวละครจะจมกลืนพื้น อ่านไม่ออก** (นี่คือเหตุผลที่พื้นในเกมจริงหม่นเสมอ)
> ห้าม: สีจัด/นีออน/ขาวจัด/ดำจัด/คอนทราสต์แรง. **ตัวละคร = สด+คอนทราสต์สูง / พื้น = หม่น+คอนทราสต์ต่ำ** — ต่างกันชัดคือถูก

## 🎨 TEXTURE STYLE BLOCK (แปะหน้า prompt ทุกใบ)

```
Seamless tileable game ground texture, perfectly flat TOP-DOWN orthographic
view (looking straight down, absolutely no perspective, no horizon, no depth).
Hand-painted painterly stylized look, like a painted fantasy RPG map: soft
brush texture, softly blended edges, gentle organic shapes.
IMPORTANT palette — this is BACKGROUND art that bright colourful characters
will stand on top of, so it must stay QUIET and never compete with them: use
MUTED, DESATURATED, slightly greyed earthy tones with LOW contrast. Keep all
values in a narrow MID range — no pure white, no pure black, no dark holes,
no bright hotspots. Absolutely NO candy-bright colours, NO neon, NO high
saturation, NO strong colour contrast, NO vivid cartoon palette.
IMPORTANT: the image must TILE SEAMLESSLY — the left edge
continues perfectly into the right edge and the top edge into the bottom edge.
IMPORTANT: absolutely NO baked lighting — no cast shadows, no directional
light, no specular highlights, no vignette, no darkened corners or edges; keep
brightness perfectly even across the whole image (lighting is added by the game
engine). IMPORTANT: NO focal point — detail must be small, uniform and evenly
scattered across the whole square, with no large unique feature, no big object,
no long crack or line that would obviously repeat when tiled. Uniform detail
scale everywhere. Square 1:1 composition, fills the entire frame edge to edge.
No characters, no props, no objects, no text, no labels, no watermark, no logo,
no border, no frame.
```

---

# 1) NEUTRAL — ชุดกลาง (ใช้ได้ทุกแมป/ทุกเผ่า) ⭐ ทำชุดนี้ก่อน

> โทนกลางๆ ไม่ผูกเผ่า → เป็นพื้นฐานของทุกแมป (แมป raid, แมปฐาน, แมปเทส)

### 1A — Neutral Grass (หญ้ากลาง)
```
[TEXTURE STYLE BLOCK]
The texture is hand-painted GRASS ground seen from directly above:
a muted sage / olive green lawn made of small soft rounded grass clumps and
tiny blade tufts packed evenly across the whole square, with very gentle
variation between slightly lighter and slightly darker greyed-green patches so
it does not look dead. Keep the green DESATURATED, dusty and mid-toned (not
vivid, not fresh, not neon) so bright characters standing on it stand out and
so it fits any faction palette. All detail is small and uniform.
```

### 1B — Neutral Dirt (ดินกลาง)
```
[TEXTURE STYLE BLOCK]
The texture is hand-painted DIRT / bare earth ground seen from directly
above: a muted dusty warm brown packed-soil surface with soft rounded shapes,
very gentle variation between slightly lighter and darker greyed-brown patches,
and tiny evenly-scattered pebbles and small soil grains. Keep the brown
DESATURATED and mid-toned (dusty, slightly greyed — not rich, not vivid) so it
fits any faction palette. All detail is small and uniform — no large rocks,
no big cracks.
```

---

# 2) NATURAL — ชุดเผ่า Natural 🌿

> โทนตาม palette เผ่า: **leafy green / earthy brown / terracotta / bone-white** (ตรงกับ `conceptArt-Natural.md`)
> ใช้ทับแมปของเผ่า Natural ให้บรรยากาศป่า/ธรรมชาติดิบ

### 2A — Natural Grass (หญ้าเผ่า Natural)
```
[TEXTURE STYLE BLOCK]
The texture is hand-painted FOREST GRASS ground for a nature-beast faction,
seen from directly above: muted mossy green grass made of small soft rounded
clumps, with tiny scattered clover leaves, small round leaves and soft moss
patches mixed evenly throughout. Very gentle variation between greyed mossy
green and slightly deeper olive green. A few tiny dull bone-white and muted
terracotta specks scattered very sparsely for faction flavour. NATURAL BEAST
palette but DESATURATED and dusty: greyed leafy green with earthy brown and
subtle muted terracotta accents — quiet enough that bright characters read
clearly on top. All detail is small and uniform — no big leaves, no flowers
large enough to stand out.
```

### 2B — Natural Dirt (ดินเผ่า Natural)
```
[TEXTURE STYLE BLOCK]
The texture is hand-painted FOREST FLOOR / earth path ground for a
nature-beast faction, seen from directly above: muted dusty earthy brown soil
with a faint terracotta tint, mixed evenly with tiny scattered pebbles, small
soil grains, a few tiny muted moss specks and a few very small dull bone-white
bits. Very gentle variation between greyed brown and soft terracotta patches.
NATURAL BEAST palette but DESATURATED and dusty: earthy brown with subtle muted
terracotta, quiet mossy green and dull bone-white accents — quiet enough that
bright characters read clearly on top. All detail is small and uniform — no big
rocks, no exposed roots, no cracks large enough to stand out.
```

---

# 3) MATERIAL SHEET — ทุกวัสดุในรูปเดียว (ตัดเอา) ✂️

> **1 ภาพ = 8 วัสดุ** เรียงเป็นตาราง → ตัดไปใช้ทีละใบ
> **ข้อดีใหญ่:** ทุกวัสดุ **สไตล์/ขนาดลาย/palette ตรงกันทั้งชุด** (gen ทีละใบมักได้สไตล์เพี้ยนกัน) — ได้ "ชุดวัสดุ" ที่ดูเป็นเซ็ตเดียวกัน
> **ข้อแลก (ต้องรู้):** ① แต่ละช่อง **ยังไม่ seamless** → ตัดมาแล้วต้อง offset-fix (ดูหัวข้อล่าง) ② **ความละเอียดต่อช่องน้อยกว่า** (ภาพ 2048 ÷ 8 ช่อง ≈ 512/ช่อง) → ใบที่ใช้หนักๆ แนะนำ gen เดี่ยวเต็มความละเอียดซ้ำอีกที (ใช้ sheet เป็น reference สไตล์)

### 3A — Material Sheet ×8 (หญ้า/ดิน/หิน/ทราย/โคลน/น้ำ/อิฐมอส/หญ้าแห้ง)
```
Material swatch reference sheet for a stylized mobile game, perfectly flat
TOP-DOWN orthographic view (looking straight down, no perspective, no horizon,
no depth). Hand-painted painterly look like a painted fantasy RPG map: soft
brush texture, softly blended edges, gentle organic shapes.
IMPORTANT palette — these are BACKGROUND ground materials that bright colourful
characters will stand on, so they must stay QUIET and never compete with them:
MUTED, DESATURATED, slightly greyed earthy tones, LOW contrast, all values kept
in a narrow MID range. Absolutely NO candy-bright colours, NO neon, NO high
saturation, NO strong contrast, NO vivid cartoon palette.
Grid layout, 4 columns x 2 rows = 8 SQUARE material swatches, all exactly the
same size, evenly spaced with clear gaps between them, nothing overlapping.
IMPORTANT: absolutely NO baked lighting — no cast shadows, no directional
light, no specular highlights, no vignette, no darkened corners or edges;
brightness perfectly even across every swatch (lighting comes from the game
engine).
IMPORTANT: inside EACH swatch the detail must be small, uniform and evenly
scattered, with NO focal point and no large unique feature — so each swatch can
be cut out and turned into a repeating tile.
All 8 swatches share the same art style, the same detail scale and the same
palette family so they read as one matching set.
Pure white background, no text, no labels, no watermark, no borders, no frames.

The 8 swatches, left to right, top row then bottom row (all muted and dusty):
1. GRASS - muted sage/olive green lawn, small soft rounded grass clumps and
   blade tufts.
2. DIRT - dusty greyed warm brown packed soil, tiny pebbles and soil grains.
3. ROCK - soft grey-beige stone, gently rounded rocky facets and small chips.
4. SAND - pale dusty greyed beige sand, fine grains and soft subtle ripples.
5. MUD - muted dark greyish-brown mud, soft rounded lumpy patches (no specular
   highlights), small mud clods.
6. WATER - shallow dusty teal water with soft rounded painted ripple shapes,
   desaturated and calm (not bright turquoise).
7. MOSSY OLD BRICK - weathered muted grey-brown brick paving, chipped rounded
   bricks with quiet greyed-green moss in the gaps.
8. DRY GRASS - muted pale tan/khaki dry grass, small dry blade tufts.
```

> อยากได้เวอร์ชัน **เผ่า Natural** ของ sheet นี้: เติมท้าย prompt ว่า
> *"Use the NATURAL BEAST palette throughout: leafy green, earthy brown, terracotta red-orange accents, bone-white details."*

### ✂️ ขั้นตอนหลังได้ sheet
1. ตัดแต่ละช่องออกเป็นไฟล์ **จัตุรัส** แยกกัน (ครอปเอาเฉพาะเนื้อวัสดุ อย่าติดพื้นขาว/ขอบ)
2. **แต่ละใบต้อง offset-fix ให้ seamless** ก่อนใช้ (หัวข้อถัดไป)
3. import เข้า Unity ตามตาราง settings ด้านล่าง (**Wrap Mode = Repeat**)

### 🎮 เอาไปใช้กับ shader ยังไง
`Splice/Ground Vertex Blend` (`Assets/Shaders/GroundVertexBlend.shader`) ผสมได้ **4 texture ต่อ material** → เลือก 4 ใบจากชุดนี้ต่อแมป เช่น
- **แมปป่า:** Base=หญ้า · R=ดิน · G=หิน · B=โคลน
- **แมปทะเลทราย:** Base=ทราย · R=ดิน · G=หิน · B=หญ้าแห้ง
- **แมปซากเมือง:** Base=อิฐมอส · R=ดิน · G=หญ้า · B=น้ำ

---

## ⚠️ AI gen ทำ seamless ไม่เป๊ะ 100% — ต้องแก้ต่อ

โมเดล image gen **มักทำขอบไม่ต่อกันสนิท** ถึงจะสั่งไปแล้ว → เอามาแก้ก่อนใช้:

**วิธีเช็ค+แก้ (Photoshop / Krita / GIMP — ฟรีก็ได้)**
1. เปิดภาพ → **Filter > Other > Offset** (Krita/GIMP: Offset) เลื่อน **50% ทั้งแนวนอน+ตั้ง**
2. **seam จะโผล่มากลางภาพ** → ใช้ **Clone Stamp / Healing** ป้ายให้กลืน
3. Offset กลับ → ได้ texture ที่ tile ได้จริง

**ทางลัด:** ถ้าไม่อยากแก้มือ — ใช้เว็บ/เครื่องมือทำ seamless อัตโนมัติ หรือ gen แล้วครอปเอาเฉพาะส่วนกลางที่ลายสม่ำเสมอ แล้วค่อย offset-fix

---

## 🟢 Import settings ใน Unity (สำคัญ ไม่งั้น tile ไม่ติด)

| ช่อง | ค่า |
|---|---|
| **Wrap Mode** | **Repeat** ← ไม่ตั้งอันนี้ = tile ไม่ได้ |
| **Filter Mode** | Bilinear |
| Texture Type | Default |
| **sRGB (Color Texture)** | ✔ |
| Compression | มือถือ: ASTC |
| Max Size | 1024 (พื้นดินไม่ต้องใหญ่ — tile เอา) |
| Generate Mip Maps | ✔ (กันหยึกหยักตอนซูมออก) |

**ใน material (CartoonShader):** ตั้ง **Tiling** ให้ลายมีขนาดสมจริงกับตัวละคร (เริ่มลอง 5-10 แล้วปรับตาขนาดแมป)

---

## 📝 Checklist หลังได้ภาพ

- [ ] **ไม่มีเงา/แสงตกกระทบ/ขอบมืด (vignette)** — สว่างเท่ากันทั้งใบ
- [ ] **ไม่มีจุดเด่น/ของชิ้นใหญ่** — ลายเล็ก กระจายสม่ำเสมอ
- [ ] มองตรงจากด้านบน 100% (ไม่มี perspective/เงาลึก)
- [ ] จัตุรัส 1:1 เต็มเฟรม ไม่มีขอบ/กรอบ
- [ ] **ผ่าน offset test แล้ว** (เลื่อน 50% ไม่เห็น seam)
- [ ] เอาไป tile ในเกมแล้ว **ไม่เห็นแพตเทิร์นซ้ำชัด** ตอนซูมออก

---

---

# 4) NORMAL MAP (ออปชัน)

> ⚠️ **อย่า gen normal map ด้วย AI** — มันจะได้ภาพ "สีม่วงๆ เหมือน normal map" ที่ค่าผิดทั้งใบ ใช้ไม่ได้จริง
> **normal map ต้องคำนวณจาก albedo** ด้วยเครื่องมือแปลง (height → normal)

**วิธีทำ (เลือกอันเดียว):**
| เครื่องมือ | หมายเหตุ |
|---|---|
| **NormalMap-Online** (เว็บฟรี) | ลาก texture ใส่ → ปรับ strength → โหลด normal map. เร็วสุด |
| **Materialize** (ฟรี) | ทำ normal/height/AO จาก albedo ครบ |
| **Krita / GIMP** | มีฟิลเตอร์ Normal Map ในตัว |
| **Blender** | Bake จาก height/bump |

**ตั้งค่าใน Unity:** Texture Type = **Normal map** · Wrap Mode = **Repeat**
**ใน shader:** ติ๊ก **Use Normal Maps** ใน `Splice/Ground Vertex Blend` → ใส่ normal ทีละ layer → ปรับ **Normal Strength**

> 💡 **ความเห็นตรงๆ:** กล้องเรา**มองจากด้านบน + ลุค toon แบนๆ** → normal map ได้ผลน้อยมากแต่กิน 4 sample เพิ่ม
> แนะนำ **ยังไม่ต้องใส่** — ทำ albedo ให้สวยก่อน ถ้าพื้นดูแบนไปค่อยเปิดทีหลัง (shader ปิดไว้ = ไม่กินเลย)

---

# 5) WATER NOISE (สำหรับ Toon Water shader)

> `Splice/Toon Water` ต้องการ texture **noise ขาวดำ tile ได้** 1 ใบ (ใช้ตัดขอบฟอง)

```
[TEXTURE STYLE BLOCK]
The texture is a soft grayscale NOISE pattern for a stylized water foam mask:
smooth organic cloudy blobs in shades of black, grey and white, evenly
distributed, medium-sized soft rounded shapes with blurry edges. Pure
grayscale only (no color). Even contrast across the whole square.
```

**ตั้งค่าใน Unity:** Texture Type = Default · **Wrap Mode = Repeat** · **sRGB = ปิด** (เป็น data ไม่ใช่สี)

---

## ทำต่อจากนี้ได้ (ถ้าต้องการ)

- **texture พื้นเพิ่ม:** หิมะ/ลาวา/กรวด — ใช้ STYLE BLOCK เดิม เปลี่ยนแค่คำอธิบายวัสดุ
- **เผ่าอื่น:** เพิ่มหมวด Human / Galax / Darkside ในไฟล์นี้ได้ (STYLE BLOCK เดิม เปลี่ยน palette)
