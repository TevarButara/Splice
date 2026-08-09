# Concept Art Prompts — HEROES · เผ่า NATURAL 🌿⚔️

> **Hero = ตัวชูโรง** — รายละเอียดมากกว่ามอน, silhouette ชัด, มีบุคลิกและท่าทางเฉพาะตัว. เผ่าเดียวกับ `conceptArt-Natural.md` (Beast/ธรรมชาติ: leafy green / earthy brown / terracotta / bone-white) แต่ **ยกระดับเป็นฮีโร่ Fantasy MOBA**
> **สไตล์ที่ล็อก:** **Broad-Brush Painterly Fantasy MOBA** — สัดส่วน heroic ประมาณ 7–7.5 หัว, anatomy และข้อต่ออ่านชัด, มือมี 5 นิ้วแยกชัดเจน. ใช้มวลสีใหญ่, ขอบนอกคม, ขอบภายในนุ่ม, ลด internal detail ประมาณ 50–60% และอ่านออกจากกล้อง top-down. ห้ามย้อนกลับไปเป็น realistic/PBR หรือ sci-fi render เพื่อให้ปั้น mid-poly 3D, rig, skin และทำ combat animation ได้ง่าย

---

## Hero ของเผ่า Natural (6 ตัว)
| # | สาย | เพศ | สถานะ |
|---|---|---|---|
| **1** | **นักดาบ (Swordsman)** | ชาย | ✅ Rowan จิ้งจอกนักดาบ |
| **2** | **แท้งค์ (Tank)** | ชาย | ✅ Torvin เต่ายักษ์ |
| **3** | **ธนู (Archer)** | หญิง | ✅ Elara นักธนู fae |
| **4** | **เวท (Mage)** | หญิง | ✅ Elowen ผู้พิทักษ์ป่า |
| **5** | **มือปืน (Gunner)** | ชาย | ✅ Rennick แรคคูนมือปืน (ด้านล่าง) |
| **6** | **ไฟท์เตอร์ระยะประชิด (Fighter)** | ชาย | ✅ Kaelor สิงโตกลายพันธุ์กรงเล็บชาด (ด้านล่าง) |

> ✅ **ทีมเผ่า Natural 6 ตัว** — beastfolk 4 (จิ้งจอก/เต่า/แรคคูน/สิงโต) + fae/elf 2 (ธนู/เวท)
> 🎨 สไตล์ทั้งหมด = **premium stylized fantasy MOBA** — heroic, readable, production-ready และไม่อ้างอิง/เลียนแบบเกมหรือศิลปินที่มีอยู่
> 🎨 **สีประจำเผ่าไม่ใช่ uniform:** hero แต่ละตัวใช้ dominant palette ต่างกันได้เต็มที่. รักษาความเป็น Natural ผ่าน organic materials, leaf/wood/bone motifs, tribal knotwork และสี accent ร่วมเพียงเล็กน้อย — ไม่บังคับว่าทุกตัวต้องแต่งเขียวหรือน้ำตาลเหมือนกัน

---

## วิธีใช้ / สิ่งที่ได้ต่อ hero

แต่ละ hero มี **ชุดไฟล์หลัก** และ optional modular garment sheets:
- **(A) Key Art** — ท่าโชว์ไดนามิก ถืออาวุธ → การ์ด hero / จอ hero select / โปรโมท
- **(B-FRONT) T-Pose Front Sheet** — ด้านหน้าหนึ่งตัวเต็มแผ่น 4K
- **(B-BACK) T-Pose Back Sheet** — ด้านหลังหนึ่งตัวเต็มแผ่น 4K
- **(B-LEFT) T-Pose Left Sheet** — ด้านซ้ายหนึ่งตัวเต็มแผ่น 4K
- **(B-RIGHT) T-Pose Right Sheet** — ด้านขวาหนึ่งตัวเต็มแผ่น 4K
- **(G1) Modular Long Garment Front + Back** *(เฉพาะตัวที่มี skirt/robe/coat ยาว)* — เครื่องแต่งกายแยกสำหรับสวมบน body
- **(G2) Modular Long Garment Left + Right** *(เฉพาะตัวที่มี skirt/robe/coat ยาว)* — ความหนา layer order และระยะห่างจากขา
- **(P1/P2) Modular Lower-body Set Front+Back / Left+Right** *(เมื่อต้องการแยกกางเกง)* — body ใส่ plain rigging under-shorts; กางเกงจริงอาจรวม waistband, hip panels และชายผ้าประจำชุดเป็น equipment ชิ้นเดียว โดยใช้ pelvis/thigh bones และเพิ่ม child cloth bones สำหรับส่วนย้อย
- **(C) Weapon / Shield / Attached-weapon detail** — อาวุธถือและโล่ให้ถอดออกจาก body และสร้างในโฟลเดอร์ย่อยของ prop; อาวุธที่เป็นส่วนถาวรของถุงมือ/แขนให้ทำ construction sheet แยก แต่คงติดอยู่กับ body turnaround
- **(D) Skills + Icons** — Skill 1 / Skill 2 / Ultimate (ชื่อ+คำอธิบาย) พร้อม prompt ไอคอนสกิล (ชุด 3 อันเข้ากัน)

> ⚠️ **Turnaround workflow ที่ล็อก:** สร้างทีละมุมเป็นคนละ generation และหนึ่งไฟล์มีตัวละครเพียงหนึ่งตัว: **FRONT → BACK → LEFT → RIGHT**. ใช้ Key Art ที่อนุมัติเป็น identity master ทุกครั้ง; แนบ Front เพิ่มใน Back/Left และแนบ Front+Left เพิ่มใน Right เพื่อคุมสัดส่วน ชุด ลายและสี. ห้ามขอหลายมุมในภาพเดียว. ถ้ามี garment ยาวเกินกลางต้นขา ให้ถอดจาก body turnaround แล้วสร้าง modular garment แยก โดย body ใส่ fitted fantasy under-suit หรือ fitted short under-shorts

### โครงสร้างโฟลเดอร์และชื่อไฟล์ที่ล็อก

```text
Natural/hero/NewStyle/[HeroName]/
  nat-hr##_hero-front-4k-v1.png
  nat-hr##_hero-back-4k-v1.png
  nat-hr##_hero-left-4k-v1.png
  nat-hr##_hero-right-4k-v1.png
  [WeaponOrShield]/
    nat-hr##_hero-[prop]-front-4k-v1.png
    nat-hr##_hero-[prop]-back-4k-v1.png
    nat-hr##_hero-[prop]-side-4k-v1.png
```

---

## 🎨 HERO STYLE BLOCK (แปะหน้า prompt ทุกภาพ)

```
Premium stylized PAINTERLY FANTASY MOBA game-hero concept art with an original visual
identity. HEROIC PROPORTIONS: approximately 7 to 7.5 heads tall, a moderately
stylized head, defined shoulders, torso and pelvis, long readable limbs, believable
anatomy, and clearly located elbows, wrists, knees and ankles. NOT chibi, NOT
super-deformed, NOT mascot-like. HANDS: both hands are complete and visible, each
with exactly five clearly separated, anatomically coherent fingers including a
readable thumb, knuckles and believable grip mechanics; no mitten hands, fused,
duplicated, missing or malformed fingers. Keep the face expressive and appealing
without oversized baby-like eyes.
RENDERING — LOCKED BROAD-BRUSH PAINTERLY MOBA STYLE: polished 2.5D hand-painted
game-art finish made from broad, confident brush masses and softly blended color
planes. Reduce internal detail by roughly 50–60%. Organize the character into three
large value groups with a crisp outer silhouette, while most interior transitions
are soft or lost. Reserve hard edges for the face, eyes, hands, major costume borders
and the main weapon edge. Use sparse useful seams, restrained motifs, simplified
graphic material cues and only a few focal highlights. No realistic PBR response,
individual fur strands, dense scale rendering, fabric weave, pores, scratches or
micro-engraving. Fur, scales, fabric, leather, wood and metal must feel soft,
stylized and hand-painted. The hero must remain instantly readable during fast-paced
top-down MOBA gameplay and practical as a clean mid-poly model with hand-painted
textures.
PRODUCTION DESIGN: build clothing, armor, belts, pouches and accessories as clear
modular pieces with believable thickness and attachment points. Keep shoulder,
elbow, wrist, waist, hip, knee, ankle and finger deformation zones unobstructed.
Avoid rigid plates crossing joints, excessive overlapping layers, thin fragile
ornaments, tangled cloth, uncontrolled dangling clutter and shapes likely to clip
during idle, run, attack, cast, hit-react or death animations. Long capes, robes,
skirts, coat tails and hair ARE ALLOWED when designed as clearly separated riggable
pieces with sufficient air gap from the legs and body, clean attachment points,
simple layer order, and practical topology for secondary bones or cloth simulation.
They must not fuse with, wrap tightly around, or intersect leg deformation zones in
the neutral pose. For modeling turnarounds, any skirt, robe or coat panel extending
below mid-thigh must be REMOVED from the base-body B1/B2 sheets and documented as a
separate modular garment in G1/G2 sheets. The base body wears a fitted, modest
fantasy under-suit or fitted short under-shorts with clearly visible hip, knee and
leg anatomy. Long hanging sleeve panels may remain on the body outfit because they
do not follow the leg rig; build them as separate secondary-bone or cloth pieces.
Keep the design feasible for a reusable humanoid game rig, skinning and standard
combat animation pipeline.
NATURE-HERO MOTIF: Nature heroes are mythical forest folk — BEASTFOLK
(fox / raccoon / turtle / lion style animal-people: fur or scales, animal ears,
tail) OR FAE / ELF (pointed ears, small fairy wings, flower/leaf crown). The faction
identity comes from organic materials, living-wood / leaf / bone motifs, tribal
knotwork and small shared nature accents — NOT from making every hero the same
color. EACH HERO MAY HAVE A DISTINCT DOMINANT PALETTE appropriate to their identity.
Each hero must receive a PERSONAL dominant palette suited to role and personality.
Leafy green is normally only a small faction-link accent — for example one gem,
rune, stitch, leaf inset or weapon edge — and must not turn the roster into matching
green uniforms. Rowan is the approved exception whose deep-green vest remains a
major identity color, balanced by cream fur, brown leather and muted gold.
NATURAL faction palette library: warm tan, earthy brown, leafy green, terracotta,
red-orange, sun-yellow, teal jewel, bone/cream-white and gold. Select a focused
subset per hero instead of using every color on every character.
NO glowing aura / no energy swirls / no floating particles AROUND the
character (magic shows only as ON-BODY glow — runes, glowing blade edge, glowing
eyes). Clean pure white background, full body fully visible, single character,
both hands and both feet fully visible, no cropped limbs, no text, no letters, no
signature, no watermark. Avoid extreme perspective and extreme foreshortening.
```

