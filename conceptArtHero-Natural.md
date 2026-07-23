# Concept Art Prompts — HEROES · เผ่า NATURAL 🌿⚔️

> **Hero = ตัวชูโรง** — ดีเทลเยอะกว่ามอน, ชัดเจน โดดเด่น มีคาแรกเตอร์/ท่าทาง. เผ่าเดียวกับ `conceptArt-Natural.md` (Beast/ธรรมชาติ: leafy green / earthy brown / terracotta / bone-white) แต่ **ยกระดับความ heroic**
> **สไตล์:** **cute stylized 3D น่ารักสุดๆ** (Supercell / First Fantasy) — chibi หัวโต ตากลมโตวาว มน soft glossy toon, ท่ายืนไดนามิกเท่ๆ. ต่างจากมอนตรง **ดีเทล/คาแรกเตอร์จัดกว่า** (เป็นตัวชูโรง)

---

## Hero ของเผ่า Natural (5 ตัว)
| # | สาย | เพศ | สถานะ |
|---|---|---|---|
| **1** | **นักดาบ (Swordsman)** | ชาย | ✅ Rowan จิ้งจอกนักดาบ |
| **2** | **แท้งค์ (Tank)** | ชาย | ✅ Torvin เต่ายักษ์ |
| **3** | **ธนู (Archer)** | หญิง | ✅ Elara นักธนู fae |
| **4** | **เวท (Mage)** | หญิง | ✅ Elowen ผู้พิทักษ์ป่า |
| **5** | **มือปืน (Gunner)** | ชาย | ✅ Rennick แรคคูนมือปืน (ด้านล่าง) |

> ✅ **ครบทีมเผ่า Natural 5 ตัว** — beastfolk 3 (จิ้งจอก/เต่า/แรคคูน) + fae/elf 2 (ธนู/เวท)
> 🎨 สไตล์ทั้งหมด = **cute stylized 3D น่ารักสุดๆ** (Supercell/First Fantasy)

---

## วิธีใช้ / สิ่งที่ได้ต่อ hero

แต่ละ hero มี **4 ชุด**:
- **(A) Key Art** — ท่าโชว์ไดนามิก ถืออาวุธ → การ์ด hero / จอ hero select / โปรโมท
- **(B) T-Pose Turnaround** — 4 มุม มือเปล่า → ใช้ **ปั้น 3D + rig** จริง (แขน/ขายาว rig-friendly, ไม่มีออร่ารอบตัว)
- **(C) Weapon prop** — อาวุธแยก (held weapon) เอาไปแปะมือตอน rig
- **(D) Skills + Icons** — Skill 1 / Skill 2 / Ultimate (ชื่อ+คำอธิบาย) พร้อม prompt ไอคอนสกิล (ชุด 3 อันเข้ากัน)

---

## 🎨 HERO STYLE BLOCK (แปะหน้า prompt ทุกภาพ)

```
Super CUTE stylized game-hero character art — adorable premium mobile-game look
like Supercell and "First Fantasy" cute stylized 3D heroes. Proportions: CHIBI —
very BIG head, small chunky rounded body, BIG glossy sparkly expressive eyes, soft
rounded adorable shapes; SLIGHTLY LONGER and clearly separated arms and legs
(rig-friendly, room to bend at elbows and knees). RENDERING: smooth soft glossy
3D-style toon shading with gentle highlights and a soft rim light, clean, polished,
colorful and juicy, SUPER appealing and cute. It's a HERO — nicely detailed and
premium — but ADORABLE first; keep it clean and readable, NOT cluttered, NOT
gritty, NOT realistic, NOT washed-out.
NATURE-HERO MOTIF: Nature heroes are cute mythical forest folk — BEASTFOLK
(fox / raccoon / turtle style animal-people: fur or scales, animal ears, tail) OR
FAE / ELF (pointed ears, small fairy wings, flower/leaf crown). A FEW small tribal
GREEN accents and small GOLD trim. Do NOT make the whole character green — use
warm tans / browns / creams for skin or fur and keep green as ACCENTS only, so the
palette stays rich and varied.
NATURAL faction palette: warm tan and earthy brown, leafy green as accents,
terracotta red-orange, teal jewel touches, bone/cream-white, small gold; warm
forest tones.
NO glowing aura / no energy swirls / no floating particles AROUND the
character (magic shows only as ON-BODY glow — runes, glowing blade edge, glowing
eyes). Clean pure white background, full body fully visible, single character,
no text, no letters, no signature, no watermark.
```

---

