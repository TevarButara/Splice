# Concept Art Prompts — UI 🎛️

> UI สไตล์ **clean modern "official infographic"** (ref: **Pokémon GO event graphics / Miko Graphics / G47ix**) — **กล่องดำโปร่งแสง (glass) มุมโค้ง, เงานุ่มบางๆ, ตัวหนังสือขาวคม, พื้นหลัง cinematic เบลอ**, เรียบหรู สะอาด minimal เข้ากับตัวละคร chibi ของเรา
>
> 🌐 **นี่คือ UI หลัก/กลางของเกม (GLOBAL — ไม่ผูกเผ่าใดเผ่าหนึ่ง)** → ฐานเป็น **neutral ดำ-เทาเข้ม** + **สี accent เดียวสลับได้** (เผ่า/จอไหน = เปลี่ยน accent) **ไม่ล็อกสีเดียวตายตัว**

## 🎯 PALETTE ล็อก (ยึด ref Pokémon GO — เรียบ สะอาด หรู)
> โครง ref = **ฐานมืด neutral + พาเนลดำโปร่งแสง + สี accent 1 สีต่อจอ + ตัวหนังสือขาว + เงา/แสงขอบนุ่ม**
> - **ฐาน (base ทุกจอ):** near-black / charcoal เทาเข้มอมน้ำเงิน (desaturated) — เป็นพื้นกลางที่นิ่ง
> - **พาเนล (panel/card/chip):** **ดำโปร่งแสง 60-80% alpha** + ขอบสว่างบาง 1px (ขาว ~15%) + เงา top-gloss จางๆ (glassmorphism-lite) มุมโค้ง **รัศมีเยอะ**
> - **ACCENT (1 สีต่อจอ/เผ่า — สลับได้):** ใช้กับ header pill / ปุ่มหลัก / timeline / badge / ขอบเรือง. ตัวอย่าง: **เขียวมะนาว** (harvest) · **ชมพู-แดง** (spotlight) · **ส้ม** (raid) · **ม่วง** (debut) · **ฟ้า** (water/ocean)
> - **ตัวหนังสือ:** ขาวล้วน (ทำใน TMP ในเกม — อาร์ตไม่มีตัวอักษร)
> - **"หรู" มาจาก "ความเรียบ + โปร่งแสง + เงานุ่ม + ระยะห่างสวย"** ไม่ใช่ลายเยอะ/ขอบหนา/สีจัด
> - จุดเน้นสี = accent + creature render 3D วาว — ที่เหลือคุมมืดให้นิ่ง อ่านง่าย

## ⚡ STYLE = CLEAN MODERN FLAT+GLOSS (ห้าม cartoon outline หนา / halftone / painterly)
> ที่ต้องการ (ตาม ref) = **flat โมเดิร์น สะอาด + เงา gloss นุ่มบางๆ + ขอบสว่าง rim บาง + มุมโค้งมาก + โปร่งแสง** (แนว UI แอปจริง / Pokémon GO). **ไม่มี** เส้นตัดขอบหนาแบบการ์ตูน, **ไม่มี** halftone dots, **ไม่มี** พู่กัน painterly, **ไม่มี** ไม้-โลหะยุคกลาง. STYLE BLOCK ใหม่บังคับไว้แล้ว → override ทุกจุด

## 🧩 สำคัญ! สไตล์นี้ "ทำในเอนจินได้เลย ไม่ต้อง AI" เยอะมาก
> พาเนล/ปุ่ม/แถบ/ช่อง chip ในสไตล์นี้ = **สี่เหลี่ยมมุมโค้ง + สีล้วน + alpha + เงานุ่ม** ซึ่ง **ทำใน Unity ตรงๆ ได้เลย** (Image + rounded sprite 9-slice + ปรับ Color/Alpha + soft shadow) — คมกว่า เบากว่า และเปลี่ยนสี accent ได้ runtime
> - **ไม่ต้อง AI:** panel, button, bar frame/fill, tab, currency pill, chip slot, timeline pill → ทำใน Unity/Figma
> - **ใช้ AI:** icon (ไอเทม/สกุลเงินวาวๆ), creature render, badge/emblem ที่มีดีเทล, **พื้นหลัง cinematic** (หมวด 8)
> - ถ้าจะ gen panel/ปุ่มด้วย AI ก็ได้ แต่ **gen ทึบแล้วมาลด alpha ในเกม** — อย่าพยายาม chroma-key ทะลุของโปร่งแสง (คีย์ไม่ออกสวย)