## 🧍 SINGLE-VIEW T-POSE BLOCKS — เลือกแปะท้าย prompt เพียง 1 block ต่อ generation

> **Tail-neutral rule สำหรับ beastfolk:** โคนหางเชื่อมกึ่งกลาง sacrum และหางชี้ตรงไปด้านหลังตามแกนลึก ขนานกับพื้นในทุกมุม. Front/Back จึงเห็นหางแบบ foreshortened และห้ามวาดหางกวาดออกซ้ายหรือขวา; Left/Right แสดงความยาวเต็มเป็นเส้นแนวนอน. ห้ามให้หางแตะหรือซ้อนมือ

### (B-FRONT) FRONT 4K
```text
Create exactly ONE large full-body orthographic FRONT view, one character only.
Strict neutral T-pose: both straight arms fully extended horizontally at shoulder
height, palms down, five separated relaxed fingers per hand. Legs straight and
slightly apart. Tail projects straight backward along the depth axis, parallel to
the ground, and is strongly foreshortened behind the pelvis; no tail sweeps left or
right. Fill about 82–88% of the square canvas height while
keeping ears, fingertips, tail and feet fully inside. Pure white background; no
weapon, prop, labels, text, perspective or extra figures.
```

### (B-BACK) BACK 4K
```text
Create exactly ONE large full-body orthographic BACK view of the same approved
character. Match the Front master exactly in scale, proportions, color boundaries,
costume construction and simplified pattern placement. Strict neutral T-pose with
both complete arms and hands visible and not hidden by the tail or back equipment.
Both arms extend horizontally at shoulder height, palms down. Five separated relaxed
fingers per hand. Show back closures, the centered sacrum tail root and attachment
points clearly. Fill about 82–88% of the square canvas height. Pure white background;
no weapon, prop, labels, text, perspective or extra figures.
```

### (B-LEFT) LEFT 4K
```text
Create exactly ONE large full-body true orthographic LEFT PROFILE facing the left
edge. Match the approved Front master exactly. Strict neutral T-pose with both arms
aligned along the depth axis at shoulder height. Show costume thickness, layer order,
the tail rooted at sacrum and extending straight backward horizontally parallel to
the ground, and footwear profile. Five separated relaxed fingers per hand. Fill
about 82–88% of the square canvas height. Pure white background; no weapon, prop,
labels, text, perspective or extra figures.
```

### (B-RIGHT) RIGHT 4K
```text
Create exactly ONE large full-body true orthographic RIGHT PROFILE facing the right
edge. Match the approved Front and Left masters exactly; this is a genuine right-side
design view, not a newly invented costume. Strict neutral T-pose with both arms along
the depth axis at shoulder height. Show costume thickness, layer order, and the tail
rooted at sacrum and extending straight backward horizontally parallel to the ground.
Five separated relaxed fingers per hand. Fill about 82–88% of the square canvas
height. Pure white background; no weapon, prop, labels, text, perspective or extra
figures.
```

## 👗 OPTIONAL MODULAR LONG-GARMENT BLOCKS — ใช้เมื่อ garment ยาวเกินกลางต้นขา

### (G1) GARMENT FRONT + BACK SHEET BLOCK
```text
Create MODULAR GARMENT SHEET G1 only: the long skirt, robe or coat-tail equipment
separated completely from the character body, shown as exactly TWO large orthographic
views in one horizontal row: exact FRONT and exact BACK. Present the wearable garment
around a simple neutral invisible-body volume / clean hollow mannequin form so the
waist opening, hip clearance and leg cavity remain understandable; do not render a
full character, skin, head, arms, hands or feet. Match the approved Key Art and B1
base body's exact waist/hip scale, attachment height, colors, textures, patterns and
materials. Show waistband/anchor, panel seams, front/side/back layer order, inner
lining, hem, fasteners and practical secondary-bone or cloth-panel segmentation.
Maintain generous air gaps for both legs and avoid fabric crossing the neutral leg
volumes. Pure white background, no body, no weapon, no text, no labels, no watermark.
```

### (G2) GARMENT LEFT + RIGHT SHEET BLOCK
```text
Create MODULAR GARMENT SHEET G2 only: the SAME separate long skirt, robe or coat-tail
equipment as G1, shown as exactly TWO large orthographic profile views in one
horizontal row: exact LEFT and exact RIGHT. No full character body. Match G1 exactly
in scale, waistband, hip shape, attachment points, panel count, layer order, textures,
patterns and colors. Show garment thickness, inner lining, front/back depth, side
seams, leg cavity, hip clearance and hem profile clearly. The garment must fit the
approved B1/B2 base body and share its master skeleton, while using dedicated skirt/
cloth bones below the waist. Pure white background, no body, no weapon, no text,
no labels, no watermark, no perspective distortion.
```

> **Modular long-garment rule:** garment ที่ยาวเกินกลางต้นขาให้ถอดจาก body B1/B2 และสร้าง G1/G2 แยกเสมอ ตัว body ใช้ fitted fantasy under-suit/กางเกงขาสั้นแนบตัวเป็น base layer ส่วนแขนเสื้อยาวที่ย้อยยังอยู่กับ upper-body outfit ได้ แต่ต้องเป็นชิ้น secondary-bone/cloth แยกจากแขน

---

## 🎯 SKILL ICON STYLE BLOCK (แปะหน้า prompt ไอคอนสกิลทุกอัน)

```
Mobile game SKILL ICON in the given FRAME SHAPE (a circle, or a rounded square). A
single bold readable emblem centered, in the same premium stylized fantasy MOBA
look as the heroes: thick clean dark outline, controlled painterly-toon shading, a
soft inner glow, a subtle radial gradient background inside the frame, and a light
highlight sheen. The icon must read INSTANTLY at small size — one clear object,
strong silhouette, high contrast. NATURAL palette accents (leaf green, warm gold,
bone-white) plus the skill's own color. Ultimate icons are grander with more GOLD
and an epic feel. No text, no letters, no numbers, no UI border chrome. The
background OUTSIDE the icon frames must be a SOLID FLAT MAGENTA / hot-pink CHROMA
fill (pure #FF00FF, completely filled, NOT transparent) — a chroma-key color that
appears NOWHERE inside the icons (so the black outlines survive) — for easy keying
to transparency later.
```

> ต่อ hero = **1 แผ่น 6 ไอคอน** (2 แถว × 3 คอลัมน์): **แถวบน = วงกลม / แถวล่าง = สี่เหลี่ยมมุมมน** (สกิลเดียวกัน Skill1/Skill2/Ultimate เรียงคอลัมน์เดียวกัน) — ได้ทั้ง 2 ทรงไว้ใช้ตาม UI. อยากได้ทีละอันก็ตัด prompt เฉพาะบรรทัดนั้น
> 🟣 **พื้น = มาเจนต้า chroma #FF00FF** (ไอคอนมีเส้นขอบดำ → คีย์สีชมพูออก เส้นดำอยู่ครบ). คีย์: `magick in.png -fuzz 20% -transparent magenta out.png` → Unity import เป็น **Sprite (2D and UI)**

---

# Hero 1 — Rowan, the Wildblade (จิ้งจอกนักดาบ) 🦊⚔️

