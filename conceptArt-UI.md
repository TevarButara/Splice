# Concept Art Prompts — UI 🎛️

> UI สไตล์ **fantasy mobile game** (ref: Clash Royale / Wild Sky TD / Supercell) — **ไม้ + โลหะ + อัญมณี**, เรียบแต่สวย ทันสมัย โค้งมน **น่ารักนิดๆ** เข้ากับตัวละคร chibi ของเรา
>
> 🌐 **นี่คือ UI หลัก/กลางของเกม (GLOBAL — ไม่ผูกเผ่าใดเผ่าหนึ่ง)** → โทนต้อง **neutral** ใช้ได้ทุกจอทุกเผ่า **ไม่ใช้สี accent ประจำเผ่า** (เขียวใบไม้ Natural / ฯลฯ) มาเป็นสีหลักของกรอบ

## 🎯 PALETTE ล็อก (ยึด ref ให้ใกล้สุด — เรียบ สะอาด ดูหรู)
> โทนหลักของ ref = **ฟ้า/เทา-น้ำเงินเย็น เป็นแกน** + **ไม้น้ำตาลอุ่น** + **ทองเป็นเส้นขอบ/จุดเน้น** + **อัญมณีฟ้า-ม่วง-ชมพู** เป็นแต้มสี
> - **โครงหลัก (panel/bar/header):** steel blue-grey + warm wood + gold trim
> - **ทอง = ใช้เป็น "trim/accent" เท่านั้น ไม่ใช่ทั้งชิ้น** (ไม่งั้นดูเชย/ล้น)
> - **หรูมาจากความ "สะอาด + วัสดุดูพรีเมียม + gradient นุ่ม"** ไม่ใช่ลายเยอะ
> - ปุ่มเท่านั้นที่มีสีสด (เขียว/แดง/ทอง) เป็น call-to-action — ส่วนกรอบ/พื้น UI คุมโทนเย็นให้นิ่ง

## ⚡ STYLE = VIBRANT TOON (ห้ามหม่น/painterly)
> ที่ gen ออกมา "เหล็กหม่น เรียลๆ painterly" = ผิด. ที่ต้องการ (ตาม ref) = **toon สีสด glossy วาว + ตัดเส้นหนาคม + ประกายดาว + ไล่เฉดเงาชัด สะอาดแบบ vector** (ไม่ใช่พู่กันเรียล). STYLE BLOCK ใหม่บังคับไว้แล้ว → override painterly/realistic/grey-metal ทุกจุด

## 💎 ตัวช่วยที่ได้ผลกว่าคำพูดเยอะ — แนบภาพ ref!
> คำ prompt อย่างเดียว**คุมสไตล์เป๊ะยาก** — ตัวแปรที่ได้ผลสุดคือ **แนบภาพ ref ที่ให้มา (icon pack / gem / cartoon props) เข้า generator เป็น "style reference / image reference"** แล้วบอกว่า *"match this exact icon art style"*
> - ChatGPT/DALL·E: แนบภาพ ref + พิมพ์ *"in the exact art style of this reference"*
> - Midjourney: ใช้ `--sref <url ภาพ ref>` (style reference) หรือใส่ภาพ ref เป็น image prompt
> - เครื่องมืออื่น (SDXL ฯลฯ): ใช้ IP-Adapter / style reference
> **แนบ ref + STYLE BLOCK คู่กัน = ตรงสุด**

## 🚫 NEGATIVE prompt (ใส่ในช่อง negative ถ้าเครื่องมือมี)
```
painterly, oil painting, brush texture, sketchy, realistic, photorealistic,
gritty, grunge, muddy, desaturated, dull, matte, flat grey metal, noisy,
rough shading, 3d render, blurry, text, watermark
```

## 🔰 EMBLEM ของเกม
> โลโก้กลาง = **ตัว "S" ทรงสายฟ้า** (S-shaped lightning bolt) ย่อจากชื่อเกม **Splice** — ใช้บนเหรียญ gold และเอาไปใช้เป็นสัญลักษณ์เกมที่อื่นได้ (loading, app icon ฯลฯ)

---

## workflow: Chroma pink → crop → 9-slice (สำคัญ)