## 🎯 SKILL ICON STYLE BLOCK (แปะหน้า prompt ไอคอนสกิลทุกอัน)

```
Mobile game SKILL ICON in the given FRAME SHAPE (a circle, or a rounded square). A
single bold readable emblem centered, in the same CUTE stylized toon look as the heroes
(Supercell / First Fantasy): thick clean dark outline, smooth glossy shading, a
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

> **จิ้งจอก beastfolk น่ารัก** สายนักดาบ — ตากลมโต ยิ้มกวนๆ หูจิ้งจอก+ห่วงทอง ลายรูนเขียวบนหน้า หางฟู. ชุด tunic เขียวลาย knotwork + ทอง (โทน/หน้า/ชุดแบบ ref 2 แต่ **น่ารักเข้า theme เกม** ไม่จริงจัง). ดาบไม้มีชีวิตคมเรืองเขียว
> **beastfolk = สัตว์อยู่แล้ว** จึงไม่ต้องมีเขา (motif เผ่า = beastfolk น่ารัก + ทอง + ลายรูนเขียว)

## (A) Key Art — ท่าโชว์ (ตาม ref)
```
[HERO STYLE BLOCK]
The hero is "Rowan the Wildblade", a young FOX beastfolk swordsman: a fluffy
cream-and-white fox with big expressive green (yellow-green) eyes, a sharp
cool-yet-charming face, large expressive fox ears with little gold ear-cuffs,
INTRICATE glowing tribal GREEN knotwork markings on his forehead and cheeks, and
a fluffy striped fox tail. Slim agile build. Gear — richly detailed, heroic and
refined, forest tones with GOLD accents (an elegant forest tribe): a fitted
GREEN tunic-vest with intricate green celtic-knotwork rune patterns and gold trim
(agile swordsman garb, NOT a big bulky mage robe), a brown sash and belt with a
gold clasp, a short layered leaf-and-cloth waist wrap, leather bracers with gold
rune inlays, wrapped brown boots with gold buckles. He wields an ORNATE
LIVING-WOOD KATANA with gold fittings and a soft-green glowing edge, a
bamboo-wrapped hilt with a tiny leaf tassel. Confident cool pose, blade resting
back over one shoulder, looking at the viewer with a cheeky grin. Full body,
clean white background, no text.
```

## (B) T-Pose Turnaround — สำหรับปั้น 3D + rig
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Rowan the Wildblade", the SAME young FOX beastfolk
swordsman: fluffy cream-and-white fox, big expressive yellow-green eyes, a sharp
charming face, large fox ears with gold ear-cuffs, intricate glowing tribal green
knotwork markings on the face, a fluffy striped fox tail; richly detailed heroic
forest gear with GOLD accents — a fitted green knotwork tunic-vest with gold trim, a
brown sash and belt with a gold clasp, a short layered leaf-and-cloth waist wrap,
leather bracers with gold rune inlays, wrapped brown boots with gold buckles.
4 views in ONE row, evenly spaced, no overlapping, left to right: front view,
back view, left side view, right side view. Exactly the same character, gear,
size and colors in all 4 views. Standing in a strict T-POSE: BOTH arms spread
fully straight out to the sides in EVERY view — in the BACK view both arms are
complete, fully visible and symmetric (not hidden or cut off). EMPTY HANDS (no
weapon — the sword is a separate prop), legs straight and slightly apart, facing
forward. Full body visible, clean white background, no weapons, no text.
```