> **APPROVED IDENTITY MASTER:** `Natural/hero/NewStyle/Rowan/nat-hr01-rowan-keyart-4k-v1.png`
> **จิ้งจอก beastfolk นักดาบ** — รูปร่างเพรียวปราดเปรียว แววตาคม ยิ้มกวน หูจิ้งจอก+ห่วงทอง ลายรูนเขียวบนหน้า หางฟูใหญ่. ล็อกชุดเป็น vest เขียวเข้มทรงสะอาด ขอบทองด้านกว้างหนึ่งชั้น ลาย leaf-knot กลางอกเพียงหนึ่งจุด, sash หนังน้ำตาล, waist wrap สั้นสามแผง (กลางครีม/ข้างเขียว), bracer และรองเท้าหนังน้ำตาล. ห้ามเพิ่มลายแน่นหรือชิ้นส่วนใหม่
> **beastfolk = สัตว์อยู่แล้ว** จึงไม่ต้องมีเขา (motif เผ่า = beastfolk + ทอง + ลายรูนเขียว)

## (A) Key Art — ท่าโชว์ (ตาม ref)
```
[HERO STYLE BLOCK]
The hero is "Rowan the Wildblade", a young adult male FOX beastfolk swordsman: a
fluffy cream-and-white fox with alert expressive green (yellow-green) eyes, a sharp
cool-yet-charming face, large expressive fox ears with little gold ear-cuffs,
restrained tribal GREEN markings on his forehead and cheeks, and
a huge fluffy striped cream-and-tan fox tail. Slim agile build. Lock the approved
clean outfit: a deep forest-green sleeveless vest with one broad muted-gold border
and only ONE central leaf-knot motif, a brown sash and belt, a short three-panel
waist wrap with one cream center panel and two green leaf side panels, simple brown
leather bracers and wrapped brown boots with broad gold accents. No dense embroidery,
no micro runes and no extra accessories. He wields the approved LIVING-WOOD KATANA:
a broad pale bone-wood blade, narrow soft-green cutting edge, one simple muted-gold
spine accent, carved leaf guard, brown bamboo hilt and tiny leaf tassel. Pose with blade resting
back over one shoulder, looking at the viewer with a cheeky grin. His free hand is
open and unobstructed with five clearly separated fingers; his weapon hand grips
the hilt naturally. Full body, clean white background, no text.
```

## (B) T-Pose Turnaround — สร้าง Front / Back / Left / Right แยกคนละไฟล์ 4K

> ลำดับที่ล็อก: **Front → Back → Left → Right**. ทุกครั้งแนบ Approved Key Art และภาพ master ก่อนหน้าเพื่อรักษา identity
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Rowan the Wildblade", the SAME young FOX beastfolk
swordsman: fluffy cream-and-white fox, alert yellow-green eyes, sharp charming face,
large ears with gold cuffs, restrained green face markings and a huge striped cream-
and-tan tail. Preserve the approved clean outfit exactly: deep forest-green sleeveless
vest, one broad muted-gold edge, one central leaf-knot motif, brown sash and belt,
short three-panel waist wrap (cream center, two green sides), simple brown bracers
and wrapped boots with broad gold accents. No dense knotwork, no micro-detail and no
new accessories. Use the approved Rowan Key Art as the authoritative identity master.
Append exactly ONE single-view block: B-FRONT, B-BACK, B-LEFT or B-RIGHT. Exactly the
same character, gear, proportions, color boundaries and simplified patterns in every
file. Standing in a strict T-POSE: BOTH arms fully straight and extended horizontally
at shoulder height in every view, palms down. EMPTY OPEN HANDS (no weapon — the sword
is a separate prop), each hand showing
exactly five separated relaxed fingers including the thumb; legs straight and
slightly apart. The tail root connects at the center sacrum and the entire tail points
straight backward horizontally, parallel to the ground; never sweep it beside a leg
or allow it to touch a hand. Clearly expose all major joint and deformation
zones for modeling, rigging and skinning. Full body visible, clean white background,
no weapons, no text.
```

## (C) Weapon prop — ดาบ (held prop; สร้าง Front / Back / Side แยกไฟล์ 4K)
```
[HERO STYLE BLOCK]
Isolated game prop, NO character and NO hands: the approved LIVING-WOOD KATANA of
Rowan the Wildblade — one broad gently curved pale bone-wood blade, a narrow localized
soft-green cutting edge, one continuous simple muted-gold spine accent, a clean carved
leaf guard, brown bamboo-wrapped hilt and one tiny leaf tassel. Broad-brush painterly
MOBA materials, simple modelable construction, no dense runes or micro engraving.
Create exactly ONE orthographic prop view per file: FRONT, BACK or thin SIDE profile.
The entire sword uses the SAME strict vertical centered presentation in every file:
blade tip points straight up at 12 o'clock, hilt is below the guard, and the tassel
cord with its single leaf hangs perfectly straight down at 6 o'clock. Front, Back
and Side must match in scale and component height. No diagonal composition and no
sideways-swinging tassel. Generous white margin. Pure white background, no body,
hand, sheath, extra prop, text, label or watermark.
```

## (D) Skills — สกิล + ไอคอน 🦊

| สกิล | ชื่อ | ประเภท | คำอธิบาย |
|---|---|---|---|
| **Skill 1** | **Leaf Slash** · ตวัดใบเสี้ยว | Dash melee | พุ่งไปข้างหน้าเป็นเส้น ฟันศัตรูที่ขวางทาง ทิ้งรอยฟันพลังใบไม้เขียว — เข้าหา/ไล่ล่าเป้าเร็ว |
| **Skill 2** | **Whirlbloom** · หมุนวนใบมีด | AoE รอบตัว | หมุนดาบรอบตัว 1 รอบ ฟันศัตรูรอบข้างทั้งหมด + ผลักถอยเล็กน้อย |
| **Ultimate** | **Wildblade Frenzy** · คลั่งใบมีดเถื่อน | Burst | เข้าโหมดคลั่ง ความเร็วโจมตี+ดาเมจพุ่ง ฟันรัวหลายครั้งใส่เป้าที่แข็งแกร่งที่สุด จบด้วยฟันกากบาทพลังเขียว |

```
[SKILL ICON STYLE BLOCK]
Two rows of matching NATURAL skill icons on ONE sheet (2 rows x 3 columns), the
SAME three skills in each row, columns left to right = Skill 1, Skill 2, Ultimate.
TOP row = each icon as a CIRCLE; BOTTOM row = the SAME icon as a ROUNDED SQUARE
(same art in both rows, only the frame shape changes). The three skills:
1) "Leaf Slash": a single sharp green crescent slash / sword-swipe arc with tiny
   flying leaves, on a green-and-white icon.
2) "Whirlbloom": a katana spinning inside a swirling ring of green leaves (a
   circular whirl motion), green and gold.
3) "Wildblade Frenzy" (grander, more GOLD, epic): a golden fox-head emblem behind
   two crossed glowing green katanas forming an X, radiant gold burst.