## 💎 ตัวช่วยที่ได้ผลกว่าคำพูดเยอะ — แนบภาพ ref!
> คำ prompt อย่างเดียว**คุมสไตล์เป๊ะยาก** — แนบภาพ ref (Pokémon GO / Miko Graphics ที่ให้มา) เข้า generator เป็น **"style reference / image reference"** แล้วบอก *"match this exact clean modern game UI style"*
> - ChatGPT/DALL·E: แนบ ref + *"in the exact clean UI art style of this reference"*
> - Midjourney: `--sref <url ภาพ ref>` หรือใส่ ref เป็น image prompt
> - SDXL ฯลฯ: IP-Adapter / style reference
> **แนบ ref + STYLE BLOCK คู่กัน = ตรงสุด**

## 🚫 NEGATIVE prompt (ใส่ในช่อง negative ถ้าเครื่องมือมี)
```
thick cartoon outline, sticker outline, halftone dots, comic, painterly, oil
painting, brush texture, sketchy, realistic, photorealistic, gritty, grunge,
muddy, desaturated, wood texture, metal frame, medieval, rustic, ornate, busy,
cluttered, 3d render clay, blurry, text, watermark
```

## 🔰 EMBLEM ของเกม
> โลโก้กลาง = **ตัว "S" ทรงสายฟ้า** (S-shaped lightning bolt) ย่อจากชื่อเกม **Splice** — ใช้บนเหรียญ gold, มุมจอ (แบบ tab โลโก้ Pokémon GO), loading, app icon

---

## workflow: Chroma / alpha (สไตล์นี้ต่างจากเดิม)

**หลักคิด:** สไตล์นี้ "แบน+โปร่งแสง" → เน้น **ทำในเอนจิน / gen แบบมี alpha** มากกว่าคีย์พื้น

**ชิ้นทึบ (icon/badge/creature/ปุ่มถ้า gen)** → gen บนพื้น chroma **เขียว `#00FF00`** (ธีมนี้ accent หลากสี รวมม่วง/ชมพู → เขียวปลอดภัยสุด) → คีย์ออก → เก็บขอบ Photoshop 1px

**ชิ้นโปร่งแสง (glass panel/chip)** → **อย่า chroma-key** (คีย์ทะลุของใสไม่ได้) → เลือก 1 ใน 2:
- **(แนะนำ)** ทำใน Unity: rounded-rect sprite ขาว 9-slice → ใส่ Color ดำ + Alpha 0.6-0.8 → ซ้อน rim/gloss
- gen เป็น panel **ทึบ** บนพื้นเขียว → คีย์ → ในเกมค่อยลด alpha

**พื้นหลังเต็มจอ (หมวด 8)** = ภาพเต็ม ไม่ต้องคีย์

---

## วิธีใช้

1. **1 prompt = 1 ชิ้น** — copy `UI STYLE BLOCK` + prompt ของชิ้นนั้น
2. สั่งออก **หน้าตรง แบน 2D (orthographic, no perspective)** อยู่กลางเฟรม
3. ชิ้นยืดได้ → **ขอบ/มุมเท่ากันทุกด้าน + ตรงกลางเรียบ** (9-slice)
4. **icon set** ทำเป็น **sheet** (สไตล์/สเกลตรงกันทั้งชุด)
5. เช็คก่อน gen: ชิ้นนี้ **ทำใน Unity ได้เลยไหม?** (panel/ปุ่ม/แถบเรียบๆ = ทำเอง ไม่ต้อง AI)

---

## 🎨 UI STYLE BLOCK (แปะหน้า prompt ทุกชิ้น)

```
Clean MODERN MOBILE GAME UI asset, sleek official-app / Pokémon GO infographic
style. Front-facing flat 2D view (straight on, no perspective, no cast shadow on
a floor). FLAT design with a soft subtle GLOSS: smooth gentle gradient, a thin
bright RIM-LIGHT along the top edge, a soft inner top sheen, and a very soft drop
shadow for depth. LARGE ROUNDED corners. NO thick cartoon outline, NO halftone
dots, NO heavy ornament — minimal, calm, premium, lots of clean negative space.
Colors are refined and slightly muted in the base with ONE punchy ACCENT hue for
emphasis. Reads clearly at small size.
STYLE MUST BE: clean, modern, flat-with-soft-gloss, glassy, minimal, sleek,
high-quality UI, elegant.
STYLE MUST NOT BE: thick outline, sticker, halftone, comic, painterly, sketchy,
realistic, gritty, muddy, wood-and-metal medieval, ornate, cluttered.
GLOBAL NEUTRAL theme: a dark charcoal / near-black base with translucent panels
and a single ACCENT color [ACCENT] (swappable). Centered, generous even margin,
fully inside frame. No text, no letters, no numbers, no logo, no watermark.
```