## (C) Weapon prop — ดาบ (held, prop แยก)
```
[HERO STYLE BLOCK]
Isolated game prop, NO character, NO hands holding it: the ORNATE LIVING-WOOD
KATANA of Rowan the Wildblade — a slim gently-curved blade whose cutting edge is
a soft glowing green leaf-edge, the flat of the blade is pale bone-white wood
with GOLD rune inlays, a bamboo-wrapped hilt in brown and green with a polished
GOLD carved-leaf guard and a tiny leaf tassel. 3 views in one row, evenly
spaced: front, side, back. Clean white background, no text.
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
The hero is "Torvin the Bulwark", a HUGE hulking male TURTLE beastfolk tank: a
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

## (B) T-Pose Turnaround — สำหรับปั้น 3D + rig
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
4 views in ONE row, evenly spaced, no overlapping, left to right: front view,
back view, left side view, right side view. Exactly the same character, gear,
size and colors in all 4 views (the back view clearly shows the spiky shell).
Standing in a strict T-POSE: BOTH arms spread fully straight out to the sides in
EVERY view — in the BACK view both arms are complete, fully visible and
symmetric (not hidden or cut off behind the shell); the shell does not cover the
arms. EMPTY HANDS (no weapon — the shield is a separate prop), legs straight and
slightly apart, facing forward. Full body visible, clean white background, no
weapons, no text.
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

> **สาวน้อย fae/elf นักธนู** น่ารัก — หูแหลม, มงกุฎใบไม้ทอง, ปีกภูติใสเล็กๆ, ผมหางม้าน้ำตาล. ชุด **ทอง/ครีม + คลุมเขียวใบไม้** โทนอุ่น (ไม่จมเขียว) พร้อมธนูไม้ + กระบอกลูกธนู
> 🌿 **ขยาย motif เผ่า:** Nature hero เป็นได้ทั้ง **beastfolk (จิ้งจอก/เต่า)** และ **fae/elf (หูแหลม+ปีกภูติ+มงกุฎใบไม้)** — ทั้งคู่ = ชาวป่าเวทมนตร์ ไม่ใช่คนธรรมดา

## (A) Key Art — ท่าโชว์ (ตาม ref)
```
[HERO STYLE BLOCK]
The hero is "Elara the Leafshot", a CUTE young female fae/elf nature archer:
petite chibi girl with big pretty eyes, pointed elf ears, a long brown ponytail,
a delicate GOLD laurel-leaf crown, small iridescent translucent fairy wings on
her back, tiny green leaf earrings. Outfit — dainty and heroic, WARM palette (NOT
mostly green): a gold-and-cream corset dress with a brown leather bodice and gold
trim, a green round gem brooch at the collar, a flowing leafy-green watercolor
CAPE with a soft gold-edged hem, cream/white leggings, and cute green leaf-vine
boots with gold accents; a slim vine wrap and bracer on her bow arm; a small
leather quiver of arrows at her hip wrapped in vines. She holds a slender wooden
RECURVE BOW. Light graceful pose, cape flowing, looking off with a gentle
confident air. Soft painterly / watercolor finish, clean simple silhouette. Full
body, clean white background, no text.
```

## (B) T-Pose Turnaround — สำหรับปั้น 3D + rig
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Elara the Leafshot", the SAME cute young female
fae/elf archer: petite chibi, pointed elf ears, long brown ponytail, gold
laurel-leaf crown, small iridescent fairy wings, green leaf earrings; WARM
palette (not mostly green) — a gold-and-cream corset dress with brown leather
bodice and gold trim, a green gem brooch, a leafy-green cape with gold-edged hem,
cream leggings, green leaf-vine boots with gold, a vine bracer on the bow arm, a
small vine-wrapped quiver of arrows at the hip.
4 views in ONE row, evenly spaced, no overlapping, left to right: front view,
back view, left side view, right side view. Exactly the same character, gear,
size and colors in all 4 views (back view shows the cape, ponytail and wings).
Standing in a strict T-POSE: BOTH arms spread fully straight out to the sides in
EVERY view, complete and symmetric (not hidden behind the cape or wings). EMPTY
HANDS (no weapon — the bow is a separate prop), legs straight and slightly apart,
facing forward. Full body visible, clean white background, no weapons, no text.
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
The hero is "Elowen the Grovekeeper", a graceful CUTE young female fae/elf nature
mage: petite chibi with big gentle green eyes, a soft blush, pointed elf ears,
very long flowing braided platinum-cream hair, a delicate crown of small WHITE
flowers and green leaves with a cute little leaf sprout on top. Outfit — elegant
and dainty, WARM LIGHT palette (mostly white/cream with green, NOT all green): a
flowing white-and-cream druid robe-dress with a green under-gown, soft gold trim,
green gem clasps and small leaf motifs, layered petal-like sleeves. She holds a
tall GNARLED LIVING-WOOD STAFF (a natural twisting branch) topped with a green
gem and small leaves, with a single green butterfly resting ON the staff top (no
floating swarm around her). Serene elegant pose, robe and hair flowing softly.
Soft painterly / watercolor finish, clean silhouette. Full body, clean white
background, no text.
```

## (B) T-Pose Turnaround — สำหรับปั้น 3D + rig
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Elowen the Grovekeeper", the SAME graceful cute
young female fae/elf mage: petite chibi, big gentle green eyes, pointed elf ears,
very long flowing braided platinum-cream hair, a crown of small white flowers and
green leaves with a little leaf sprout on top; elegant WARM LIGHT outfit (mostly
white/cream with green accents, NOT all green) — a flowing white-and-cream
robe-dress with a green under-gown, soft gold trim, green gem clasps, small leaf
motifs, layered petal-like sleeves.
4 views in ONE row, evenly spaced, no overlapping, left to right: front view,
back view, left side view, right side view. Exactly the same character, gear,
size and colors in all 4 views (back view shows the long braided hair and robe).
Standing in a strict T-POSE: BOTH arms spread fully straight out to the sides in
EVERY view, complete and symmetric (not hidden behind the hair or robe). EMPTY
HANDS (no staff — the staff is a separate prop), legs straight, facing forward.
Full body visible, clean white background, no weapons, no floating butterflies,
no text.
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