Same art and colors in both rows — only the frame shape differs (circles on top,
rounded squares below). Solid flat magenta chroma (#FF00FF) background, no text.
```

---

# Hero 2 — Torvin, the Bulwark (เต่ายักษ์แท้งค์) 🐢🛡️

> **เต่ายักษ์ beastfolk** สายแท้งค์ — ตัวใหญ่ ถึก บึกบึน ใจเย็นนิ่งแต่เท่. **กระดองมอสบนหลัง = เกราะธรรมชาติ** มีต้นไม้/ดอกไม้เล็กขึ้น. รูนเขียวเรือง + ทองแต่งตามกฎเผ่า. อาวุธ = **โล่ยักษ์ไม้-หิน** (สายกันแทงค์)
> ⚠️ ต่างจากมอน Bastion Tortoise: ตัวนี้เป็น **เต่ายืน 2 ขา humanoid heroic** ดีเทลจัดกว่า

## (A) Key Art — ท่าโชว์
```
[HERO STYLE BLOCK]
The hero is "Torvin the Bulwark", a huge powerful adult male TURTLE beastfolk tank: a
big broad-shouldered sturdy bipedal turtle with thick powerful arms, a heavy
grounded stance, a calm stoic-but-cool face. WARM earthy palette (NOT mostly
green): sandy TAN and warm olive-brown scaly skin, with green used only as
accents. On his back a big domed SPIKY SHELL — a rugged brown shell whose rim is
ringed with a frill of chunky SPIKES and green leaves (a spiky leaf-mane look,
like the reference). SIMPLE CLEAN outfit (painterly, uncluttered — only a few
pieces): a wrapped cream tunic and sash, a sturdy leather belt, a simple leaf
waist wrap. A tribal necklace of bone teeth and a teal jewel, a FEW subtle tribal
markings on his arms, and small gold accents. Warm earthy color tone like the
reference (tan/brown body, green leaf frill, terracotta and teal accents). He
rests one hand on a big round bark-and-stone SHIELD standing beside him. Sturdy
immovable pose, quiet confidence. Strong simple silhouette, painterly and clean,
full body, clean white background, no text.
```

## (B) A-Pose Turnaround — ใช้ prompt นี้ร่วมกับ B1 หรือ B2 ทีละแผ่น
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Torvin the Bulwark", the SAME huge hulking male
TURTLE beastfolk tank: broad-shouldered sturdy bipedal turtle, thick arms, calm
cool face; WARM earthy palette (NOT mostly green) — sandy TAN and warm
olive-brown scaly skin, green as accents only; a big domed SPIKY SHELL on the
back, its rim ringed with a frill of chunky spikes and green leaves; a FEW subtle
tribal markings on the arms; SIMPLE CLEAN gear (painterly, uncluttered) — a
wrapped cream tunic and sash, a leather belt, a simple leaf waist wrap, a tribal
bone-teeth-and-teal-jewel necklace, small gold accents, sturdy legs.
Use the approved Torvin Key Art as the authoritative identity reference. Append
exactly ONE turnaround block: either (B1) FRONT + BACK or (B2) LEFT + RIGHT; never
request all four views in one image. Exactly the same character, gear, proportions,
texture patterns and colors across both sheets. The B1 back view must clearly show
the spiky shell without hiding either arm. Standing in a strict A-POSE: BOTH arms
straight and lowered diagonally about 35–45 degrees from the shoulders in EVERY view;
the shell does not cover the arms. EMPTY OPEN HANDS (no weapon — the shield is a separate prop), angled toward the thighs with a clear air gap from the hips,
down, each hand showing exactly five separated relaxed fingers including the thumb;
legs straight and slightly apart, facing forward. Clearly expose all major joint
and deformation zones for modeling, rigging and skinning. Full body visible, clean
white background, no weapons, no text.
```

## (C) Weapon prop — โล่ยักษ์ (held, prop แยก)
```
[HERO STYLE BLOCK]
Isolated game prop, NO character, NO hands holding it: the big round
bark-and-stone SHIELD of Torvin the Bulwark — a chunky round shield of bark and
grey stone with a raised stone boss in the center, a FEW simple green tribal rune
marks, a subtle gold rim, and a small moss patch. Simple clean painterly design,
not cluttered. 3 views in one row, evenly spaced: front, side (edge-on, showing
thickness), back (with a wooden handle). Clean white background, no text.
```

## (D) Skills — สกิล + ไอคอน 🐢

| สกิล | ชื่อ | ประเภท | คำอธิบาย |
|---|---|---|---|
| **Skill 1** | **Bulwark Guard** · กำแพงพิทักษ์ | Defense buff | ยกโล่ตั้งการ์ด ลดดาเมจให้ตัวเอง+พันธมิตรด้านหน้าอย่างมากช่วงเวลาสั้น (บล็อกแนวหน้า) |
| **Skill 2** | **Quake Stomp** · ทุบปฐพี | AoE stun | ทุบโล่ลงพื้น เกิดคลื่นสะเทือน สตัน/ทำให้ศัตรูรอบตัวช้าลง |
| **Ultimate** | **Fortress Shell** · ป้อมกระดอง | Invuln + Taunt | หดเข้ากระดองหนาม เกือบอมตะชั่วขณะ ยั่ว (taunt) ให้ศัตรูตีตัวเอง + สะท้อนดาเมจหนามกลับ |

```
[SKILL ICON STYLE BLOCK]
Two rows of matching NATURAL skill icons on ONE sheet (2 rows x 3 columns), the
SAME three skills in each row, columns left to right = Skill 1, Skill 2, Ultimate.
TOP row = each icon as a CIRCLE; BOTTOM row = the SAME icon as a ROUNDED SQUARE
(same art in both rows, only the frame shape changes). The three skills:
1) "Bulwark Guard": a sturdy round bark-and-stone shield glowing with a soft green
   protective sheen, brown/green/gold.
2) "Quake Stomp": the same shield slamming down with cracked ground and concentric
   shockwave rings, dust and small rocks, earthy tan/green.
3) "Fortress Shell" (grander, more GOLD, epic): a big domed SPIKY turtle shell
   (fortress-like) ringed with green leaves and radiant gold, an unbreakable
   fortress feel.
Same art and colors in both rows — only the frame shape differs (circles on top,
rounded squares below). Solid flat magenta chroma (#FF00FF) background, no text.
```

---

# Hero 3 — Elara, the Leafshot (นักธนู fae สาว) 🏹🍃

> **สาว fae/elf นักธนู** ผู้ปราดเปรียว — หูแหลม, มงกุฎใบไม้ทอง, ปีกภูติใสเล็กๆ, ผมหางม้าน้ำตาล. ชุด **ทอง/ครีม + คลุมเขียวใบไม้** โทนอุ่น (ไม่จมเขียว) พร้อมธนูไม้ + กระบอกลูกธนู
> 🌿 **ขยาย motif เผ่า:** Nature hero เป็นได้ทั้ง **beastfolk (จิ้งจอก/เต่า)** และ **fae/elf (หูแหลม+ปีกภูติ+มงกุฎใบไม้)** — ทั้งคู่ = ชาวป่าเวทมนตร์ ไม่ใช่คนธรรมดา

## (A) Key Art — ท่าโชว์ (ตาม ref)
```
[HERO STYLE BLOCK]
The hero is "Elara the Leafshot", an agile young adult female fae/elf nature
archer with balanced heroic proportions, alert expressive eyes, pointed elf ears,
a long brown ponytail,
a delicate GOLD laurel-leaf crown, small iridescent translucent fairy wings on
her back, tiny green leaf earrings. Outfit — dainty and heroic, WARM palette (NOT
mostly green): a gold-and-cream corset dress with a brown leather bodice and gold
trim, a green round gem brooch at the collar, a flowing leafy-green watercolor
CAPE with a soft gold-edged hem, cream/white leggings, and refined green leaf-vine
boots with gold accents; a slim vine wrap and bracer on her bow arm; a small
leather quiver of arrows at her hip wrapped in vines. She holds a slender wooden
RECURVE BOW. Light graceful pose, cape flowing, looking off with a gentle
confident air. Her bow hand and draw hand use believable archery anatomy; all five
fingers on each hand are distinct and correctly placed. Controlled stylized
hand-painted MOBA finish, broad readable shapes and clean silhouette. Full body, clean white
background, no text.
```

## (B) A-Pose Turnaround — ใช้ prompt นี้ร่วมกับ B1 หรือ B2 ทีละแผ่น
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Elara the Leafshot", the SAME young adult female
fae/elf archer with balanced heroic proportions: pointed elf ears, long brown
ponytail, gold
laurel-leaf crown, small iridescent fairy wings, green leaf earrings; WARM
palette (not mostly green) — a gold-and-cream corset dress with brown leather
bodice and gold trim, a green gem brooch, a leafy-green cape with gold-edged hem,
cream leggings, green leaf-vine boots with gold, a vine bracer on the bow arm, a
small vine-wrapped quiver of arrows at the hip.
Use the approved Elara Key Art as the authoritative identity reference. Append
exactly ONE turnaround block: either (B1) FRONT + BACK or (B2) LEFT + RIGHT; never
request all four views in one image. Exactly the same character, gear, proportions,
texture patterns and colors across both sheets. The B1 back view must clearly show
the cape, ponytail and wings. Standing in a strict A-POSE: BOTH arms straight and
lowered diagonally about 35–45 degrees from the shoulders in EVERY view, complete
and symmetric (not hidden behind the cape or wings). EMPTY OPEN HANDS (no weapon —
the bow is a separate prop), angled toward the thighs with a clear air gap, each hand
showing exactly five separated relaxed fingers including the thumb; legs straight
and slightly apart, facing forward. Clearly expose all major joint and deformation
zones for modeling, rigging and skinning. Full body visible, clean white background,
no weapons, no text.
```

## (C) Weapon prop — ธนู (held, prop แยก)
```
[HERO STYLE BLOCK]
Isolated game prop, NO character, NO hands holding it: the wooden RECURVE BOW of
Elara the Leafshot — a slender graceful curved bow of pale living wood with a
leather-wrapped grip, thin green vines twining along the limbs, small gold caps
at the tips, a soft-green glowing bowstring, and a tiny leaf charm. Simple clean
painterly design. 3 views in one row, evenly spaced: front, side, back. Clean
white background, no text.
```

## (D) Skills — สกิล + ไอคอน 🏹

| สกิล | ชื่อ | ประเภท | คำอธิบาย |
|---|---|---|---|
| **Skill 1** | **Piercing Shot** · ธนูทะลวง | Line pierce | เล็งอัดพลังลูกธนู 1 ดอก ยิงทะลุศัตรูเป็นแนวตรง ดาเมจแรง |
| **Skill 2** | **Leaf Volley** · ห่าธนูใบไม้ | AoE rain | ยิงธนูขึ้นฟ้าเป็นชุด ตกลงมาเป็นห่าในพื้นที่เป้าหมาย โดนหลายตัว |
| **Ultimate** | **Spirit Arrow** · ศรวิญญาณพงไพร | Nuke line | อัดศรพลังธรรมชาติดวงใหญ่ ยิงทะลุทั้งแนว ดาเมจมหาศาล |

```
[SKILL ICON STYLE BLOCK]
Two rows of matching NATURAL skill icons on ONE sheet (2 rows x 3 columns), the
SAME three skills in each row, columns left to right = Skill 1, Skill 2, Ultimate.
TOP row = each icon as a CIRCLE; BOTTOM row = the SAME icon as a ROUNDED SQUARE
(same art in both rows, only the frame shape changes). The three skills:
1) "Piercing Shot": a single glowing green arrow shooting forward with a sharp
   pierce streak, green/gold.