> `[ACCENT]` = เลือกต่อจอ: `lime green` / `pink-red` / `orange` / `purple` / `sky blue` (ค่าเริ่มต้น neutral = `sky blue` หรือ `purple`)

## 🟩 CHROMA addendum (เฉพาะชิ้น "ทึบ" — icon/badge/creature)

```
The element sits on a PERFECTLY FLAT solid GREEN background (pure #00FF00,
chroma key green-screen style), a single even color with NO gradient, NO
texture, NO shadow on the background. The element does not touch the frame
edges. Clean sharp silhouette so the green can be keyed out cleanly.
```

> ⚠️ **ชิ้นโปร่งแสง (glass panel/chip) ไม่ใช้ addendum นี้** — ทำใน Unity หรือ gen ทึบแล้วลด alpha

## 🔲 9-SLICE addendum (สำหรับ panel/button/bar/frame ที่ต้องยืด)

```
Designed for 9-slice UI scaling: a rounded rectangle with the SAME thin edge
treatment on all four sides, identical corners, and a SIMPLE FLAT even center
area (no picture or focal detail in the middle, so it can stretch cleanly).
Symmetric left-right and top-bottom.
```

---

# 1) PANELS — พาเนล (glass — ส่วนใหญ่ทำใน Unity ได้)

> กล่อง UI โปร่งแสงมุมโค้ง. **9-slice** ทุกอัน. 🧩 พวกนี้ทำใน Unity ตรงๆ ดีกว่า (rounded sprite + Color/Alpha) — prompt ไว้เผื่อ gen

## Template
```
[UI STYLE BLOCK]
[9-SLICE addendum]
The asset is a [SIZE] modern game PANEL: a rounded-rectangle translucent
DARK-CHARCOAL glass card with LARGE rounded corners, a thin bright rim-light on
the top edge, a soft top sheen, and a soft outer drop shadow. [ACCENT_DETAIL].
Flat even interior. Front flat 2D.
```

| # | [SIZE] | [ACCENT_DETAIL] |
|---|---|---|
| 1A หลักใหญ่ | large tall panel | เรียบ ไม่มี accent (พื้นเนื้อหา) |
| 1B หลักกลาง | medium panel | เรียบ ไม่มี accent |
| 1C มี header accent | medium panel | a thin ACCENT-color bar across the very top edge (header strip) |
| 1D info box (มีสี) | small wide panel | the whole card tinted with a translucent ACCENT color (like the blue/purple info boxes in the ref) |
| 1E overlay ใหญ่ | large panel | darker, more opaque (popup dim) |
| 1F chip slot | small square | a small rounded-square translucent slot to hold ONE icon/creature (grid cell) |

> "ขนาด" ปรับคำ large/medium/small ได้. accent = สลับสีต่อจอ

---

# 2) TOP BAR & BARS — แถบบน / แถบ (🧩 ทำใน Unity ได้)

## 2A — Top resource bar
```
[UI STYLE BLOCK]
[9-SLICE addendum]
The asset is a horizontal modern game TOP BAR: a slim rounded translucent
dark-charcoal glass strip with a thin top rim-light, meant to hold currency
counters. Very clean, simple flat center so it can stretch. Front flat 2D.
```

## 2B — Currency pill
```
[UI STYLE BLOCK]
The asset is a small modern game COUNTER PILL: a rounded dark translucent capsule
with a thin rim-light, a round socket on the left for an icon, and a small ACCENT
"+" plus-button circle on the right. Clean and minimal. Front flat 2D.
```

## 2C — Tab (2 states)
```
[UI STYLE BLOCK]
[9-SLICE addendum]
The asset is a modern game TAB: a rounded pill; provide TWO states — an
unselected dark translucent tab, and a SELECTED tab filled with the solid ACCENT
color and a soft glow. Provide the tab empty (no icon). Front flat 2D.
```

## 2D — Timeline pill (แถบเวลา start … end แบบ ref)
```
[UI STYLE BLOCK]
The asset is a modern game TIMELINE / SCHEDULE pill: a small rounded dark
capsule containing a colored status DOT on the left (provide a GREEN "start" dot
version and a RED "end" dot version), a divider, and space for a date + time.
Clean, minimal, glassy. Front flat 2D.
```

---

# 3) BUTTONS — ปุ่ม (🧩 ทำใน Unity ได้)

> **9-slice**. pill มุมโค้ง สีล้วน เงานุ่มบางๆ minimal (ไม่มีขอบหนา). pressed = gen/ปรับสีเข้มลง