**ชิ้น UI ที่ต้อง "ยืดได้"** (panel/button/bar/card/header) → gen บน **พื้นชมพู chroma แบนๆ** → ใน Unity คีย์สีชมพูออก + ตั้ง **Sprite Border (9-slice)** → ยืดตามขนาดจริงโดยขอบ/มุมไม่เพี้ยน

**สิ่งที่ต้องรู้:**
- **สีชมพู chroma = magenta แบนล้วน `#FF00FF`** (สีที่ไม่มีในอาร์ต) → คีย์ออกง่าย
- ⚠️ **ยกเว้นชิ้นที่มีสีชมพู/ม่วงในตัว** (เช่น gem ชมพู, ปุ่มม่วง) → ใช้ **chroma เขียว `#00FF00`** แทน ไม่งั้นคีย์แล้วตัวอาร์ตแหว่ง
- ⚠️ **AI ทำขอบ chroma ไม่เนียน 100%** — มักมีขอบชมพูปนนิดๆ → **ต้องเก็บขอบใน Photoshop** (คีย์ + ลบขอบ 1-2px) ก่อนเข้า Unity. ถ้าเนี้ยบสุดคือ gen บนพื้นแล้ว **ตัดมือ** เอา
- **icon/card** ที่ไม่ต้องยืด → ไม่ต้อง 9-slice แค่คีย์พื้นออกเป็น sprite โปร่ง

**ของที่ไม่ใช้ chroma:** **พื้นหลังฉากเต็มจอ (หมวด 8)** = ภาพเต็ม ไม่ต้องคีย์

---

## วิธีใช้

1. **1 prompt = 1 ชิ้น** (panel/button/bar 1 อัน) — copy `UI STYLE BLOCK` + prompt ของชิ้นนั้น
2. สั่งออก **หน้าตรง แบน 2D (orthographic, no perspective)** อยู่กลางเฟรม
3. ชิ้นยืดได้ → บอกให้ **ขอบ/มุมเท่ากันทุกด้าน + ตรงกลางเรียบ** (เงื่อนไข 9-slice)
4. **icon set / card frames** ทำเป็น **sheet** ได้ (สไตล์/สเกลตรงกันทั้งชุด — เหมือน material sheet)

---

## 🎨 UI STYLE BLOCK (แปะหน้า prompt ทุกชิ้น)

```
Clean vibrant CARTOON game icon, mobile game icon-pack asset, front-facing flat
2D view (straight on, no perspective, no cast shadow). Crisp digital TOON
rendering: BRIGHT SATURATED colors, smooth glossy gradient shading with clear
light-to-shadow steps, big soft SPECULAR highlights (wet shiny look) and tiny
4-point STAR SPARKLE glints on shiny / gem / metal surfaces. A BOLD CLEAN
OUTLINE around the whole shape and the main inner shapes — uniform thickness,
usually a darker shade of the object's own color (not pure black). Chunky,
rounded, appealing, sticker-like, juicy and premium. Simple bold silhouette that
reads clearly at small size. Consistent light from top-left.
STYLE MUST BE: colorful, clean, glossy, cel-shaded-with-gradients, vector-like.
STYLE MUST NOT BE: painterly, sketchy, textured brushwork, realistic, gritty,
muddy, desaturated, dull grey metal, flat matte.
GLOBAL NEUTRAL UI theme (not tied to any faction). Centered, generous even
margin, fully inside frame. No text, no letters, no numbers, no logo, no
watermark.
```

## 🟪 CHROMA addendum (แปะต่อ — สำหรับชิ้นที่ต้องคีย์พื้นออก)

```
The element sits on a PERFECTLY FLAT solid MAGENTA background (pure #FF00FF,
chroma key green-screen style), a single even color with NO gradient, NO
texture, NO shadow on the background. The element does not touch the frame
edges. Clean sharp silhouette so the magenta can be keyed out cleanly.
```

> ชิ้นที่มีชมพู/ม่วงในตัว → เปลี่ยนคำ `MAGENTA (pure #FF00FF)` เป็น `GREEN (pure #00FF00)`

## 🔲 9-SLICE addendum (แปะต่อ — สำหรับ panel/button/bar/frame ที่ต้องยืด)

```
Designed for 9-slice UI scaling: a rounded rectangle with the SAME decorated
border thickness on all four sides, identical corners, and a SIMPLE FLAT even
center area (no big picture or focal detail in the middle, so the middle can
stretch cleanly). Symmetric left-right and top-bottom.
```