2) "Leaf Volley": several arrows arcing down like rain over a small target ring,
   with falling green leaves.
3) "Spirit Arrow" (grander, more GOLD, epic): one huge radiant nature-spirit arrow
   nocked on a glowing golden bow at full draw, blazing green energy.
Same art and colors in both rows — only the frame shape differs (circles on top,
rounded squares below). Solid flat magenta chroma (#FF00FF) background, no text.
```

---

# Hero 4 — Elowen, the Grovekeeper (เวท fae ผู้พิทักษ์ป่า) 🌸✨

> **สาว fae/elf เวท** สายนักบวชป่า — สง่า อ่อนโยน. ผมยาวสลวยสีครีม/ทองอ่อน, มงกุฎดอกไม้ขาว+ใบไม้, หูแหลม, ตาเขียว. ชุด **โรบขาว/ครีม + เขียว + ทอง** สง่างาม + คทากิ่งไม้มีชีวิต. โทน**ขาว/เขียวอ่อน** (ไม่จมเขียว)
> ⚠️ **no-aura:** ผีเสื้อ/ประกาย **ห้ามลอยรอบตัว** (ตัด 3D) — ให้ผีเสื้อเกาะ**บนคทา**/ดอกไม้บนหัวแทน

## (A) Key Art — ท่าโชว์ (ตาม ref)
```
[HERO STYLE BLOCK]
The hero is "Elowen the Grovekeeper", a graceful young adult female fae/elf nature
mage with balanced heroic proportions, gentle green eyes, pointed elf ears,
very long flowing braided platinum-cream hair, a delicate crown of small WHITE
flowers and green leaves with a small leaf sprout on top. Outfit — elegant
and dainty, WARM LIGHT palette (mostly white/cream with green, NOT all green): a
flowing white-and-cream druid robe-dress with a green under-gown, soft gold trim,
green gem clasps and small leaf motifs, layered petal-like sleeves. She holds a
tall GNARLED LIVING-WOOD STAFF (a natural twisting branch) topped with a green
gem and small leaves, with a single green butterfly resting ON the staff top (no
floating swarm around her). Serene elegant pose, robe and hair flowing softly.
Her staff hand grips naturally and her free hand is fully visible with five clearly
separated fingers. Controlled stylized hand-painted MOBA finish, clean silhouette.
Full body, clean white background, no text.
```

## (B) A-Pose Turnaround — ใช้ prompt นี้ร่วมกับ B1 หรือ B2 ทีละแผ่น
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Elowen the Grovekeeper", the SAME graceful young
adult female fae/elf mage with balanced heroic proportions, gentle green eyes,
pointed elf ears,
very long flowing braided platinum-cream hair, a crown of small white flowers and
green leaves with a little leaf sprout on top; elegant WARM LIGHT outfit (mostly
white/cream with green accents, NOT all green) — keep the fitted white-and-cream
upper bodice, green chest panel, soft gold trim, green gem clasps, small leaf motifs
and long layered petal-like SLEEVE PANELS. REMOVE the long robe skirt from the base
body turnaround. Replace it below the waist with a modest fitted cream-and-green
fantasy under-suit / fitted short under-shorts ending at the upper thigh, with clean
leg openings and the hip, knee and ankle anatomy fully visible. The separate long
robe garment is documented in G1/G2.
Use the approved Elowen Key Art as the authoritative identity reference. Append
exactly ONE turnaround block: either (B1) FRONT + BACK or (B2) LEFT + RIGHT; never
request all four views in one image. Exactly the same character, gear, proportions,
texture patterns and colors across both sheets. The B1 back view must clearly show
the long braided hair, upper-bodice construction and under-suit waist/hip fit, but
NO long skirt or robe panels around the legs. Standing in a strict A-POSE: BOTH arms
straight and lowered diagonally about 35–45 degrees from the shoulders in EVERY view,
complete and symmetric (not hidden behind the hair or robe). EMPTY OPEN HANDS (no
staff — the staff is a separate prop), angled toward the thighs with a clear air gap,
each hand
showing exactly five separated relaxed fingers including the thumb; legs straight
and slightly apart, facing forward. Clearly expose all major joint and deformation
zones for modeling, rigging and skinning. Full body visible, clean white background,
no weapons, no floating butterflies, no text.
```

## (G) Modular Long Robe/Skirt — สร้างเป็น equipment แยกสำหรับสวมใน Unity

> ใช้ Key Art + body B1/B2 เป็น reference และสร้าง G1/G2 คนละ generation ชิ้นนี้ไม่มีตัวละครอยู่ข้างใน แต่ต้องพอดีกับเอว/สะโพกของ base body และใช้ master skeleton เดียวกัน

```text
[HERO STYLE BLOCK]
The separate modular long robe-skirt equipment of "Elowen the Grovekeeper",
removed from her base body turnaround so it can be equipped later in Unity as a
separate SkinnedMeshRenderer. Preserve the approved Key Art design: an elegant
white-and-cream outer robe over a leafy-green under-layer, soft gold trim, restrained
leaf embroidery, green jewel drops, and clearly separated long front, side and back
petal-like cloth panels. The garment begins at the exact approved B1 waistline and
fits the same hip proportions. It contains no upper bodice, no sleeves and no body.
Use a structured waistband/waist anchor with clear fasteners. Provide generous hollow
leg cavities and air gaps so the garment never binds to the legs. Divide long panels
into clean practical segments suitable for dedicated skirt bones or cloth simulation,
with readable inner lining, seams, edge thickness and layer order. Append exactly ONE
garment block: either (G1) FRONT + BACK or (G2) LEFT + RIGHT; never request all four
views in one image. Pure white background, no character body, no skin, no head, no
arms, no hands, no feet, no staff, no butterfly, no text.
```

## (C) Weapon prop — คทา (held, prop แยก)
```
[HERO STYLE BLOCK]
Isolated game prop, NO character, NO hands holding it: the GNARLED LIVING-WOOD
STAFF of Elowen the Grovekeeper — a tall twisting natural branch of pale wood,
its top curling around a glowing green gem, small green leaves and tiny white
flowers sprouting along it, a soft cloth wrap on the grip and a small gold ring.
Simple clean painterly design. 3 views in one row, evenly spaced: front, side,
back. Clean white background, no text.
```

## (D) Skills — สกิล + ไอคอน 🌸

| สกิล | ชื่อ | ประเภท | คำอธิบาย |
|---|---|---|---|
| **Skill 1** | **Bloom Heal** · ผลิบานเยียวยา | AoE heal | ร่ายดอกไม้บานในพื้นที่ ฟื้น HP ให้พันธมิตรที่ยืนในเขต |
| **Skill 2** | **Thornsnare** · เถารัดหนาม | AoE root | เรียกเถาหนามผุดจากพื้น รัดศัตรูในพื้นที่ หยุด/ทำให้ช้า |
| **Ultimate** | **Grove Sanctuary** · อภิรักษ์พงไพร | Zone heal + buff | เสกต้นไม้ศักดิ์สิทธิ์ สร้างเขตศักดิ์สิทธิ์ ฟื้น HP ต่อเนื่อง + บัฟพันธมิตรในเขต |

```
[SKILL ICON STYLE BLOCK]
Two rows of matching NATURAL skill icons on ONE sheet (2 rows x 3 columns), the
SAME three skills in each row, columns left to right = Skill 1, Skill 2, Ultimate.
TOP row = each icon as a CIRCLE; BOTTOM row = the SAME icon as a ROUNDED SQUARE
(same art in both rows, only the frame shape changes). The three skills:
1) "Bloom Heal": a blooming white-and-green flower with a soft green healing PLUS /
   heart glow, gentle sparkle, green/white/gold.
2) "Thornsnare": coiling thorny green vines twisting into a snare/knot, a few
   sharp thorns, green/brown.
3) "Grove Sanctuary" (grander, more GOLD, epic): a glowing sacred World-Tree /
   grove with radiant golden light and floating leaves, a sanctuary feel.
Same art and colors in both rows — only the frame shape differs (circles on top,
rounded squares below). Solid flat magenta chroma (#FF00FF) background, no text.
```

---

# Hero 5 — Rennick, the Trickshot (แรคคูนมือปืน) 🦝🔫

> **แรคคูน beastfolk มือปืน** เจ้าเล่ห์ว่องไว — ยิ้มกวน แววตาคม ลายหน้ากากแรคคูน หางฟูลาย. ปืนเป็น **ไม้มีชีวิต ยิงเมล็ด/ลูกโอ๊ก**. โทน **น้ำตาล-เทา + green/gold accent** (ไม่จมเขียว)
> เติมความหลากหลาย beastfolk เผ่า Natural (จิ้งจอก/เต่า/แรคคูน)

## (A) Key Art — ท่าโชว์
```
[HERO STYLE BLOCK]
The hero is "Rennick the Trickshot", a lean agile adult male RACCOON beastfolk
gunner with balanced heroic proportions, a mischievous grin, alert expressive eyes,
a dark
raccoon face-mask marking, fluffy round cheeks, raccoon ears, and a big fluffy
striped tail. Warm palette — grey-brown and cream raccoon fur with tan-and-green
gear (NOT all green). Outfit — clever scavenger-ranger: a leaf-and-leather vest, a
bandolier of little seed / acorn ammo pouches across the chest, a small bark-brim
hat or bandana, vine-wrapped bracers, a belt with a gold buckle, small gold
accents. He holds a compact LIVING-WOOD RIFLE / bark blunderbuss — a sturdy
wooden gun with vine wrap, a gold trigger, a leaf sight and an acorn loaded in the
barrel. Cheeky confident gunslinger pose, the gun resting on his shoulder,
winking and grinning at the viewer. Both hands use believable firearm grip anatomy
with five distinct fingers each. Polished stylized hand-painted MOBA concept-art finish,
clean silhouette. Full body, clean white background, no text.
```

## (B) A-Pose Turnaround — ใช้ prompt นี้ร่วมกับ B1 หรือ B2 ทีละแผ่น
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Rennick the Trickshot", the SAME lean agile adult
male RACCOON beastfolk gunner with balanced heroic proportions, alert expressive
eyes, cheeky
grin, dark raccoon face-mask marking, raccoon ears, big fluffy striped tail; warm
grey-brown and cream fur with tan-and-green gear (not all green) — a leaf-and-
leather vest, a bandolier of seed/acorn ammo pouches, a small bark-brim hat, vine
bracers, a belt with a gold buckle, small gold accents.
Use the approved Rennick Key Art as the authoritative identity reference. Append
exactly ONE turnaround block: either (B1) FRONT + BACK or (B2) LEFT + RIGHT; never
request all four views in one image. Exactly the same character, gear, proportions,
texture patterns and colors across both sheets. The B1 back view must clearly show
the bushy tail and bandolier. Standing in a strict A-POSE: BOTH arms straight and
lowered diagonally about 35–45 degrees from the shoulders in EVERY view, complete
and symmetric (not hidden behind the tail). EMPTY OPEN HANDS (no gun — the gun is a
separate prop), angled toward the thighs with a clear air gap, each hand showing exactly
five separated relaxed fingers including the thumb; legs straight and slightly
apart, facing forward. Clearly expose all major joint and deformation zones for
modeling, rigging and skinning. Full body visible, clean white background, no
weapons, no text.
```

## (C) Weapon prop — ปืนไม้ (held, prop แยก)
```
[HERO STYLE BLOCK]
Isolated game prop, NO character, NO hands holding it: the LIVING-WOOD RIFLE /
bark blunderbuss of Rennick the Trickshot — a compact sturdy gun made of warm
brown living wood with green vine wrap along the barrel, a gold trigger and gold
band, a small leaf sight on top, a flared bark muzzle with an acorn peeking out,
and a little leaf charm on the stock. Clean stylized production design. 3 views
in one row, evenly spaced: front, side, back. Clean white background, no text.
```

## (D) Skills — สกิล + ไอคอน 🦝

| สกิล | ชื่อ | ประเภท | คำอธิบาย |
|---|---|---|---|
| **Skill 1** | **Scatter Seeds** · กระสุนกระจาย | Cone spread | ยิงเมล็ด/ลูกโอ๊กกระจายเป็นรูปพัด โดนศัตรูหลายตัวระยะใกล้ |
| **Skill 2** | **Acorn Bomb** · ระเบิดลูกโอ๊ก | AoE burst | ขว้างลูกโอ๊กระเบิด ดาเมจเป็นวงบริเวณจุดตก + ผลักถอย |
| **Ultimate** | **Trickshot Barrage** · รัวพิศดาร | Rapid barrage | รัวยิงกระสุนเมล็ดเด้งสะท้อน (ricochet) โดนศัตรูหลายตัวต่อเนื่องช่วงเวลาหนึ่ง |

```
[SKILL ICON STYLE BLOCK]
Two rows of matching NATURAL skill icons on ONE sheet (2 rows x 3 columns), the
SAME three skills in each row, columns left to right = Skill 1, Skill 2, Ultimate.
TOP row = each icon as a CIRCLE; BOTTOM row = the SAME icon as a ROUNDED SQUARE
(same art in both rows, only the frame shape changes). The three skills:
1) "Scatter Seeds": a fan/cone spread of little brown seeds and acorns flying out,
   with tiny motion streaks, brown/green.