## 3-SHEET — ปุ่มทุกสีในแผ่นเดียว (gen ทีเดียว แล้วตัดเอา) ⭐
> **แผ่นเดียวจบ** — สไตล์/แสง/ทรง/สเกล ตรงกันทุกปุ่ม (ดีกว่า gen ทีละสีที่มักเพี้ยน). ตัดทีละอันไปทำ 9-slice
```
[UI STYLE BLOCK]
The asset is a BUTTON SHEET for a modern mobile game: a grid of 2 columns x 4
rows = 8 identical-shape BUTTONS on a pure flat WHITE background, evenly spaced
with clear gaps, nothing overlapping, all the SAME size and shape. Each button is
a horizontal rounded PILL with LARGE rounded corners, a soft top sheen (glossy
highlight along the top), a very subtle darker bottom edge for depth, and a soft
drop shadow. NO thick outline, clean and minimal, sleek Pokémon-GO / modern-app
style. All 8 share the exact same shape, lighting, gloss and finish so they look
like one matched set — ONLY the fill color differs. Each button face is EMPTY
(no icon, no text, no numbers).
The 8 buttons, left to right, top row first:
1. GREEN button (positive / buy / confirm).
2. SKY-BLUE button (secondary / info).
3. PURPLE button (accent / main action).
4. ORANGE button (accent alt).
5. PINK-RED button (accent alt / hot).
6. RED button (cancel / danger).
7. GOLD-YELLOW button (premium / special).
8. GREY muted flat button (disabled — slightly darker, low saturation, no gloss).
Pure flat WHITE background so each button can be cut out. No text, no frame.
```

> **หมายเหตุ 9-slice:** ปุ่มบนแผ่นสั้นๆ ไม่เป็นไร — ตอนตัดมา ให้ตั้ง Sprite Border ซ้าย/ขวาให้ **มุมโค้งไม่ยืด** แล้ว Image Type = Sliced จะยืดกลางได้ตามข้อความ. ถ้าห่วงมุมโค้งเพี้ยนตอนยืดยาว → เติมท้าย prompt ว่า *"make each pill wide with a long flat straight center section"*
>
> อยากได้ **สถานะ pressed** ครบชุด → gen แผ่นที่ 2 ด้วย prompt เดิม เติม *"darker pressed-down state, no top sheen, pushed-in look"*

## Template (เผื่ออยาก gen ทีละสี)
```
[UI STYLE BLOCK]
[9-SLICE addendum]
The asset is a modern game BUTTON: a rounded pill filled with a solid [COLOR],
LARGE rounded corners, a soft top sheen and a very subtle darker bottom edge for
depth, a soft drop shadow. No thick outline. Empty face (no icon, no text).
Front flat 2D.
```

| # | [COLOR] | ใช้ทำอะไร |
|---|---|---|
| 3A | ACCENT (theme color) | ปุ่มหลัก/ยืนยัน/เล่น |
| 3B | dark translucent glass | ปุ่มรอง/ข้อมูล |
| 3C | green | สำเร็จ/ซื้อได้/CTA เชิงบวก |
| 3D | red | ปิด/ยกเลิก/อันตราย |
| 3E | grey (muted, flat) | ปุ่มปิดใช้งาน (disabled) |
| 3F | gold-yellow | premium/พิเศษ (เช่นวันที่ในกล่องเหลืองใน ref) |

## 3G-SHEET — ปุ่มกลมทุกสีในแผ่นเดียว (gen ทีเดียว แล้วตัดเอา) ⭐
> ปุ่มไอคอนวงกลม (+, ✕, ✓, settings ฯลฯ) — **แผ่นเดียวจบ** สไตล์/แสง/สเกลตรงกัน หน้าว่างไว้ใส่ไอคอนในเกม
```
[UI STYLE BLOCK]
The asset is a ROUND-BUTTON SHEET for a modern mobile game: a grid of 2 columns x
4 rows = 8 identical-shape CIRCULAR buttons on a pure flat WHITE background,
evenly spaced with clear gaps, nothing overlapping, all the SAME size. Each is a
clean glossy CIRCLE button with a soft top sheen and a soft drop shadow, NO thick
outline, sleek Pokémon-GO / modern-app style. All 8 share the exact same shape,
lighting, gloss and finish so they look like one matched set — ONLY the fill
color differs. Each button face is EMPTY (no symbol, no icon, no text).
The 8 buttons, left to right, top row first:
1. GREEN. 2. SKY-BLUE. 3. PURPLE. 4. ORANGE. 5. PINK-RED. 6. RED. 7. GOLD-YELLOW.
8. GREY muted flat (disabled — darker, low saturation, no gloss).
Pure flat WHITE background so each button can be cut out. No text, no frame.
```