---

# 1) PANELS — พาเนล (3 ขนาด × 3 สี)

> พื้นหลังกล่อง UI. **9-slice** ทุกอัน. สี = บทบาท: ฟ้า(หลัก/ข้อมูล) / ไม้-น้ำตาล(รอง/เนื้อหา) / เข้ม(overlay/premium)

## Template
```
[UI STYLE BLOCK]
[9-SLICE addendum]
[CHROMA addendum]
The asset is a [SIZE] fantasy game PANEL: a rounded-rectangle [COLOR] with a
[BORDER]. Flat even interior. Front flat 2D.
```

| # | [SIZE] | [COLOR] | [BORDER] |
|---|---|---|---|
| 1A หลักใหญ่ | large tall panel | soft sky-blue interior | thick brown wood frame with gold corner caps and small rivets |
| 1B หลักกลาง | medium panel | soft sky-blue interior | brown wood frame, gold trim line |
| 1C รองใหญ่ | large panel | warm parchment/wood-brown interior | dark wood frame with metal corner brackets |
| 1D รองกลาง | medium panel | warm parchment interior | simple rounded wood border |
| 1E overlay ใหญ่ | large panel | dark slate blue-grey interior (semi-dark popup) | steel frame with gold rivets |
| 1F premium กลาง | medium panel | deep purple interior | ornate gold frame with a gem stud on top |

> "3 สี" = ฟ้า(1A/1B) · น้ำตาลไม้(1C/1D) · เข้ม/พรีเมียม(1E/1F). "3 ขนาด" = ปรับคำ large/medium/small ได้ตามใช้

---

# 2) TOP BAR & BARS — แถบบน / แถบ

## 2A — Top resource bar (แถบทรัพยากรบนสุด)
```
[UI STYLE BLOCK]
[9-SLICE addendum]
[CHROMA addendum]
The asset is a horizontal fantasy game TOP BAR strip: a long rounded steel-and-
wood bar with gold trim, a slightly raised middle plaque area, small rivets at
the ends. Flat, front-facing, meant to hold currency counters. Simple flat
center so it can stretch. Front flat 2D.
```

## 2B — Currency pill (ช่องใส่เลขทรัพยากร)
```
[UI STYLE BLOCK]
[CHROMA addendum]
The asset is a small rounded fantasy game COUNTER PILL: a dark rounded capsule
with a gold rim and a round socket on the left for an icon, and a little "+"
plus-button circle on the right. Flat front 2D.
```

## 2C — Tab bar slot (แถบเมนูล่าง / แท็บ)
```
[UI STYLE BLOCK]
[9-SLICE addendum]
[CHROMA addendum]
The asset is a fantasy game BOTTOM TAB BAR segment: a warm wood plank strip with
a raised rounded selected-tab block (brighter, gold-trimmed) — provide the tab
block empty (no icon). Front flat 2D.
```

---

# 3) BUTTONS — ปุ่ม (หลายสี/แบบ)

> **9-slice** ทุกปุ่ม (ยืดความกว้างตามข้อความ). ทำทั้ง **หน้าปกติ** (ถ้าจะทำ pressed ให้ gen ซ้ำ สีเข้มลง)

## Template
```
[UI STYLE BLOCK]
[9-SLICE addendum]
[CHROMA addendum]
The asset is a fantasy game BUTTON: a rounded glossy [COLOR] pill button with a
soft top highlight, a darker bottom rim (subtle 3D bevel), a thin gold outline.
Empty face (no icon, no text). Front flat 2D.
```

| # | [COLOR] | ใช้ทำอะไร |
|---|---|---|
| 3A | green | ยืนยัน/เล่น/ซื้อได้ |
| 3B | blue | ปุ่มรอง/ข้อมูล |
| 3C | gold-yellow | premium/พิเศษ |
| 3D | red | ปิด/ยกเลิก/อันตราย |
| 3E | grey (desaturated, flat) | ปุ่มปิดใช้งาน (disabled) |
| 3F | purple | gem/ร้านค้า |