2) "Acorn Bomb": a cute round acorn bomb with a lit fuse and a small burst,
   brown/green/orange.
3) "Trickshot Barrage" (grander, more GOLD, epic): a raccoon-mask emblem behind a
   golden living-wood rifle with a storm of ricocheting seed-bullets and gold
   sparks.
Same art and colors in both rows — only the frame shape differs (circles on top,
rounded squares below). Solid flat magenta chroma (#FF00FF) background, no text.
```

---

# Hero 6 — Kaelor, the Crimson Claw (สิงโตกลายพันธุ์ไฟท์เตอร์) 🦁💎

> **มนุษย์–สิงโตกลายพันธุ์ชาย · Fighter ระยะประชิด** — ใบหน้าเป็น humanoid feline ที่ยังมีโครงหน้ามนุษย์ ไม่ใช่หัวสิงโตเต็มตัว; มีลายแต้มชนเผ่า แผงคอคล้ายเส้นผม และ **คริสตัลเขียว→เหลืองไล่เฉดผุดจากแนวกระดูกสันหลัง** เป็น signature silhouette
> **อาวุธ asymmetrical:** แขนขวาเพียงข้างเดียวติดกงเล็บเหล็กชาดขนาดมหึมา 3 ใบ ยาวเกินปลายนิ้วประมาณสองเท่าของช่วงมือ; แขนซ้ายเป็นมือเปล่าสำหรับบาลานซ์ silhouette และ animation
> **palette เฉพาะตัว:** สีธรรมชาติเป็นหลัก — sand, warm cream, ochre, raw umber, charcoal, muted terracotta; คริสตัลไล่เฉด moss-green → chartreuse → warm yellow ส่วนแดงชาดสงวนไว้ที่กงเล็บเท่านั้น
> **production note:** ผลึกหลังเป็น rigid modular clusters parent กับ spine/chest bones และเว้น shoulder deformation zone; กงเล็บเป็น attached weapon; **กางเกง/hip panels เป็น modular equipment แยกจาก base body**

## (A) Key Art — ท่าโชว์
```
[HERO STYLE BLOCK]
The hero is "Kaelor the Crimson Claw", an adult male MUTATED HUMAN-LION hybrid
melee fighter with powerful athletic heroic proportions — broad shoulders and chest,
a narrow combat-ready waist and long muscular readable limbs. He must NOT have a
literal realistic lion head. FACE IDENTITY: handsome but feral humanoid facial planes,
human-like cheekbones and jaw, a very short subtle feline muzzle, a small dark feline
nose, intense amber cat eyes, expressive human-like brows, pointed lion ears and only
partial tawny fur along the cheeks, temples and neck. A layered swept-back mane reads
more like thick wild hair mixed with fur. Decorate the face with asymmetric charcoal
and muted-terracotta tribal markings around one eye, fine gold inlay scars across the
opposite cheek, two small crystal studs at the brow and a bone ear ornament. Original
design; use the references only for human/feline balance and ornamental density.

MUTATION SIGNATURE: physical clusters of TRANSLUCENT GREEN-TO-YELLOW GRADIENT
CRYSTAL grow from his upper back and spine, visible behind the shoulders and mane.
Use a readable large/medium/small rhythm: one dominant faceted shard rising behind
the RIGHT shoulder blade, three medium shards stepping down the spine, and several
tiny crystal buds. Every large and medium shard grades from deep moss-green at its
root through fresh leaf-green and luminous chartreuse to warm clear yellow at its
tip. Add dark stone inclusions and subtle internal yellow veins, but NO gold-colored
crystal and NO metallic gold coating. NOT forged armor, NOT floating and NOT an aura.
Crystal roots stay on the torso/spine and do not cross shoulder or waist deformation.

NATURAL PERSONAL PALETTE: sand, warm cream, ochre, raw umber, charcoal and muted
terracotta dominate. Green-to-yellow gradient color belongs mainly to the natural
back crystals; hardware is aged dark bronze rather than bright gold. Reserve saturated
crimson red almost entirely for the giant weapon. Add one tiny desaturated moss-green
cord as the Natural-faction accent. Avoid a shiny
red-and-gold royal armor look; materials should feel weathered, earthen and organic.

OUTFIT: asymmetrical nomadic pit-fighter gear — fitted cream-and-ochre wrap vest,
charcoal leather harness that frames rather than hides the chest, one compact layered
shoulder guard on the non-weapon side, raw-leather waist belt, short split terracotta
and dark-brown hip panels ending above mid-thigh, fitted under-shorts, wrapped shins
and practical open-joint boots. Bone toggles, rough stitching and small hammered-gold
fasteners. The visible short fighter trousers and hip panels are a separate wearable
equipment set over a plain fitted base under-short. Clear attachment points; no rigid
plate crosses any major joint.

ASYMMETRICAL ATTACHED WEAPON: ONLY Kaelor's RIGHT arm carries one enormous open-palm
crimson claw gauntlet with exactly THREE massive forward-swept red metal blades. Each
blade is broad, heavy and approximately the combined length of his forearm and hand,
extending about twice the hand length beyond the fingertips. The middle blade is
slightly longest, creating a bold predatory silhouette. Dark scarlet steel faces,
blackened bevels, mineral-like chips, aged-bronze sockets and a charcoal leather mount.
The rigid rail sits on the back of the right hand and joins a separate forearm cuff,
with a clear wrist gap. The LEFT arm has NO weapon, NO blade and NO matching gauntlet:
it ends in one visible relaxed five-fingered humanoid-feline hand. Both hands have
exactly five anatomical fingers including one thumb.

Dynamic three-quarter combat stance that shows the huge right claw in side silhouette
without hiding the decorated face or green-yellow gradient back crystals. The bare left hand guards
his torso. Confident predatory gaze, no extreme foreshortening.
Full body fully visible, clean pure white background, no text, no logo, no watermark.
```

## (B) A-Pose Turnaround — attached claws ต้องติดอยู่ใน B1/B2
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Kaelor the Crimson Claw", the SAME mutated adult
male HUMAN-LION hybrid fighter from the approved Key Art: athletic heroic build,
handsome humanoid facial planes, short subtle feline muzzle, partial cheek/neck fur,
wild hair-like tawny mane, lion ears, amber eyes, asymmetric charcoal/terracotta face
paint, fine bronze facial inlay scars, long tail and the exact physical green-to-yellow
gradient crystal clusters rooted along his upper back and spine. Preserve the natural sand, cream,
ochre, raw-umber, charcoal and muted-terracotta palette. Preserve the asymmetrical
nomadic fighter outfit construction.

Use the approved Kaelor Key Art as the authoritative identity reference. Append
exactly ONE turnaround block: either (B1) FRONT + BACK or (B2) LEFT + RIGHT; never
request all four views in one image. Exactly the same character, proportions, face
decorations, crystal count/placement, tail, gear, textures and colors across both
sheets. BASE-BODY RULE: remove Kaelor's designed trousers, belt-mounted hip panels
and all hanging waist cloth from B1/B2. The body wears only plain fitted warm-grey
modest rigging under-shorts ending high on the thigh, with no decorative pattern, so
the waist, pelvis, hip crease and thighs are fully readable. The removed trousers,
hip panels and long hanging tabard are one combined lower-body equipment set documented
in P1/P2. Strict neutral A-POSE: both arms straight and lowered diagonally about
35–45 degrees from the shoulders with a clear hand-to-hip gap, legs straight and
slightly apart, all major deformation
zones clearly exposed.

IMPORTANT ATTACHED-WEAPON EXCEPTION: Kaelor's single RIGHT-arm giant crimson claw
gauntlet is a permanent part of his model, so it MUST remain equipped in every view.
This overrides any generic turnaround phrase saying "empty hands", "no weapon" or
"no weapons". ONLY THE RIGHT HAND carries exactly THREE enormous red metal blades;
the left hand has zero blades and no matching gauntlet. The middle blade is slightly
longest; all three extend about twice the hand length beyond the fingertips. The right
mount stops before the wrist crease and leaves finger joints free. Both hands still
show five separated real fingers including one thumb. In the back view clearly expose
the green-yellow crystal roots and their safe gaps from shoulder joints. Full mane, crystal
tips, ear tips, blade tips, fingertips, tail and feet inside the canvas.
Clean pure white background, no text, no labels, no extra props.
```

## (P) Combined modular trousers + tabard — รวมเป็น equipment ชิ้นเดียว
```
[HERO STYLE BLOCK]
Create Kaelor's removable COMBINED LOWER-BODY EQUIPMENT SET completely separated
from the character body. The trousers and all hanging cloth are permanently assembled
as ONE wearable game-equipment asset; do not split them into separate P and G objects.
The combined design includes:
fitted charcoal-and-raw-umber short trousers ending at the upper thigh, a sturdy
raw-leather waistband, two short asymmetrical ochre / muted-terracotta hip panels,
one long central muted-terracotta FRONT tabard reaching approximately to the knee,
two narrower split raw-umber / dark-brown BACK tails with warm-cream lining, small
bone toggles, rough stitching, aged-bronze fasteners, and one tiny moss-green braided
cord. The long panels are sewn and hooked directly into the same waistband as the
trousers; there is no second belt and no detachable G garment.

Match the approved Key Art and B1 base body's exact waist, pelvis and upper-thigh
scale. Show a clean hollow waist/hip volume and two empty leg openings; do not render
skin or a full mannequin. Clearly show waistband anchors, fly/closure, crotch topology,
seat construction, inner-leg seams, fabric thickness, long-panel attachment seams,
lining and enough clearance for hip, thigh and leg deformation. The fitted trousers
share the master pelvis/thigh bones; the long front/back panels use dedicated child
cloth bones under the same lower-body equipment prefab in Unity.

Create only ONE P sheet per generation:
- P1 = exactly TWO orthographic views, FRONT and BACK.
- P2 = exactly TWO orthographic profiles, LEFT and RIGHT, matching approved P1.
Pure white background, no body, no skin, no visible legs, no tail, no crystals, no
weapon, no separate garment floating beside the trousers, no text.
```

## (C) Attached claw construction sheet — ติดกับโมเดล ไม่ใช่ prop ถือแยก
```
[HERO STYLE BLOCK]
Production design sheet for Kaelor's SINGLE PERMANENTLY ATTACHED giant crimson
RIGHT claw-gauntlet assembly, not a handheld prop. Show exactly FOUR large isolated
technical views on one clean white sheet: RIGHT-GAUNTLET top view, palm view, profile and
three-quarter assembly view. Include a simplified neutral lion hand and short forearm
inside the gauntlet only so attachment and articulation are unambiguous; no full
character. The hand is anatomically complete with five separated fingers and one
thumb. Open-palm construction leaves every finger joint and the palm visible.

Exactly THREE MASSIVE forward-swept steel blades attach to a rigid aged-bronze and
blackened-metal rail on the BACK of the right hand. Each blade is approximately the
combined length of forearm and hand and extends about twice the hand length beyond the
fingertips; the middle blade is slightly longest. Dark scarlet faces, blackened bevels,
mineral chips, aged-bronze sockets, charcoal leather straps and one tiny moss-green inlay.
The rail continues to a separate forearm cuff but stops before the wrist crease. Show
believable heavy thickness, safe gaps between blades, wrist flex
clearance, finger curl clearance and clean parenting zones for hand and forearm bones.
No blade may originate from the fingertips; no plate crosses the wrist; no loose
chains, no glow, no energy, no text, no labels, no arrows, no watermark.
```

## (D) Skills — สกิล + ไอคอน 🦁

| สกิล | ชื่อ | ประเภท | คำอธิบาย |
|---|---|---|---|
| **Skill 1** | **Crimson Pounce** · โผกรงเล็บชาด | Gap close / slash | กระโจนระยะสั้นแล้วกวาดกงเล็บยักษ์ข้างขวา สร้างดาเมจกายภาพและติดเลือดไหลช่วงสั้น |
| **Skill 2** | **Pridebreaker Roar** · คำรามสยบศึก | Cone control | คำรามเป็นกรวยระยะใกล้ ทำดาเมจและทำให้ศัตรูชะงัก; Kaelor ได้เกราะชั่วคราวตามจำนวนศัตรูที่โดน |
| **Ultimate** | **Verdant Apex** · กลายพันธุ์ยอดนักล่า | Self-buff / finisher | คริสตัลเขียว→เหลืองตามหลังขยายตัว เพิ่มเกราะและความเร็ว; กงเล็บยักษ์ชาร์จพลังแร่ก่อนปิดท้ายด้วยการกวาดครึ่งวงกว้าง |

```
[SKILL ICON STYLE BLOCK]
Two rows of matching NATURAL skill icons on ONE sheet (2 rows x 3 columns), the
SAME three skills in each row, columns left to right = Skill 1, Skill 2, Ultimate.
TOP row = each icon as a CIRCLE; BOTTOM row = the SAME icon as a ROUNDED SQUARE
(same art in both rows, only the frame shape changes). Give this hero a distinct
natural sand / ochre / raw-umber / charcoal palette, green-to-yellow crystal highlights,
crimson reserved for the weapon and only tiny moss-green faction
accents. The three skills:
1) "Crimson Pounce": one giant three-blade crimson claw lunging diagonally through
   a sharp forward chevron, red/orange with a tiny green knot at the base.
2) "Pridebreaker Roar": a decorated humanoid-feline mask roaring into a short ochre
   cone of force, with a small dark shield shape behind it.
3) "Verdant Apex" (grander, epic): one giant crimson three-blade claw in front of
   an ascending fan of translucent crystals grading moss-green to chartreuse to
   warm yellow, with dark stone roots, crimson/green/yellow/charcoal.
Same art and colors in both rows — only the frame shape differs. Solid flat magenta
chroma (#FF00FF) background outside every icon frame, no text, no letters, no numbers.
```

---

# 🎮 ปุ่มหลักของเกม (ทุกเผ่า) — Attack / Blink / Heal

> ปุ่ม HUD **หลักที่ใช้ทุกเผ่า** (ไม่ผูกเผ่า Natural) — **วงกลม ไล่เฉด ขาว→ฟ้า→ม่วง บนพื้นดำ ไม่มี glow** มินิมอลเรียบโมเดิร์น. เข้าชุดกับ `conceptArt-UI.md`
> ✅ **ทำเป็น set แผ่นเดียว** (3 ปุ่มในภาพเดียว) เป็นหลัก — พื้นดำทึบคีย์ออกง่าย. มี prompt เดี่ยวเผื่อ regen เฉพาะปุ่ม
> gen **state ปกติ** ก่อน; **กด(pressed)** = เข้มลง/เล็กลงนิด, **ปิด(disabled)** = เทา-ซีด

## 🎨 BUTTON STYLE BLOCK (แปะหน้า prompt ปุ่มทุกอัน)

```
Clean minimal round game control button — a perfect CIRCLE with a smooth flat
GRADIENT going from WHITE to light BLUE to soft PURPLE. Modern, understated, FLAT —
absolutely NO glow, NO halo, NO neon outline, NO glossy candy shine, NO gold, no
ornament. The emblem is a simple clean symbol centered, minimal and instantly
readable, in a CONTRASTING tone so it stays clear over the gradient (white with a
subtle soft edge on the lighter part, or a deep violet line). UNIVERSAL UI shared by
every faction. No text, no letters, no numbers. Background = SOLID PURE BLACK (flat
#000000, fully filled, NOT transparent, NOT dark-navy) so it keys out cleanly.
```

## 🖼️ ปุ่มทั้งชุด — 1 แผ่นเดียว (set sheet) ⭐ หลัก
```
[BUTTON STYLE BLOCK]
A SET SHEET of the 3 main HUD control buttons, ALL on ONE image: three perfect
CIRCLE buttons in a single horizontal row, evenly spaced, SAME size, each with the
SAME smooth flat white-to-blue-to-purple gradient (no glow), on one solid pure
black background. Left to right, each with a clean readable emblem centered:
1) ATTACK — a bold clenched FIST punching toward the viewer.
2) BLINK / SPRINT — a silhouette of a person leaping / dashing forward with a
   sparkle trail behind them.
3) HEAL — a rounded PLUS merged with a small heart.
Minimal, modern, flat, no glow. No text, no letters, no numbers.
```

**emblem อ้างอิง (วงกลม ไล่เฉด ขาว→ฟ้า→ม่วง พื้นดำ):** 👊 Attack = กำปั้น · ⚡ Blink = เงาคนกระโดดพุ่ง+ประกายตามหลัง · ✚ Heal = plus+heart

> เผื่อ regen ทีละปุ่ม (อันไหนออกไม่สวย) ใช้ prompt เดี่ยวด้านล่าง — แปะ BUTTON STYLE BLOCK หน้า

## (เดี่ยว) Attack — กำปั้น 👊
```
[BUTTON STYLE BLOCK]
One single CIRCLE button, centered. The emblem is a bold clenched FIST punching
toward the viewer (knuckles-forward), a clean readable symbol on the
white-to-blue-to-purple gradient circle.
```

## (เดี่ยว) Blink / Sprint — พุ่งตัว ⚡
```
[BUTTON STYLE BLOCK]
One single CIRCLE button, centered. The emblem is a clean SILHOUETTE of a person
LEAPING / dashing forward (dynamic mid-leap, body leaning forward), with a trailing
streak of sparkles / light motes fanning out behind them (a blink-dash trail), on
the white-to-blue-to-purple gradient circle.
```

## (เดี่ยว) Heal — ฟื้นพลัง ✚
```
[BUTTON STYLE BLOCK]
One single CIRCLE button, centered. The emblem is a HEAL symbol — a soft rounded
PLUS / cross merged with a small heart, a clean readable symbol on the
white-to-blue-to-purple gradient circle.
```

---

## 📝 Checklist (hero)

- [ ] ดีเทล/ท่าทาง **โดดเด่นกว่ามอนชัดเจน** (นี่คือตัวชูโรง)
- [ ] **premium stylized fantasy MOBA** — สัดส่วน heroic 7–7.5 หัว, silhouette อ่านออกจากมุม top-down
- [ ] hero แต่ละตัวมี dominant palette ของตัวเองได้; ความเป็น Natural มาจาก organic motif/material/pattern และ faction accent เล็กน้อย — **ไม่ต้องเป็นโทนเดียวกันทั้งเผ่า**
- [ ] **ไม่มีออร่า/particle รอบตัว** (เรืองเฉพาะบนตัว/คมดาบ)
- [ ] T-pose: anatomy/ข้อต่อชัด แขนเหยียดตรงแนวนอนระดับไหล่ ฝ่ามือคว่ำ และมีนิ้วครบข้างละ 5 นิ้ว; held weapon/โล่ให้ถอดเป็น prop, แต่ attached weapon ถาวรให้ติดอยู่กับ body และไม่ขวางข้อมือ/นิ้ว
- [ ] Beastfolk tail: โคนอยู่กึ่งกลาง sacrum, ชี้ตรงไปด้านหลังและขนานพื้น; Front/Back ต้อง foreshorten, Left/Right เห็นความยาวเต็ม; ห้ามหางแตะมือ
- [ ] ชุด/เกราะไม่ขวาง shoulder, elbow, wrist, hip, knee, ankle
- [ ] garment ที่ยาวเกินกลางต้นขา: ถอดจาก body B1/B2 → body ใส่ fitted under-suit/กางเกงขาสั้น → สร้าง garment G1/G2 แยกเป็น equipment
- [ ] modular garment ใช้ waist/hip scale และ master skeleton เดียวกับ body; เพิ่ม dedicated skirt/cloth bones ใต้เอว
- [ ] แขนเสื้อยาวที่ย้อยอยู่กับ upper-body outfit ได้ แต่ต้องแยกเป็น secondary-bone/cloth pieces และไม่ merge กับแขน
- [ ] Turnaround แยก **Front / Back / Left / Right เป็น 4 ไฟล์ 4K** — หนึ่งไฟล์มีตัวละครหนึ่งตัวเท่านั้น
- [ ] ตัวละครสูงประมาณ 82–88% ของแต่ละ sheet เพื่อเก็บ texture/pattern detail และมี white space รอบปลายหู/นิ้ว/หาง/เท้า
- [ ] **ทุกมุมแขน/ขาครบ 2 ข้าง** — โดยเฉพาะ back view (AI ชอบตัด/บังแขนหลังกระดอง). ถ้าขาด → re-gen หรือ inpaint แขนที่หายใน Photoshop
- [ ] ทุกมุมใช้ Approved Key Art เดียวกัน; แนบ Front master ใน Back/Left และแนบ Front+Left ใน Right เพื่อคุม identity/texture consistency
- [ ] Key art กับ Turnaround = **ตัวละคร/ชุด/สีเดียวกันเป๊ะ**
- [ ] **Skills**: Skill1/Skill2/Ultimate มีชื่อ+คำอธิบาย ครบทุก hero
- [ ] **Skill icons**: อ่านออกที่ขนาดเล็ก, Ultimate เด่น/ทองกว่า, เข้าชุดกัน
- [ ] **ปุ่มหลัก** (Attack/Blink/Heal): วงกลม ไล่เฉด ขาว→ฟ้า→ม่วง พื้นดำ ไม่มี glow, emblem ชัด, ใช้ได้ทุกเผ่า
- [ ] พื้นขาว ไม่มีลายเซ็น/ข้อความ

---

## ทำต่อ

- **เผ่าอื่น:** `conceptArtHero-Human.md` / `-Galax.md` / `-Darkside.md` — โครงไฟล์เดียวกัน (STYLE BLOCK/SKILL ICON BLOCK เดิม เปลี่ยน palette+ธีม) + ออกแบบ **skills+icons ต่อ hero** เหมือนกัน
- **ปุ่มหลัก (Attack/Blink/Heal)** = ทำครั้งเดียว ใช้ทุกเผ่า (อยู่ไฟล์นี้ + เข้าชุด `conceptArt-UI.md`)
- **skin/tier ของ hero** (ถ้าจะทำ): ออกแบบชุด variant บนโครงเดิม