> pressed state → gen แผ่น 2 เติม *"darker pressed-down state, no top sheen, pushed-in look"*

## 3H — Corner logo tab (แถบโลโก้มุมบน แบบ Pokémon GO)
```
[UI STYLE BLOCK]
The asset is a modern game CORNER TAB / bookmark ribbon that sits at the TOP-
RIGHT, a rounded rectangle in the ACCENT color hanging from the top edge, clean
with a soft shadow, meant to hold a small logo (leave it empty). Front flat 2D.
```

---

# 4) HEADER BG — ป้ายหัวข้อ (🧩 ทำใน Unity ได้)

## 4A — Section header strip
```
[UI STYLE BLOCK]
[9-SLICE addendum]
The asset is a modern game SECTION HEADER: a slim rounded translucent bar with a
thin ACCENT-color underline or a small ACCENT block on the left, very clean, lots
of space for a title. Empty center. Front flat 2D.
```

## 4B — Badge / status chip (เช่น "+BONUS", tier number, ประเภท)
```
[UI STYLE BLOCK]
[CHROMA addendum]
The asset is a small modern game BADGE chip: a tiny rounded pill or circle in a
solid ACCENT color with a soft glow, clean and minimal, to hold a short label or
a single number (leave it empty). Front flat 2D.
```

---

# 5) BARS — หลอด (🧩 ทำใน Unity ได้)

> **9-slice แนวนอน**. แต่ละหลอด = **กรอบเปล่า (frame)** + **แถบเติม (fill)** แยกกัน

## 5A — Bar frame
```
[UI STYLE BLOCK]
[9-SLICE addendum]
The asset is an EMPTY modern game BAR FRAME: a slim rounded dark translucent
slot with a subtle inner shadow, empty inside (a hollow track to be filled).
Clean, minimal. Front flat 2D.
```

## 5B — Bar fill
```
[UI STYLE BLOCK]
[9-SLICE addendum]
The asset is a rounded glossy [COLOR] BAR FILL: a clean horizontal fill bar with
a soft top sheen, uniform along its length so it can stretch. Front flat 2D.
```

| # | [COLOR] | ใช้ |
|---|---|---|
| HP | red→green gradient (or plain green) | เลือด |
| Mana | blue | มานา |
| Shield | light cyan/white | โล่ |
| XP/Loading | ACCENT / gold | โหลด/ค่าประสบการณ์ |
| Stat | orange | ค่าสเตตัสทั่วไป |

> 💡 ใช้ `BarColorSO` → gen fill **ขาวล้วน glossy** แล้วให้เกมคูณสี = fill เดียวทุกหลอด

---

# 6) ICON SET — ไอคอน (sheet เดียว ให้สไตล์ตรงกัน — ใช้ AI)

> ทำเป็น **sheet ตาราง** ให้สไตล์/สเกล/แสงตรงกันทั้งชุด แล้วตัดทีละอัน. สไตล์นี้ = **ไอคอน glossy สะอาด มินิ-3D นุ่ม** (ไม่มีขอบตัดหนา) แบบไอเทมใน Pokémon GO

## 6A — Core icon sheet (ไอคอนหลัก ×16)
```
[UI STYLE BLOCK]
The asset is an ICON SHEET for a modern mobile game: a grid of 4 columns x 4 rows
= 16 SQUARE icons, all the same size, evenly spaced with clear gaps, nothing
overlapping. CLEAN GLOSSY mini-3D style — each icon is smooth, softly shaded with
a gentle gloss and a soft highlight, refined colors, NO thick outline, NO
halftone, readable at small size (like Pokémon GO item icons). All 16 share the
exact same clean style, lighting and finish so they look like one matched set.
Pure flat WHITE background (icons will be cut out individually). No text, no
numbers, no frame.
The 16 icons, left to right, top row first:
1. GOLD COIN — a smooth glossy gold coin, and the raised symbol in the CENTER is
   a stylized letter "S" shaped like a LIGHTNING BOLT (the game's emblem).
2. GEM (a clean faceted premium gem — violet/blue).
3. ORB (a smooth glossy soft-currency crystal ball).
4. HEART (a clean glossy red health heart).
5. MANA (a smooth blue teardrop / mana drop).
6. LOOT BOX (a clean modern mystery gift box).
7. LOOT BAG (a smooth pouch with a coin peeking out).
8. KEY (a clean glossy key).
9. STAR (a smooth glossy gold star).
10. TROPHY (a clean glossy gold cup).
11. GIFT (a wrapped present with a bow).
12. TICKET / PASS (a clean event ticket card, like the "Ticket of Treats" in the
    reference).
13. POTION (a smooth flask with bright liquid).
14. CANDY (a glossy round pokémon-go-style candy).
15. SHIELD (a clean rounded shield).
16. ENERGY (a smooth glossy lightning bolt).
```