## 3G — Icon button กลม (ปุ่มไอคอนวงกลม เช่น + / ปิด / settings)
```
[UI STYLE BLOCK]
[CHROMA addendum]
The asset is a small ROUND fantasy game icon button: a glossy circular button
with gold rim and a soft highlight, empty face (no symbol). Provide it in green,
and note it can be recolored. Front flat 2D.
```

---

# 4) HEADER BG — ป้ายหัวข้อ

## 4A — Title banner (ป้ายชื่อหน้าจอ เช่น "Shop")
```
[UI STYLE BLOCK]
[9-SLICE addendum]
[CHROMA addendum]
The asset is a fantasy game TITLE BANNER header: a horizontal steel-blue ribbon
plaque with gold trim, small diamond-shaped gem studs on the left and right
ends, slightly curved top, hanging-sign feel. Empty center (no text) and simple
so the middle can stretch. Front flat 2D.
```

## 4B — Section header (ป้ายหัวข้อย่อยในพาเนล เช่น "Free Items")
```
[UI STYLE BLOCK]
[9-SLICE addendum]
[CHROMA addendum]
The asset is a smaller fantasy game SECTION HEADER strip: a rounded wood ribbon
with a lighter cloth banner center and small rope ties at the ends. Empty center.
Front flat 2D.
```

---

# 5) BARS — หลอด (loading / stat / hp / mana)

> **9-slice แนวนอน** (ยืดความยาว). แต่ละหลอด = ทำ **2 ชิ้น: กรอบเปล่า (frame)** + **แถบเติม (fill)** แยกกัน จะได้ปรับ fillAmount ในเกม

## 5A — Bar frame (กรอบหลอด ใช้ร่วมทุกหลอด)
```
[UI STYLE BLOCK]
[9-SLICE addendum]
[CHROMA addendum]
The asset is an EMPTY fantasy game BAR FRAME: a long rounded dark slot with a
steel rim and inner shadow, empty inside (a hollow track to be filled). Front
flat 2D.
```

## 5B — Bar fill (แถบเติม — ทำหลายสี)
```
[UI STYLE BLOCK]
[9-SLICE addendum]
[CHROMA addendum]
The asset is a rounded glossy [COLOR] BAR FILL: a bright candy-glossy horizontal
fill bar with a soft top shine stripe, uniform along its length so it can
stretch. Front flat 2D.
```

| # | [COLOR] | ใช้ |
|---|---|---|
| HP | red→green gradient (or plain green) | เลือด (ไล่สีทำใน shader/BarColorSO ได้ ทำ fill ขาว/เขียวก็พอ) |
| Mana | blue | มานา |
| Shield | light cyan/white | โล่ |
| XP/Loading | gold-yellow | โหลด/ค่าประสบการณ์ |
| Stat | orange | ค่าสเตตัสทั่วไป |

> 💡 ถ้าจะใช้ระบบ `BarColorSO` (gradient) ที่เราทำไว้ → gen fill เป็น **สีขาวล้วน glossy** แล้วให้เกมคูณสี gradient เอง = ใช้ fill เดียวทุกหลอด

---

# 6) ICON SET — ไอคอน (sheet เดียว ให้สไตล์ตรงกัน)

> ทำเป็น **sheet ตาราง** เพื่อให้ **สไตล์/สเกล/แสงตรงกันทั้งชุด** (gen ทีละอันจะเพี้ยน) แล้วตัดทีละอัน

## 6A — Core icon sheet (ไอคอนหลัก ×16)
```
[UI STYLE BLOCK]
The asset is an ICON SHEET for a fantasy game: a grid of 4 columns x 4 rows =
16 SQUARE icons, all the same size, evenly spaced with clear gaps, nothing
overlapping. VIBRANT TOON style — every icon is BRIGHT, SATURATED, glossy with
smooth gradient shading and a bold clean DARK OUTLINE (Brawl Stars / Clash
Royale icon look), chunky rounded and juicy. NOT gritty, NOT dull metal, NOT
desaturated. All 16 icons share the exact same art style, thickness, outline,
lighting and glossy finish so they look like one matched set and read clearly at
small size. Pure flat WHITE background (icons will be cut out individually). No
text, no numbers, no frame.
The 16 icons, left to right, top row first:
1. GOLD COIN — a bright glossy gold coin, and the raised symbol in the CENTER is
   a stylized letter "S" shaped like a LIGHTNING BOLT (an S-shaped thunderbolt,
   the game's emblem), embossed in the gold.
2. GEM (green cut gemstone / emerald).
3. DIAMOND (pink/magenta cut diamond gem).
4. HEART (glossy red health heart).
5. MANA (glossy blue teardrop / mana drop).
6. WOODEN CHEST (closed treasure chest with metal bands).
7. LOOT BAG (brown drawstring sack with a gold coin peeking out).
8. KEY (ornate gold key).
9. STAR (glossy gold star).
10. TROPHY (gold victory cup).
11. SPELLBOOK (a closed rune book with a glowing gem on the cover).
12. SCROLL (rolled parchment scroll with a ribbon).
13. POTION (round flask with colored liquid).
14. SWORD (short stylized blade, hilt up).
15. SHIELD (rounded wooden-and-metal shield).
16. ENERGY (a glossy lightning bolt).
```