> **แรคคูน beastfolk มือปืน** เจ้าเล่ห์น่ารัก — ยิ้มกวน ตากลมโต ลายหน้ากากแรคคูน หางฟูลาย. ปืนเป็น **ไม้มีชีวิต ยิงเมล็ด/ลูกโอ๊ก**. โทน **น้ำตาล-เทา + green/gold accent** (ไม่จมเขียว)
> เติมความหลากหลาย beastfolk เผ่า Natural (จิ้งจอก/เต่า/แรคคูน)

## (A) Key Art — ท่าโชว์
```
[HERO STYLE BLOCK]
The hero is "Rennick the Trickshot", a CUTE cheeky male RACCOON beastfolk gunner:
a small chunky chibi raccoon with a big mischievous grin, big glossy eyes, a dark
raccoon face-mask marking, fluffy round cheeks, raccoon ears, and a big fluffy
striped tail. Warm palette — grey-brown and cream raccoon fur with tan-and-green
gear (NOT all green). Outfit — cute scavenger-ranger: a leaf-and-leather vest, a
bandolier of little seed / acorn ammo pouches across the chest, a small bark-brim
hat or bandana, vine-wrapped bracers, a belt with a gold buckle, small gold
accents. He holds a chunky cute LIVING-WOOD RIFLE / bark blunderbuss — a stubby
wooden gun with vine wrap, a gold trigger, a leaf sight and an acorn loaded in the
barrel. Cheeky confident gunslinger pose, the gun resting on his shoulder,
winking and grinning at the viewer. Soft glossy cute stylized 3D finish, clean
silhouette. Full body, clean white background, no text.
```

## (B) T-Pose Turnaround — สำหรับปั้น 3D + rig
```
[HERO STYLE BLOCK]
Character turnaround sheet of "Rennick the Trickshot", the SAME cute cheeky male
RACCOON beastfolk gunner: small chunky chibi raccoon, big glossy eyes, cheeky
grin, dark raccoon face-mask marking, raccoon ears, big fluffy striped tail; warm
grey-brown and cream fur with tan-and-green gear (not all green) — a leaf-and-
leather vest, a bandolier of seed/acorn ammo pouches, a small bark-brim hat, vine
bracers, a belt with a gold buckle, small gold accents.
4 views in ONE row, evenly spaced, no overlapping, left to right: front view,
back view, left side view, right side view. Exactly the same character, gear,
size and colors in all 4 views (back view shows the bushy tail and bandolier).
Standing in a strict T-POSE: BOTH arms spread fully straight out to the sides in
EVERY view, complete and symmetric (not hidden behind the tail). EMPTY HANDS (no
gun — the gun is a separate prop), legs straight and slightly apart, facing
forward. Full body visible, clean white background, no weapons, no text.
```

## (C) Weapon prop — ปืนไม้ (held, prop แยก)
```
[HERO STYLE BLOCK]
Isolated game prop, NO character, NO hands holding it: the LIVING-WOOD RIFLE /
bark blunderbuss of Rennick the Trickshot — a chunky cute stubby gun made of warm
brown living wood with green vine wrap along the barrel, a gold trigger and gold
band, a small leaf sight on top, a flared bark muzzle with an acorn peeking out,
and a little leaf charm on the stock. Simple clean cute stylized design. 3 views
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
- [ ] **cute stylized 3D น่ารักสุดๆ** (Supercell/First Fantasy) — ตากลมโต วาว มน adorable
- [ ] palette เผ่า Natural (tan/brown เป็นหลัก, green เป็น accent — ไม่จมเขียว)
- [ ] **ไม่มีออร่า/particle รอบตัว** (เรืองเฉพาะบนตัว/คมดาบ)
- [ ] T-pose: แขน/ขายาว rig-friendly, **มือเปล่า** (ดาบแยก prop)
- [ ] **ทุกมุมแขน/ขาครบ 2 ข้าง** — โดยเฉพาะ back view (AI ชอบตัด/บังแขนหลังกระดอง). ถ้าขาด → re-gen หรือ inpaint แขนที่หายใน Photoshop
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