## 6B — Extra / UI-glyph icon sheet (ไอคอนเสริม ×16 — เกมนี้ใช้)
```
[UI STYLE BLOCK]
The asset is an ICON SHEET for a modern mobile game: grid 4 columns x 4 rows = 16
SQUARE icons, same size, evenly spaced, no overlap, one matched set. CLEAN
minimal style — smooth flat glyphs with a soft gloss, refined, NO thick outline,
NO halftone, readable at small size. Some are simple WHITE line/solid UI glyphs
(clean app-icon look), some are small glossy objects. Pure flat WHITE background.
No text, no frame.
The 16 icons, left to right, top row first:
1. PLUS (a clean round add button).
2. SETTINGS gear (a clean cog glyph).
3. LOCK (a clean padlock glyph).
4. CLOCK / TIMER (a clean clock glyph).
5. MAP / area pin (a clean map-pin glyph).
6. TOWER (a small clean defense-tower icon).
7. MONSTER PAW (a clean three-toe paw print).
8. MINER PICKAXE (a clean pickaxe glyph).
9. GOLD NODE (a small glossy gold-ore cluster).
10. UPGRADE ARROW (a clean up-chevron glyph).
11. CARD (a clean card glyph).
12. FRIEND / social (two clean person silhouettes).
13. LEADERBOARD (a clean podium 1-2-3 glyph).
14. BINOCULARS (a clean binoculars glyph, like the "nearby" icon in the ref).
15. SPEED (a clean winged-boot glyph).
16. INFO (a round "i" info badge — leave the letter area blank).
```

> เกมเราใช้: gold, gem, orb, health, mana, loot box, loot bag, key, ticket, candy, tower, monster paw, pickaxe, gold node, upgrade, card, timer, map, binoculars, info, settings — ครบใน 2 sheet

---

# 7) CARD FRAMES — กรอบการ์ด ระดับ 1-5

> rarity 5 ระดับ แบบ **สะอาด** — ต่างกันด้วย **สี accent + ความเรือง/glow** ไม่ใช่ deco ไม้/โลหะรก. กรอบเปล่า (กลางโปร่ง วางอาร์ตทีหลัง)

## 7-SHEET — การ์ด 5 tier เรียงแผ่นเดียว (gen ทีเดียว เทียบ rarity ง่าย) ⭐
> **แผ่นเดียวจบ** — ทรง/สเกล/แสงตรงกัน ต่างแค่ rim+glow ไล่ระดับ ดูออกทันทีว่าอันไหนหายาก. ตัดทีละใบไปใช้
```
[UI STYLE BLOCK]
The asset is a CARD-FRAME SHEET for a modern mobile game: 5 EMPTY vertical
trading-card frames in a single ROW, side by side, evenly spaced with clear gaps,
all the SAME size and shape. Each is a rounded-rectangle card border with a dark
glass base and a clean colored rim; the CENTER of every card is EMPTY and
transparent / flat white (hollow — character art goes there later), only the
clean border is drawn, with a small empty name strip at the bottom. All 5 share
the exact same clean minimal shape and lighting — they differ ONLY by rim color
and glow strength, rising in rarity left to right:
1. COMMON — grey rim, no glow, plainest.
2. UNCOMMON — green rim, faint soft green glow.
3. RARE — blue rim, soft blue glow, slightly brighter edges.
4. EPIC — purple rim, glowing purple aura, brighter rim.
5. LEGENDARY — gold / rainbow-gradient rim, strong warm gold glow all around,
   the most premium.
Pure flat WHITE background so each card can be cut out. Front flat 2D, symmetric,
minimal. No text, no numbers, no character art inside.
```

## Template (เผื่ออยาก gen ทีละใบ)
```
[UI STYLE BLOCK]
[CHROMA addendum]
The asset is an EMPTY modern game CARD FRAME (rarity tier [N]): a vertical
rounded-rectangle card border, dark glass base with a clean [COLOR] rim and
[GLOW]. The CENTER is empty and transparent (hollow — character art goes there
later), only the clean border is drawn. A small strip at the bottom for a name
(leave it empty). Front flat 2D, symmetric, minimal.
```