## 6B — Extra icon sheet (ไอคอนเสริม ×16 — เกมนี้ใช้)
```
[UI STYLE BLOCK]
The asset is an ICON SHEET for a fantasy game: grid 4 columns x 4 rows = 16
SQUARE icons, same size, evenly spaced, no overlap, one matched set. VIBRANT
TOON style — BRIGHT SATURATED, glossy gradient shading, bold clean DARK OUTLINE
(Brawl Stars / Clash Royale look), chunky and juicy, readable at small size.
NOT gritty, NOT dull metal, NOT desaturated. Pure flat WHITE background. No
text, no frame.
The 16 icons, left to right, top row first:
1. PLUS button (green round add button).
2. SETTINGS gear (grey cog).
3. LOCK (closed padlock).
4. CLOCK / TIMER (round hourglass or clock).
5. MAP / area flag (little map with a pin).
6. TOWER (a small cute defense tower icon).
7. MONSTER FOOTPRINT (a green three-toe paw/claw print).
8. MINER PICKAXE (a stubby wooden pickaxe).
9. GOLD MINE NODE (a cluster of gold ore chunks).
10. UPGRADE ARROW (a glowing up-arrow chevron).
11. CARD (a small game card back).
12. FRIEND / social (two little person silhouettes head-and-shoulders).
13. LEADERBOARD (a podium 1-2-3).
14. HEALTH PLUS (a green medkit cross).
15. SPEED (a small winged boot).
16. INFO (a round "i" info badge — leave the letter area blank as a plain disc).
```

> เกมเราใช้: gold, gem, diamond, health, mana, chest, loot, key, spellbook, tower, monster paw, pickaxe, gold node, upgrade, card, timer, map, info, settings — ครบใน 2 sheet นี้

---

# 7) CARD FRAMES — กรอบการ์ด ระดับ 1-5

> กรอบการ์ดมอน/สเปล **rarity 5 ระดับ** — สี + deco **ต่างกันชัดเจน ไล่จากธรรมดา → หรูอลัง**. ทำเป็นกรอบเปล่า (ตรงกลางโปร่ง วางอาร์ตตัวละครทีหลัง)

## Template
```
[UI STYLE BLOCK]
[CHROMA addendum]
The asset is an EMPTY fantasy game CARD FRAME (rarity tier [N]): a vertical
rounded-rectangle trading-card border, [COLOR] with [DECO]. The CENTER is empty
and transparent (hollow — the character art goes there later), only the border/
frame is drawn. A small banner strip at the bottom for a name (leave it empty).
Front flat 2D, symmetric.
```

| Tier | [COLOR] | [DECO] (ต่างกันชัด) |
|---|---|---|
| **1 Common** | grey stone / plain wood | เรียบสุด ขอบหินเทา ไม่มีลาย มุมตัดตรงๆ |
| **2 Uncommon** | green + bronze | ขอบทองแดง มีใบไม้เล็กที่มุมล่าง |
| **3 Rare** | blue + silver | ขอบเงินมันวาว มีอัญมณีฟ้าเม็ดเล็กบนหัวการ์ด |
| **4 Epic** | purple + gold | ขอบทองสลักลาย มีอัญมณีม่วง 2 เม็ดข้างบน เรืองอ่อน |
| **5 Legendary** | fiery gold / orange | ขอบทองอลังหรูสุด ปีก/เขาโลหะที่หัวการ์ด อัญมณีใหญ่กลางหัว ประกายทอง |