| Tier | [COLOR] rim | [GLOW] |
|---|---|---|
| **1 Common** | grey | ไม่มี glow เรียบสุด |
| **2 Uncommon** | green | glow เขียวจางบางๆ |
| **3 Rare** | blue | glow ฟ้านุ่ม + มุมมนสว่าง |
| **4 Epic** | purple | glow ม่วงเรือง + rim สว่างขึ้น |
| **5 Legendary** | gold/rainbow gradient | glow ทองเรืองรอบการ์ด + rim ไล่เฉด premium |

> ไล่ระดับด้วย **"ยิ่งสูง = rim สว่าง + glow แรงขึ้น"** สะอาดๆ ดูออกทันทีว่าอันไหนหายาก

---

# 8) BACKGROUNDS — พื้นหลังฉาก (เต็มจอ — cinematic, ใช้ AI)

> ภาพเต็ม ไม่ต้องคีย์. **แนวตั้ง (portrait mobile)**. cinematic เบลอ bokeh ไล่เฉดตามธีมสี — เข้มพอให้ UI ด้านหน้าอ่านออก (แบบพื้นหลัง Pokémon GO event)

## 8A — Themed cinematic background (พื้นหลังหลัก — ต่อธีมสี)
```
[UI STYLE BLOCK]
A full vertical PORTRAIT cinematic background for a modern mobile game menu,
Pokémon GO event-graphic style: a soft BLURRED scene with warm BOKEH light
orbs and a smooth gradient in the theme ACCENT color [ACCENT] fading into deep
dark at the bottom. Atmospheric, dreamy, out-of-focus, low detail. Composition
kept SIMPLE and calm with low contrast in the middle so translucent UI panels on
top stay readable. Darker toward the edges. No characters in the exact center.
No text, no UI, no watermark. Portrait 9:16.
```

> `[ACCENT]` = green(harvest) / warm brown-pink(spotlight) / orange(raid) / purple(night-debut) / blue(water) — gen ชุดละสีตามธีมจอ

## 8B — Plain gradient background (พื้นหลังรอง — จอย่อย)
```
[UI STYLE BLOCK]
A full vertical PORTRAIT plain background for a modern game sub-screen: a smooth
clean dark gradient (deep charcoal to a slightly lighter ACCENT-tinted top), very
subtle soft bokeh, low contrast, even and calm so UI reads clearly on top.
Gentle vignette at the edges. No objects, no characters, no text. Portrait 9:16.
```

---

# 9) COMBAT / RAID HUD BUTTONS — ปุ่มตอนรบ (sheet เดียว) ⭐

> ปุ่มควบคุมตอนบุก/เรด **รวมแผ่นเดียว** grid 3×3 = 9 ช่อง. **7 ช่องแรก = ปุ่มวงกลม มีสัญลักษณ์ในตัว** (พร้อมกดใช้), **2 ช่องท้าย = glyph เปล่า** (ไอคอนบอกสถานะ ไม่ใช่ปุ่ม). สไตล์ dark-glass เดียวกับ ref ไอคอน — วงกลมกระจกดำ + สัญลักษณ์ขาว + ริง accent ตามหน้าที่

```
[UI STYLE BLOCK]
[CHROMA addendum]
The asset is a COMBAT HUD BUTTON SHEET for a modern mobile game (clean Pokémon-GO
/ app style): a grid of 3 columns x 3 rows = 9 items on a pure flat GREEN
background (#00FF00 chroma, so each can be cut out), evenly spaced with clear
gaps, nothing overlapping. Items 1–7 are all the SAME circular button: a glossy
dark-charcoal glass CIRCLE with a soft top sheen, a soft drop shadow, a thin
colored ACCENT ring, and a clean simple WHITE symbol centered on it. Items 8–9
are NOT buttons — they are just clean WHITE glyph icons alone (no circle, no
background). All share the exact same clean minimal lighting and finish so they
look like one matched set, readable at small size. NO thick cartoon outline, NO
halftone, NO text/letters/numbers.
The 9 items, left to right, top row first:
1. LOCK TARGET (enemy) — a targeting reticle / crosshair over a small creature
   silhouette; RED accent ring.
2. LOCK TOWER — a targeting reticle / crosshair over a small tower shape; ORANGE
   accent ring.
3. REBORN / RESPAWN — a circular revive arrow (loop) with an upward spark or a
   phoenix feather; CYAN accent ring.
4. REPAIR — a wrench (or wrench + hammer cross); GREEN accent ring.
5. UPGRADE — an upward chevron / level-up arrow; BLUE accent ring.
6. SELL — a coin with a small outgoing arrow (trade the coin away); GOLD accent
   ring.
7. HOME / RETURN TO BASE — a simple house symbol; SKY-BLUE accent ring.
8. CAMERA SWITCH — a clean white glyph ONLY (no circle): a small camera with two
   curved rotate arrows around it (switch camera angle).
9. TIME REMAINING — a clean white glyph ONLY (no circle): a simple hourglass (or
   a clock) meaning time left.
Pure flat GREEN background so every item can be keyed out. No frame, no text.
```

> **สี accent ring** = โค้ดสีตามหน้าที่ให้แยกออกในสนามรบ (แดง=โจมตี / เขียว=ซ่อม / ฟ้า=อัพ / ทอง=ขาย ฯลฯ) — ฐานวงกลมเป็น dark-glass เหมือนกันหมดจึงยังดูเป็นชุดเดียว
> **glyph 8–9 (กล้อง/เวลา)** ทำเป็นสีขาวล้วนไม่มีวงกลม → เอาไปวางบนปุ่มอื่น/มุมจอ/ข้างตัวเลขเวลาได้อิสระ
> ถ้าจะทำ **สถานะ locked (กำลังล็อกเป้าอยู่)** → gen ซ้ำเติม *"reticle glowing brighter, active locked-on state"*

---

## 🟢 Unity import settings

**ชิ้น UI (ไอคอน/badge ที่คีย์ chroma แล้ว):**
| ช่อง | ค่า |
|---|---|
| Texture Type | **Sprite (2D and UI)** |
| Mesh Type | Full Rect |
| Wrap Mode | Clamp |
| Filter Mode | Bilinear |
| Alpha Source | **Input Texture Alpha** / Alpha Is Transparency ✔ |
| Compression | มือถือ: ASTC |
| **Sprite Editor → Border** | ตั้ง L/T/R/B (9-slice) สำหรับ panel/bar/button |
| Image component | **Image Type = Sliced** |

**พาเนล glass ที่ทำใน Unity:** ใช้ rounded-rect sprite ขาว 9-slice → Image `Color` ดำ + `Alpha` 0.6-0.8 → ซ้อน object ขอบ rim (ขาว alpha ต่ำ) + soft shadow (Shadow/Outline component หรือ sprite เงา)

**พื้นหลังเต็มจอ (หมวด 8):** Sprite, ไม่ต้อง border, Canvas เต็มจอ

**คีย์สีใน Photoshop (ชิ้นทึบ ก่อนเข้า Unity):** Select > Color Range เลือกเขียว → ลบ → **Defringe 1px** → export PNG โปร่ง

---

## 📝 Checklist

- [ ] เช็คก่อน gen: ชิ้นนี้ **ทำใน Unity ได้เลยไหม** (panel/ปุ่ม/แถบเรียบ = ทำเอง)
- [ ] หน้าตรง แบน ไม่มี perspective / ไม่มีเงาทอดพื้น
- [ ] สไตล์: flat + gloss นุ่มบางๆ, มุมโค้งเยอะ, **ไม่มีขอบหนา/halftone**
- [ ] สี: ฐานดำ neutral + accent สลับได้ + ตัวหนังสือขาว (อาร์ตไม่มีตัวอักษร)
- [ ] panel = โปร่งแสง (ทำในเอนจิน หรือ gen ทึบแล้วลด alpha — ไม่คีย์ทะลุของใส)
- [ ] ชิ้นยืดได้: ขอบ 4 ด้านเท่ากัน + กลางเรียบ (9-slice)
- [ ] icon: ชุดเดียว glossy สะอาดตรงกัน อ่านออกตอนย่อเล็ก
- [ ] card 1-5: ไล่ rim/glow ชัด ดูออกว่าอันไหนหายาก
- [ ] bg: cinematic เบลอ/เข้มกลางจอพอให้ UI อ่านออก
- [ ] ชิ้นทึบ: เก็บขอบ chroma ใน Photoshop ก่อนเข้า Unity

---

## ทำต่อได้

- **popup เฉพาะทาง:** result win/lose, level-up, chest-open — โครง glass panel เดิม + accent
- **badge/chip:** tier number, "+BONUS", ประเภทธาตุ (จุดกลมสี), ป้าย % — ดู 4B
- **timeline/schedule:** แถบ start…end แบบ ref — ดู 2D
- **progress node:** map area, quest checkmark
- เผ่าอื่น: **แค่เปลี่ยนสี accent** (Human/Galax/Natural/Darkside) — STYLE BLOCK เดิม, base ดำ neutral เหมือนกัน