> ไล่ระดับให้ **"ยิ่งสูง ยิ่งมี deco เพิ่ม + วาว/เรืองขึ้น"** — วางเรียงกันต้องดูออกทันทีว่าอันไหนหายากกว่า

---

# 8) BACKGROUNDS — พื้นหลังฉาก (เต็มจอ — ไม่ใช้ chroma)

> ภาพเต็ม ไม่ต้องคีย์. **แนวตั้ง (portrait mobile)**. เบลอ/หม่นพอให้ UI ด้านหน้าอ่านออก (พื้นหลัง = ฉาก ไม่แย่งสายตา — หลักเดียวกับกฎ muted-ground)

## 8A — Main hub background (พื้นหลังหลัก เช่นจอ Shop/Heroes)
```
[UI STYLE BLOCK]
A full vertical PORTRAIT background scene for a fantasy game menu (shop / hub):
a cozy stylized fantasy marketplace / camp — wooden stalls, hanging lanterns,
banners, crystals and treasure. GLOBAL NEUTRAL palette matching the UI: cool
blue-grey sky and tones with warm wood accents and gold highlights (NOT a green
forest, NOT tied to any faction). Composition kept SIMPLE and slightly SOFT/
BLURRED with muted mid-tones and low contrast in the middle area so bright UI
panels placed on top stay readable. Top has a bit more detail (sky, tents), the
lower-middle is calmer. No characters in the exact center. No text, no UI, no
watermark. Portrait 9:16.
```

## 8B — Secondary / pattern background (พื้นหลังรอง — จอย่อย/หลัง panel)
```
[UI STYLE BLOCK]
A full vertical PORTRAIT plain background for a fantasy game sub-screen: a
smooth soft blue gradient with a very subtle faint diamond / damask pattern,
low contrast, even and calm across the whole image so UI reads clearly on top.
Slightly darker at the edges (gentle vignette). No objects, no characters, no
text. Portrait 9:16.
```

---

## 🟢 Unity import settings

**ชิ้น UI (คีย์ chroma แล้ว):**
| ช่อง | ค่า |
|---|---|
| Texture Type | **Sprite (2D and UI)** |
| Mesh Type | Full Rect |
| Wrap Mode | Clamp |
| Filter Mode | Bilinear |
| Compression | มือถือ: ASTC |
| **Sprite Editor → Border** | ตั้ง L/T/R/B ให้มุม/ขอบไม่ยืด (9-slice) |
| Image component | **Image Type = Sliced** |

**พื้นหลังเต็มจอ (หมวด 8):** Sprite, ไม่ต้อง border, ใช้ Canvas เต็มจอ

**คีย์สีชมพูใน Photoshop (ก่อนเข้า Unity):** Select > Color Range เลือก magenta → ลบ → **Layer > Matting > Defringe 1px** (ลบขอบชมพูตกค้าง) → export PNG โปร่ง

---

## 📝 Checklist

- [ ] หน้าตรง แบน ไม่มี perspective / ไม่มีเงาทอดพื้น
- [ ] ชิ้นยืดได้: ขอบ 4 ด้านเท่ากัน + ตรงกลางเรียบ (9-slice ได้)
- [ ] พื้น chroma แบนล้วน (magenta / เขียวถ้าตัวมีชมพู) แยกจากตัวชิ้นชัด
- [ ] icon: ชุดเดียวสไตล์/สเกลตรงกัน อ่านออกตอนย่อเล็ก
- [ ] card 1-5: ไล่ deco/ความวาวชัด ดูออกว่าอันไหนหายากกว่า
- [ ] bg: หม่น/เบลอกลางจอพอให้ UI อ่านออก
- [ ] เก็บขอบ chroma ใน Photoshop ก่อนเข้า Unity (AI ทำขอบไม่เนียน)

---

## ทำต่อได้

- **popup เฉพาะทาง:** result win/lose, level-up, chest-open — โครง panel เดิม + deco
- **badge/ribbon:** "NEW", "SALE", มุมพับ, ป้าย % ส่วนลด
- **progress node:** map area, quest checkmark
- เผ่าอื่น: UI ชุดสีต่างเผ่า (Human/Galax/Darkside) — STYLE BLOCK เดิม เปลี่ยน accent
