# Concept Art Prompts — เผ่า NATURAL (Beast) 🌿

> ใช้ส่งให้ ChatGPT (GPT-4o image) สร้าง concept art. สไตล์หลัก: **น่ารัก chibi เล่นแล้วสนุกไม่เครียด** (ref: Brawl Stars Spike / Defense Derby chibi / cute monster sticker)
> เผ่า Natural = ธีมสัตว์ป่า/ไดโนเสาร์/ธรรมชาติดิบ (splice-faction-design.md: Beast) — โทนสีเขียว/น้ำตาล/ดินเผา

---

## วิธีใช้

1. **แต่ละ prompt = 1 ภาพ** — copy "STYLE BLOCK" + prompt ของชิ้นนั้นส่งพร้อมกัน (STYLE BLOCK คุมให้ทุกภาพสไตล์เดียวกัน)
2. แนบภาพ ref (Spike/Defense Derby) ไปด้วยครั้งแรก แล้วบอกให้จำสไตล์ไว้ใช้ทุกภาพในแชทนั้น
3. ถ้า layout ตาราง (4 แถว × 5 level) เพี้ยน/ตัวทับกัน → สั่งแยกเป็น "ทีละ level, 4 มุมมอง" แทน (มี prompt สำรองท้ายไฟล์)
4. ภาพจะเอาไป paint ต่อ → เน้นเส้นชัด สีแบน แสงเงาน้อย

---

> ⚠️ **กฎ proportion (rig-friendly):** ทุกตัวละคร **แขน/ขายาวขึ้นกว่า chibi ทั่วไปหน่อย + แยกออกจากลำตัวชัด** (มีช่องว่าง) เพื่อ rig/skin ง่าย ไม่ยืด-แตกตอน animate. กฎนี้อยู่ใน STYLE BLOCK แล้ว → **override คำว่า "stubby/tiny" ในข้อความย่อยทุกจุด**
>
> ⚠️ **กฎ no-aura (ตัวละครเท่านั้น):** **ห้ามมีออร่า/particle/FX ลอยรอบตัว หรือของหมุนวนรอบตัว (orbiting)** — เพราะปั้น 3D ไม่ได้ ต้องตัดทิ้ง. สื่อพลัง/ความเป็นนักเวทผ่าน **on-body** แทน (ตาเรืองแสง, รูนบนผิว/ขน, แกน/คริสตัลเรืองบนตัว, หมวก/ผ้าคลุม). กฎนี้อยู่ใน STYLE BLOCK แล้ว → **override คำว่า "floating particles / glowing aura around it / orbiting ..." ในข้อความย่อยทุกจุดของบล็อกตัวละคร**. *(props/towers/อาวุธ ไม่ใช่ตัวละคร — "level 5 glowing/magical" ยังได้ปกติ)*

## 🎨 STYLE BLOCK (แปะหน้า prompt ทุกภาพ)

```
Cute chibi mobile game concept art style, like Brawl Stars and Supercell art:
big heads, rounded bodies, large expressive glossy eyes, soft rounded shapes
with no sharp scary details, thick clean outlines, flat cel-shaded colors with
minimal soft shading, playful and friendly mood (kid-friendly, nothing scary
or gory). IMPORTANT proportions: arms and legs are SLIGHTLY LONGER and clearly
separated from the torso with a visible gap (rig-friendly, NOT tiny stubs) so
the character deforms cleanly when animated; keep the cute chibi look but give
limbs enough length to bend at elbows and knees. NATURAL BEAST faction palette: leafy green, earthy
brown, terracotta red-orange accents, bone-white details, natural textures
(scales, fur, leaves, wood, stone) simplified into cute rounded forms.
IMPORTANT (characters only): NO glowing aura, energy swirls, spell/buff FX, or
floating particles AROUND the character, and NO objects orbiting/hovering around
it — keep the character a single clean solid silhouette that can be sculpted as
one 3D mesh. Show magic via ON-BODY features only (glowing eyes, runes on the
skin/fur, a glowing core/crystal ON the body, hats/robes). (This does NOT apply
to separate props/towers/weapons, which may still glow.)
Pure white background, isolated objects, evenly spaced with clear gaps,
nothing overlapping, no text, no labels, no watermark, no logo.
```

---

# 1) TOWERS — ป้อม 5 แบบ × 5 level

> ทุกแบบ: **1 ภาพ = ตาราง 4 แถว × 5 คอลัมน์** — แถว 1 ด้านหน้า / แถว 2 ด้านหลัง / แถว 3 ด้านข้างซ้าย / แถว 4 ด้านข้างขวา, คอลัมน์เรียง level 1→5 (ซ้าย→ขวา, ใหญ่/อลังขึ้นตาม level). ถ้าป้อมมีสิ่งมีชีวิต/แขน → กาง T-pose, **ไม่มีอาวุธ**

### Tower 1 — Thorn Snare (กับดักหนามหิน) `T1 line`
```
[STYLE BLOCK]
Turnaround concept sheet of ONE cute fantasy defense tower design called
"Thorn Snare": a small round stone mound wrapped in soft cartoon vines with
chunky rounded thorns, tiny cute leaves on top. Grid layout, 4 rows x 5 columns,
all cells evenly spaced, no overlapping. Each COLUMN is the same tower at
upgrade level 1 to 5 from left to right: level 1 tiny simple mound, growing
bigger and fancier each level, level 5 large mound with glowing flower crown
and layered vine rings. Each ROW shows a different view of the same level:
row 1 front view, row 2 back view, row 3 left side view, row 4 right side view.
Same design and colors in every view. White background, no text, no weapons.
```

### Tower 2 — Hunter Hut (กระท่อมนักล่า)
```
[STYLE BLOCK]
Turnaround concept sheet of ONE cute wooden hunter hut defense tower:
a chubby round treehouse-like hut on short thick wooden legs, leaf roof,
round window like a friendly eye, small rope and bone-white horn decorations.
Grid layout, 4 rows x 5 columns, evenly spaced, no overlapping.
Each COLUMN is the same hut at upgrade level 1 to 5 left to right:
level 1 tiny single hut, each level adds floors, bigger leaf roof, totem
decorations; level 5 grand three-story hut with big antler crown.
Each ROW is a view of the same level: row 1 front, row 2 back,
row 3 left side, row 4 right side. Same design in every view.
White background, no text, no characters, no weapons.
```

### Tower 3 — Ancient Totem (โทเทมบรรพกาล)
```
[STYLE BLOCK]
Turnaround concept sheet of ONE cute carved wooden totem pole defense tower:
stacked chubby round animal faces (bear, boar, owl) with big friendly eyes,
small leaf and feather decorations, moss patches. Grid layout, 4 rows x 5
columns, evenly spaced, no overlapping. Each COLUMN is the totem at upgrade
level 1 to 5 left to right: level 1 single small face block, gaining one more
stacked face and taller pole each level, level 5 tall five-face totem with
glowing carved patterns and feather crown. Each ROW is a view of the same
level: row 1 front, row 2 back, row 3 left side, row 4 right side.
Same design in every view. White background, no text, no weapons.
```

### Tower 4 — Bloom Turret (ป้อมดอกไม้ยิงเมล็ด)
```
[STYLE BLOCK]
Turnaround concept sheet of ONE cute giant flower defense tower:
a chubby cartoon flower with a big round bud head that looks like a friendly
face, thick short stem, big rounded leaves as a skirt, sitting in a small
dirt mound pot of stones. Grid layout, 4 rows x 5 columns, evenly spaced,
no overlapping. Each COLUMN is the flower at upgrade level 1 to 5 left to
right: level 1 small sprout with closed bud, blooming bigger each level,
level 5 fully bloomed giant flower with double petal layers and tiny glowing
pollen orbs floating around. Each ROW is a view of the same level:
row 1 front, row 2 back, row 3 left side, row 4 right side.
Same design in every view. White background, no text, no weapons.
```

### Tower 5 — Titan Bone Tower (ป้อมกระดูกไททัน) `T5 line`
```
[STYLE BLOCK]
Turnaround concept sheet of ONE cute prehistoric bone defense tower:
rounded bone-white dinosaur ribs and a chubby friendly dino skull (big round
eye sockets, smiling, NOT scary) stacked into a small tower, moss and tiny
flowers growing on the bones. Grid layout, 4 rows x 5 columns, evenly spaced,
no overlapping. Each COLUMN is the tower at upgrade level 1 to 5 left to
right: level 1 a few small ribs, growing into a larger bone structure each
level, level 5 grand tower with big smiling titan skull crown and glowing
green moss runes. Each ROW is a view of the same level: row 1 front,
row 2 back, row 3 left side, row 4 right side. Same design in every view.
White background, no text, no weapons.
```

---

# 2) MONSTERS — มอนสเตอร์ 14 ตัว (ถูก → แพง) × 5 level (พัฒนาการ)

> ทุกตัว: **1 ภาพ = ตาราง 4 แถว × 5 คอลัมน์** — แถว = หน้า/หลัง/ซ้าย/ขวา, คอลัมน์ = **level 1→5 (พัฒนาการ: โตขึ้น + เกราะ/หนาม/ออร่าเพิ่ม แต่ยังเป็นตัวเดียวกัน silhouette เดิม)**. **ยืน T-pose ทุกช่อง**, **ไม่มีอาวุธ** (จะทำใส่ทีหลัง), หน้าตาน่ารักเป็นมิตร

### Monster 1 — Sprout Raptor (แร็ปเตอร์จิ๋ว) `T1 ถูกสุด`
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi baby raptor dinosaur monster:
big round head, huge glossy friendly eyes, leaf sprout growing on top of its
head, chubby short body, stubby little arms and legs, small rounded tail,
leafy green scales with cream belly. It WEARS wooden leaf-blade claw-gloves on
both hands as part of its design (a worn accessory, not a separate held
weapon), growing bigger with more blades and a green glow at higher levels.
Grid layout, 4 rows x 5 columns, evenly
spaced, no overlapping. Each COLUMN is the SAME raptor at evolution level
1 to 5 left to right, growing slightly bigger each level:
level 1 tiny hatchling with a single sprout;
level 2 taller, sprout becomes a small twin-leaf, tiny leaf bracers on wrists;
level 3 leaf shoulder pads and a flower on the head sprout, small back leaf spikes;
level 4 wooden bark chest armor, bigger back leaf spikes, striped tail rings;
level 5 full cute bark-and-leaf armor set, glowing flower crown, small
glowing green aura leaves floating around it.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, legs straight and slightly apart, facing forward, happy expression.
Each ROW is a view of the same level: row 1 front view, row 2 back view,
row 3 left side view, row 4 right side view. Same character in all views.
Full body visible, white background, no weapons, no text.
```

### Monster 2 — Bramble Boar (หมูป่าหนาม)
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi wild boar monster standing
upright on two legs: chubby round body, big friendly snout, tiny rounded
tusks, mohawk of soft cartoon bramble thorns and leaves down its back,
earthy brown fur with green thorn accents. Grid layout, 4 rows x 5 columns,
evenly spaced, no overlapping. Each COLUMN is the SAME boar at evolution
level 1 to 5 left to right, growing slightly bigger each level:
level 1 small piglet with tiny thorn mohawk;
level 2 thicker mohawk, rope belt with a wooden charm;
level 3 tusks grow a bit, bramble shoulder guards, thorn wrist bands;
level 4 rounded wooden tusk caps, bramble chest harness, bigger back thorns;
level 5 full bramble armor with cute stone helmet (ears sticking out),
glowing thorn tips and tiny floating leaf particles.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, legs straight, facing forward, cheerful expression.
Each ROW is a view of the same level: row 1 front, row 2 back,
row 3 left side, row 4 right side. Same character in all views.
Full body visible, white background, no weapons, no text.
```

### Monster 3 — Moss Golemling (โกเลมมอสส์)
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi rock golem monster:
round boulder body covered in soft green moss patches, stubby stone arms and
legs, big single friendly glowing eye, cracks glowing soft warm green. Its stone
fists form built-in stone gauntlets as part of the body (a worn feature, not a
separate held weapon), growing bigger with glowing runes at higher levels.
Grid layout, 4 rows x 5 columns, evenly spaced, no overlapping.
Each COLUMN is the SAME golem at evolution level 1 to 5 left to right,
growing slightly bigger each level:
level 1 small pebble body with one moss patch;
level 2 more moss, tiny mushroom on the head;
level 3 small flowers bloom on shoulders, extra floating pebble orbiting it;
level 4 stone shoulder plates, glowing rune carvings, two orbiting pebbles;
level 5 mini mountain body with a tiny tree growing on its back, bright
glowing runes and three orbiting glowing stones.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, legs straight, facing forward. Each ROW is a view of the same level:
row 1 front, row 2 back, row 3 left side, row 4 right side.
Same character in all views. Full body visible, white background,
no weapons, no text.
```

### Monster 4 — Griffin Chick (ลูกกริฟฟิน)
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi baby griffin monster:
big fluffy round head with large glossy eyes, small rounded beak, chubby
lion-cub body standing upright on two legs, puffy wings, soft cream and
golden-brown feathers with leafy green ribbon accents. It WEARS golden feather
talon-blades on its claws as part of its design (a worn accessory, not a
separate held weapon), becoming sharper and glowing at higher levels.
Grid layout,
4 rows x 5 columns, evenly spaced, no overlapping. Each COLUMN is the SAME
griffin at evolution level 1 to 5 left to right, growing slightly bigger
each level:
level 1 fluffy chick with tiny stub wings;
level 2 wings grow real feathers, small head feather tuft;
level 3 leaf ribbon collar, longer tail with feather tip, ear feathers;
level 4 golden feather chest guard and wing tip blades of soft gold feathers,
small feather crest crown;
level 5 majestic full feather crest, large elegant wings, golden claw caps,
gentle glowing wind swirls around the wings.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, wings slightly open behind the arms, legs straight, facing forward.
Each ROW is a view of the same level: row 1 front, row 2 back,
row 3 left side, row 4 right side. Same character in all views.
Full body visible, white background, no weapons, no text.
```

### Monster 5 — Little Titan (ไททันจิ๋ว) `T5 แพงสุด`
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi ancient forest titan monster:
looks powerful but adorable — big round dino-like head with tiny rounded
horns, huge gentle glowing eyes, chubby massive body, thick stubby arms and
legs, mossy back, deep green and stone-grey palette with warm glowing green
cracks. Grid layout, 4 rows x 5 columns, evenly spaced, no overlapping.
Each COLUMN is the SAME titan at evolution level 1 to 5 left to right,
growing slightly bigger each level:
level 1 small titan cub with bare mossy back;
level 2 tiny rocks and grass grow on its shoulders, horn stubs get rings;
level 3 a small tree sprouts on its back, stone knuckle guards;
level 4 shoulder boulders like pauldrons, glowing rune belt of vines,
bigger curved horns;
level 5 a walking hill — small forest with trees and glowing crystals on
its back and shoulders, majestic antler-like horns, bright glowing green
cracks and floating leaf-and-light particles.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, legs straight, facing forward, calm friendly expression.
Each ROW is a view of the same level: row 1 front, row 2 back,
row 3 left side, row 4 right side. Same character in all views.
Full body visible, white background, no weapons, no text.
```

### Monster 6 — Acorn Scout (กระรอกสอดแนม)
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi squirrel scout monster:
big fluffy round tail, big glossy friendly eyes, chubby small body, wearing a
little acorn-cap helmet, a leaf cape and a woven seed satchel. Grid layout,
4 rows x 5 columns, evenly spaced, no overlapping. Each COLUMN is the SAME
squirrel at evolution level 1 to 5 left to right, growing slightly bigger and
gaining more gear each level:
level 1 tiny squirrel with a small acorn cap and a leaf scarf;
level 2 bigger acorn helmet and a seed satchel bag;
level 3 a leaf cape, bark shin guards, acorns clipped on a belt;
level 4 layered leaf cloak, acorn shoulder pads, twig goggles on the head;
level 5 full scout regalia: big leaf-feather cape, acorn-crown helmet with
tiny leaf wings, a satchel of glowing seeds, and striped tail rings.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, legs straight, facing forward, cheerful expression. Each ROW is a view
of the same level: row 1 front, row 2 back, row 3 left side, row 4 right side.
Same character in all views. Full body visible, white background, no weapons,
no text.
```

### Monster 7 — Spore Shaman (เห็ดหมอผี)
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi walking mushroom shaman monster:
a round mushroom-cap head, big friendly eyes, chubby little body, stubby legs
and tiny arms. Grid layout, 4 rows x 5 columns, evenly spaced, no overlapping.
Each COLUMN is the SAME mushroom at evolution level 1 to 5 left to right,
growing bigger and gaining shaman gear each level:
level 1 small plain-cap mushroom sprite;
level 2 a wide-brim spotted mushroom hat and a small herb pouch belt;
level 3 a glowing spore necklace and a mossy poncho;
level 4 a layered fungus robe, several herb pouches, glowing spots on the cap;
level 5 grand shaman regalia: huge patterned mushroom-cap hat, a robe of
hanging moss and flowers, a glowing spore aura, and bead-and-bone necklaces.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, legs straight, facing forward. Each ROW is a view of the same level:
row 1 front, row 2 back, row 3 left side, row 4 right side. Same character in
all views. Full body visible, white background, no weapons, no text.
```

### Monster 8 — Blossom Stag (กวางดอกไม้)
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi stag deer monster standing
upright on two legs: soft fur, big gentle glossy eyes, small rounded antlers,
slender friendly build. Grid layout, 4 rows x 5 columns, evenly spaced, no
overlapping. Each COLUMN is the SAME stag at evolution level 1 to 5 left to
right, growing bigger and more adorned each level:
level 1 a little fawn with tiny flower-bud antlers;
level 2 antlers grow with a blooming flower crown and leaf ear tufts;
level 3 a woven moss cloak, a bead collar, and anklets;
level 4 a layered flowering antler crown, a vine sash, blossoms on the
shoulders;
level 5 grand forest-spirit regalia: a huge blooming antler crown, a flowing
moss-and-flower cloak, glowing pollen, and ceremonial bead ornaments.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, legs straight, facing forward, gentle expression. Each ROW is a view
of the same level: row 1 front, row 2 back, row 3 left side, row 4 right side.
Same character in all views. Full body visible, white background, no weapons,
no text.
```

### Monster 9 — Bastion Tortoise (เต่าป้อม)
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi tortoise monster on stubby legs:
big friendly eyes, a chubby body, and a big mossy shell shaped like a little
fortress on its back. Grid layout, 4 rows x 5 columns, evenly spaced, no
overlapping. Each COLUMN is the SAME tortoise at evolution level 1 to 5 left
to right, its shell-fortress growing grander each level:
level 1 a small tortoise with a plain mossy shell;
level 2 the shell grows tiny stone walls and a vine sash;
level 3 a stone helmet with a leaf plume, little tower bumps and flowers on
the shell;
level 4 a fortress shell with mini stone battlements, a moss cloak, and a gem
brow crown;
level 5 grand living-fortress: a big shell castle with towers, gardens and a
tiny waterfall, an ornate stone helmet crown, and a glowing rune sash.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, legs straight, facing forward. Each ROW is a view of the same level:
row 1 front, row 2 back, row 3 left side, row 4 right side. Same character in
all views. Full body visible, white background, no weapons, no text.
```

### Monster 10 — Lumen Moth (มอดเรืองแสง) `แพงสุด`
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi luna-moth spirit monster standing
upright: a fuzzy round body, big gentle eyes, feathery antennae, and big
glowing patterned wings. Grid layout, 4 rows x 5 columns, evenly spaced, no
overlapping. Each COLUMN is the SAME moth at evolution level 1 to 5 left to
right, growing more radiant and adorned each level:
level 1 a small fuzzy moth with tiny wings and plain antennae;
level 2 wings gain glowing eye-spot patterns and a pollen scarf;
level 3 a feathery antenna crown, a pollen-dust cloak, and glowing leg cuffs;
level 4 layered luminous wing patterns, a pollen-dust mantle, and a gem collar;
level 5 grand forest-spirit regalia: large glowing patterned wings, a flowing
pollen-light cloak, an antenna crown of glowing buds, and floating dust-of-
light particles around it.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, wings slightly open behind, legs straight, facing forward. Each ROW is
a view of the same level: row 1 front, row 2 back, row 3 left side, row 4
right side. Same character in all views. Full body visible, white background,
no weapons, no text.
```

### Monster 11 — Ronin Tabby (แมวส้มนักดาบ) `T5 นักดาบ`
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi orange tabby cat swordsman
(a wandering ronin cat) standing upright on two legs: big round head, huge
glossy friendly eyes, small triangular ears, tiny pink nose, fluffy striped
orange fur with a cream belly, a long expressive striped tail, slim
rig-friendly arms and legs. Nature-beast ronin theme made of leaf, bamboo,
bark and rope (NOT metal). Grid layout, 4 rows x 5 columns, evenly spaced,
no overlapping. Each COLUMN is the SAME cat at evolution level 1 to 5 left to
right, growing slightly bigger and more armored each level:
level 1 a small kitten with a simple leaf headband and a cloth sash belt;
level 2 a short bark-brown haori vest, a rope belt, small wrist wraps;
level 3 a single leaf-plate shoulder guard, a leaf-crest bandana, bark bracers;
level 4 bamboo-and-bark lamellar chest armor, striped tail rings, a small
horned leaf helm;
level 5 full cute ronin regalia: layered leaf-and-bark lamellar armor, a
horned kabuto-style helm with a carved wooden crest, a flowing moss cape,
a soft glowing green aura and floating leaf petals around it.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, EMPTY HANDS, legs straight and slightly apart, facing forward, calm
confident expression. Each ROW is a view of the same level: row 1 front,
row 2 back, row 3 left side, row 4 right side. Same character in all views.
Full body visible, white background, no weapons, no text.
```

> 🔮 **Monster 12-14 = สาย "นักเวท/Supporter"** (ตรงกับระบบ Supporter ในเกม: Heal / Shield / Buff). ทั้งสามยืน T-pose มือเปล่า — คทา/ไม้กายสิทธิ์เป็น **prop แยก** (หมวด 6A) แต่พลังเวทหลักสื่อผ่าน **ออร่า/สัญลักษณ์เรืองแสงบนตัว** ให้อ่านออกว่าเป็นนักเวทแม้ไม่มีของในมือ

### Monster 12 — Mendcap Mystic (เห็ดหมอเวท) `Lv3 · Supporter (Heal)`
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi mushroom healer-mage monster:
a round glowing mushroom-cap head like a soft lantern dome, big gentle glossy
eyes, chubby little body, slim rig-friendly arms and legs. A gentle HEALER —
its cap softly glows warm green-gold from within, and it wears a little leaf
medic-sash with a glowing heart-shaped spore charm on the chest (worn, part of
the design). Grid layout, 4 rows x 5 columns, evenly spaced, no overlapping.
Each COLUMN is the SAME mushroom at evolution level 1 to 5 left to right,
growing bigger and more radiant each level:
level 1 a small plain-cap sprite with a faint glow and a leaf sash;
level 2 the cap glows brighter with soft spots, a herb pouch on the sash;
level 3 a glowing heart-spore pendant, a mossy medic poncho, glowing spots on the cap;
level 4 a layered luminous cap, several herb pouches, glowing green runes on the cap;
level 5 grand healer regalia: a big radiant patterned cap that glows like a
lantern from within, a flowing moss-and-flower robe, and a bright glowing
heart-spore pendant on the chest.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, EMPTY HANDS, legs straight, facing forward, gentle caring expression.
Each ROW is a view of the same level: row 1 front, row 2 back, row 3 left
side, row 4 right side. Same character in all views. Full body visible,
white background, no weapons, no text.
```

### Monster 13 — Wardroot Sprite (ภูติต้นไม้เกราะ) `Lv5 · Supporter (Shield + Heal)`
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi tree-spirit (dryad) mage monster:
a small round wooden-bodied sprite with a friendly bark face, big glossy gentle
eyes, soft leafy-moss hair, a glowing green heartwood core visible on its chest,
slim rig-friendly wooden arms and legs. A GUARDIAN mage — its body is
plated with smooth rounded bark shield-armor, and glowing protective runes trace
its body (worn, part of the design). Grid layout, 4 rows x 5 columns, evenly
spaced, no overlapping. Each COLUMN is the SAME sprite at evolution level 1 to 5
left to right, growing bigger and more armored each level:
level 1 a little wooden sprite with a faint heartwood glow and a small bark
chest-plate;
level 2 leafy-moss hair grows, bark shoulder-plates, tiny glowing runes on the bark;
level 3 a mossy mantle, layered bark plate-armor, glowing green runes on the plates;
level 4 bark pauldrons with blooming flowers, a brighter heartwood core, full
rune-carved bark plate armor;
level 5 grand forest-guardian regalia: a crown of leaves and blossoms, a big
glowing heartwood core on the chest, a flowing moss cloak, and full rune-lit
bark plate armor covering its body.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, EMPTY HANDS, legs straight, facing forward, calm protective expression.
Each ROW is a view of the same level: row 1 front, row 2 back, row 3 left
side, row 4 right side. Same character in all views. Full body visible,
white background, no weapons, no text.
```

### Monster 14 — Aura Fox (จิ้งจอกเวทเสริมพลัง) `Lv7 · Supporter (Buff)`
```
[STYLE BLOCK]
Evolution turnaround sheet of ONE cute chibi mystical fox-spirit (kitsune) mage
monster standing upright on two legs: a fluffy round-headed fox with big glossy
gentle eyes, small pointed ears, a tiny nose, soft cream-and-amber fur with
leafy-green accents, slim rig-friendly arms and legs. An EMPOWERER mage — it
has multiple glowing spirit-tails and glowing nature-rune markings on its fur
(worn, part of the design). Grid layout, 4 rows x 5 columns, evenly spaced, no
overlapping. Each COLUMN is the SAME fox at evolution level 1 to 5 left to right,
gaining more tails, brighter glow and more adornment each level:
level 1 a small fox kit with a single softly-glowing tail and faint rune marks;
level 2 two glowing tails, a leaf-ribbon collar, brighter rune marks on the fur;
level 3 three glowing tails, a flower-and-leaf ear crown, glowing rune sashes;
level 4 five radiant glowing tails, glowing rune sashes, brighter glowing fur markings;
level 5 grand spirit regalia: many luminous flowing tails fanned out, a crown
of glowing leaves and crystals, and bright glowing rune markings all over its fur.
Standing in a strict T-POSE in EVERY cell: arms spread straight out to the
sides, tails fanned behind, EMPTY HANDS, legs straight, facing forward, wise
friendly expression. Each ROW is a view of the same level: row 1 front,
row 2 back, row 3 left side, row 4 right side. Same character in all views.
Full body visible, white background, no weapons, no text.
```

---

# 3) FENCES — รั้ว 3 แบบ (ถูก → แพง)

> **1 ภาพ = ตาราง 4 แถว × 3 คอลัมน์** — คอลัมน์ = แบบที่ 1→3 (ถูก→แพง), แถว = หน้า/หลัง/ซ้าย/ขวา. รั้วเป็น "ท่อนตรง 1 segment" (จะเอาไปต่อเรียงเป็นแนวยาวในเกม)

```
[STYLE BLOCK]
Concept sheet of THREE cute fantasy fence segment designs for a nature beast
faction, each fence is one straight modular segment piece. Grid layout,
4 rows x 3 columns, evenly spaced, no overlapping.
Each COLUMN is a different fence tier, cheap to expensive left to right:
column 1 "Twig Fence" — simple small wooden sticks tied with rope, a few
leaves; column 2 "Thorn Hedge" — chubby rounded bramble hedge with soft
cartoon thorns and tiny flowers; column 3 "Stone Fang Wall" — low wall of
rounded mossy stones with cute bone-white fang shapes and glowing green
moss runes. Each ROW shows the same segment from a different view:
row 1 front view, row 2 back view, row 3 left side view, row 4 right side
view. Same design in every view. White background, no text, no characters.
```

---

# 4) MINER + MINER BASE (แบบเดียว × 3 level)

### Miner — คนขุดทอง (ตัวละคร)
> **1 ภาพ = ตาราง 4 แถว × 3 คอลัมน์** — คอลัมน์ = level 1→3, แถว = หน้า/หลัง/ซ้าย/ขวา, T-pose, ไม่มีอุปกรณ์/อาวุธในมือ

```
[STYLE BLOCK]
Character turnaround sheet of ONE cute chibi mole miner creature for a nature
beast faction: chubby round mole with soft brown fur, big friendly goggles-like
eyes, pink round nose, big flat digging paws (rounded, not sharp), tiny leaf
backpack. Grid layout, 4 rows x 3 columns, evenly spaced, no overlapping.
Each COLUMN is the same mole at upgrade level 1 to 3 left to right:
level 1 plain little mole, level 2 adds a leaf hard-hat and rope belt,
level 3 adds a sturdy acorn helmet with tiny lamp and bigger backpack.
Standing in a strict T-POSE in every cell: arms spread straight out to the
sides, legs straight, facing forward. Each ROW is a view of the same level:
row 1 front, row 2 back, row 3 left side, row 4 right side. Same character
in all views. Empty hands, no tools, no weapons, white background, no text.
```

### Miner Base — จุดส่งทอง (สิ่งปลูกสร้าง)
```
[STYLE BLOCK]
Turnaround concept sheet of ONE cute gold deposit burrow building for a
nature beast faction: a chubby round earth mound with a friendly cave
entrance like a smiling mouth, wooden support beams, small gold nuggets and
coins spilling around the entrance, leaves and mushrooms on top.
Grid layout, 4 rows x 3 columns, evenly spaced, no overlapping.
Each COLUMN is the same burrow at upgrade level 1 to 3 left to right:
level 1 small simple dirt mound, level 2 bigger with wooden doorframe and
gold cart, level 3 large burrow with stone entrance, golden horn decorations
and glowing lanterns. Each ROW is a view of the same level: row 1 front,
row 2 back, row 3 left side, row 4 right side. Same design in every view.
White background, no text, no characters.
```

---

# 5) BASE — ฐานหลัก/Fort Core (อันเดียว × 5 level)

> **1 ภาพ = ตาราง 4 แถว × 5 คอลัมน์** — คอลัมน์ = level 1→5, แถว = หน้า/หลัง/ซ้าย/ขวา. เป็น "หัวใจเมือง" ต้องดูสำคัญสุดในแมป

```
[STYLE BLOCK]
Turnaround concept sheet of ONE cute great tree fortress — the main base
heart of a nature beast faction city: a chubby giant tree with a big round
friendly face-like hollow, thick short trunk, fluffy round leaf crown,
small wooden platforms and rope bridges around it, glowing green heart
crystal nested in the roots. Grid layout, 4 rows x 5 columns, evenly spaced,
no overlapping. Each COLUMN is the same tree fortress at upgrade level
1 to 5 left to right: level 1 small young tree with tiny crystal,
growing bigger and grander each level — more platforms, stone ring base,
hanging lanterns, antler-like golden branches; level 5 majestic world-tree
with triple leaf crown, glowing runes and floating leaf particles.
Each ROW is a view of the same level: row 1 front, row 2 back,
row 3 left side, row 4 right side. Same design in every view.
White background, no text, no characters, no weapons.
```

---

# 6) WEAPONS & SHIELDS — อาวุธ/โล่ (prop แยก, แยก level ตาม mon/tower)

> ตัวละคร T-pose มือเปล่า → พวกนี้เป็น **prop แยก** เอาไป**แปะที่มือ/แขนตอน rig**
> 🧤 **อาวุธแบบสวม (ถุงมือ/กงเล็บ/gauntlet ที่ติดตัว) = ออกแบบมาในตัวละครเลย** (อยู่ในข้อ 2 แล้ว ไม่ต้อง gen แยก) — หมวดนี้เฉพาะ **อาวุธถือ/แยก** (กระบอง/ค้อน/คทา/ไม้เท้า/หอก). ตัว worn ในตาราง 6A จะ mark ว่า "อยู่ในตัวละครแล้ว"
> ทุกชิ้น: **isolated prop, ไม่มีตัวละคร, ไม่มีมือถือ, white bg** — layout **3 แถว (หน้า / ข้าง / หลัง) × 5 คอลัมน์ (level 1→5)**, level สูงใหญ่/อลัง/เรืองแสงขึ้น
> ธีม match กับมอน/ป้อมของมัน (Natural = ไม้/กระดูก/หิน/ใบไม้/เถา/คริสตัล)

## 🗡️ WEAPON STYLE addendum (แปะต่อจาก STYLE BLOCK)
```
Isolated cute game prop, NO character, NO hands holding it, chunky chibi
Brawl-Stars-style proportions, thick clean outlines, flat cel-shaded colors.
Natural beast faction materials only (wood, bone, stone, leaf, vine, crystal)
in leafy green / earthy brown / bone-white / terracotta. Grid layout,
3 rows x 5 columns, evenly spaced, no overlapping, no text. Each COLUMN is
the SAME item at level 1 to 5 left to right, bigger and more ornate each level
(level 5 glowing/magical). Each ROW is a view: row 1 front, row 2 side,
row 3 back. Pure white background.
```

## Template (ใช้กับทุกช่องในตารางด้านล่าง)
```
[STYLE BLOCK]
[WEAPON STYLE addendum]
The prop is [ITEM] designed to match the monster/tower "[NAME]".
Level 1: [L1]. Level 5: [L5]. (levels in between grow gradually.)
```

---

## 6A) อาวุธคู่มอน (Monster Weapons) — 1 ภาพ/มอน (×5 level)

| # | มอน | [ITEM] | Level 1 → Level 5 |
|---|---|---|---|
| 1 | Sprout Raptor | 🧤 **worn — อยู่ในตัวละครแล้ว** (wooden leaf-blade claw-glove) | *ไม่ต้อง gen แยก — เติมในตัวละคร (ทำแล้ว)* |
| 2 | Bramble Boar | thorny wooden club | กิ่งหนามสั้น → กระบองหนามยักษ์ ดอกกุหลาบ+อำพันเรืองแสง |
| 3 | Moss Golemling | 🧤 **worn — อยู่ในตัวละครแล้ว** (built-in stone gauntlets/หมัดหิน) | *ไม่ต้อง gen แยก — เติมในตัวละคร (ทำแล้ว)* |
| 4 | Griffin Chick | 🧤 **worn — อยู่ในตัวละครแล้ว** (golden feather talon-blades) | *ไม่ต้อง gen แยก — เติมในตัวละคร (ทำแล้ว)* |
| 5 | Little Titan | uprooted log-and-stone maul | ท่อนไม้เล็ก → ค้อนไม้+หินยักษ์ มอส+คริสตัลเรืองแสง |
| 6 | Acorn Scout | acorn-tipped twig spear | ไม้จิ้มลูกโอ๊ก → หอกไม้เพรียว หัวลูกโอ๊กทอง+ใบไม้ |
| 7 | Spore Shaman | glowing mushroom staff | ไม้เท้าเห็ดเล็ก → ไม้เท้าเห็ดใหญ่ สปอร์เรืองแสง+เถาพัน |
| 8 | Blossom Stag | blooming branch wand / vine whip | กิ่งดอกไม้เล็ก → คทากิ่งไม้ดอกบานสะพรั่ง เรืองแสงละอองเกสร |
| 9 | Bastion Tortoise | chunky stone hammer | ค้อนหินเล็ก → ค้อนหินก้อนใหญ่ รูน+มอส+น้ำเรืองแสง |
| 10 | Lumen Moth | glowing crystal wand | คทาคริสตัลเล็ก → คทาคริสตัลใหญ่ ปีกแสง+ละอองเรืองแสงลอย |
| 11 | Ronin Tabby | leaf-forged katana (ดาบใบไม้) | ดาบไม้ไผ่ฝึกเล็ก → คาตานะใบไม้-เปลือกไม้ คมเรืองแสงเขียว+กลีบใบปลิว |
| 12 | Mendcap Mystic | glowing heal staff (คทาเห็ดเยียวยา) | ไม้เท้าเห็ดเล็กเรืองอ่อน → คทาเห็ดโคมใหญ่ สปอร์เยียวยาสีเขียว-ทองลอย |
| 13 | Wardroot Sprite | rune-branch guardian staff (คทากิ่งไม้รูน) | กิ่งไม้รูนเล็ก → คทากิ่งไม้แกนหัวใจไม้ แผ่นเปลือกไม้-รูนป้องกันลอยรอบ |
| 14 | Aura Fox | spirit-orb focus wand (คทาลูกแก้ววิญญาณ) | คทาลูกแก้วเล็กเรืองอ่อน → คทาลูกแก้ววิญญาณใหญ่ ออร่าเสริมพลัง+ลางแสงลอย |

## 6B) โล่คู่มอน (Monster Shields) — 1 ภาพ/มอน (×5 level)

| # | มอน | [ITEM] | Level 1 → Level 5 |
|---|---|---|---|
| 1 | Sprout Raptor | round leaf shield (lilypad-like) | ใบไม้กลมแผ่นเดียว → โล่ใบไม้ซ้อนชั้น ดอกไม้กลาง+เรืองแสง |
| 2 | Bramble Boar | bramble bark round shield | เปลือกไม้กลมเล็ก → โล่เปลือกไม้หนา หนามรอบขอบ+อำพัน |
| 3 | Moss Golemling | stone slab shield | แผ่นหินเล็ก → โล่หินหนา รูนเรืองแสง+คริสตัลกลาง |
| 4 | Griffin Chick | golden feather kite shield | โล่ขนนกเล็ก → โล่ทองทรงว่าว ขนเรืองแสง+อัญมณี |
| 5 | Little Titan | huge boulder shield | ก้อนหินแบน → โล่หินยักษ์ มอส+ต้นไม้จิ๋ว+คริสตัลเรืองแสง |
| 6 | Acorn Scout | acorn-cap buckler | ฝาลูกโอ๊กเล็ก → โล่ฝาลูกโอ๊กทอง ใบไม้ขอบ+เมล็ดเรืองแสง |
| 7 | Spore Shaman | giant mushroom-cap shield | หมวกเห็ดเล็ก → โล่หมวกเห็ดใหญ่ ลายจุด+สปอร์เรืองแสง |
| 8 | Blossom Stag | woven flower-vine shield | โล่เถาสานเล็ก → โล่เถาสานดอกบาน ละอองเกสรเรืองแสง |
| 9 | Bastion Tortoise | stone tower-shield | โล่หินสูงเล็ก → โล่กำแพงป้อม หอคอยจิ๋ว+รูนเรืองแสง |
| 10 | Lumen Moth | luminous wing shield | โล่ปีกแสงเล็ก → โล่ปีกผีเสื้อใหญ่ ลายเรืองแสง+ละอองแสง |
| 11 | Ronin Tabby | *(นักดาบล้วน — ไม่มีโล่ / ถ้าอยากได้: small bark tsuba-guard buckler)* | — (ถ้าทำ: การ์ดดาบเปลือกไม้เล็ก → การ์ดใบไม้-ไม้ไผ่เรืองแสง) |
| 12 | Mendcap Mystic | *(นักเวท — ไม่มีโล่ถือ; พลังป้องกันคือสเปล)* | — |
| 13 | Wardroot Sprite | 🛡️ **worn — อยู่ในตัวละครแล้ว** (แผ่นเปลือกไม้-รูนลอยรอบตัว) | *ไม่ต้อง gen แยก — เป็นส่วนหนึ่งของตัวละคร (สื่อว่าเป็นสายโล่)* |
| 14 | Aura Fox | *(นักเวท — ไม่มีโล่ถือ; พลังคือออร่าบัฟ)* | — |

## 6C) TURRET ป้อม (Tower Turrets) — เครื่องยิงติดป้อม + ฐาน + กระสุน

> ป้อมเป็นโครงสร้าง — "อาวุธ" = **turret (ปืน/เครื่องยิงธนู/เครื่องยิงหิน) ที่วางบนยอดป้อม**
> **แต่ละ turret มี "ฐานเล็ก" (mounting base/pivot) ในตัว** → วางลงบนป้อมแล้วดูลงตัวสวยงาม
> **1 ป้อม = 2 ภาพ:** (A) ตัว turret ทั้ง 5 level × 4 มุม / (B) กระสุนของมัน แยกออกมา (เฉพาะหน้า/หลัง)
> ธีม Natural = ไม้/ไม้ไผ่/กระดูก/หิน/ใบไม้/เถา/เชือก/คริสตัล (ไม่ใช้โลหะแวววาว)

### 🔫 TURRET STYLE addendum (สำหรับ**ภาพ A** — ตัว turret)
```
Isolated cute game TURRET prop (a mounted weapon that sits on TOP of a defense
tower), NO character, NO hands. The turret is ONE SINGLE PIECE: the weapon is
built directly onto a small round ROTATING TURNTABLE base (a low circular
swivel disc / drum) so the WHOLE thing can spin in place on the tower — do NOT
draw a separate detachable mount, the round turntable is part of the turret and
turns together with it. The turntable is round and symmetrical so it looks
correct from every angle while rotating. Chunky chibi Brawl-Stars-style
proportions, thick clean outlines, flat cel-shaded colors. Natural beast
faction materials only (wood, bamboo, bone, stone, leaf, vine, rope, crystal)
in leafy green / earthy brown / bone-white / terracotta. Grid layout, 4 rows x
5 columns, evenly spaced, no overlapping, no text. Each COLUMN is the SAME
turret at level 1 to 5 left to right, bigger and more ornate each level (level
5 glowing / magical); the round turntable base also grows fancier each level.
Each ROW is a view: row 1 front, row 2 back, row 3 left side, row 4 right side.
Same design and colors in every view. Pure white background.
```

### 🎯 AMMO STYLE addendum (สำหรับ**ภาพ B** — กระสุน)
```
Isolated cute game PROJECTILE / ammo prop that matches its turret, NO
character, NO hands. Chunky chibi style, thick clean outlines, flat cel-shaded
colors, same natural materials as the turret. Grid layout, 2 rows x 5 columns,
evenly spaced, no overlapping, no text. Each COLUMN is the SAME projectile at
level 1 to 5 left to right, bigger and more magical each level. Row 1 front
view, row 2 back view. Same design in both views. Pure white background.
```

### Template — ภาพ A (turret)
```
[STYLE BLOCK]
[TURRET STYLE addendum]
The turret is [ITEM] mounted on its small base, designed to sit on the tower
"[NAME]". Level 1: [L1]. Level 5: [L5]. (levels in between grow gradually.)
```

### Template — ภาพ B (กระสุน)
```
[STYLE BLOCK]
[AMMO STYLE addendum]
The projectile is [AMMO] fired by the "[NAME]" turret.
Level 1: [L1 ammo]. Level 5: [L5 ammo].
```

---

**ตารางสรุป turret + กระสุน ต่อป้อม** (เอา [ITEM]/[AMMO] ไปแทนใน template)

| # | ป้อม | ประเภท | [ITEM] turret (L1 → L5) | [AMMO] กระสุน (L1 → L5) |
|---|---|---|---|---|
| 1 | Thorn Snare | เครื่องยิงหนาม (ballista เถาวัลย์) | thorn-vine harpoon launcher บนฐานหินมอสกลม: เถาหนามขดเล็กบนแท่นหิน → เครื่องยิงฉมวกเถาหนามใหญ่ ดอกไม้บาน+เรืองแสงเขียว | thorn harpoon dart: หนามไม้เล็ก → ฉมวกหนามใหญ่ ครีบใบไม้+ปลายเรืองแสงเขียว |
| 2 | Hunter Hut | เครื่องยิงธนู (ballista/หน้าไม้) | wooden bone crossbow ballista บนแท่นไม้กลม+เชือก: หน้าไม้ไม้เล็ก → บัลลิสตาไม้-กระดูกคันคู่ใหญ่ เชือกตึง+เขากวางประดับ | bone-tipped bolt/arrow: ลูกธนูไม้เล็ก → ลูกดอกกระดูกใหญ่ ครีบขนนก+เรืองแสง |
| 3 | Ancient Totem | ตัวปล่อยลำแสงวิญญาณ | spirit-eye beam emitter บนวงแหวนไม้แกะ: ตาไม้แกะดวงเล็ก → ตาวิญญาณเรืองแสงใหญ่ รัศมีรูน+ขนนกรอบ | spirit orb / rune wisp: ดวงแสงเขียวเล็ก → ลูกวิญญาณเรืองแสงใหญ่ วงแหวนรูนหมุน |
| 4 | Bloom Turret | ปืนยิงเมล็ด (ดอกไม้) | flower-bud seed cannon บนกระถางดิน-ใบไม้: ดอกตูมปากกระบอกเล็ก → ปืนดอกไม้บานสองชั้นใหญ่ ละอองเกสรเรืองแสง | seed pod shot: เมล็ดกลมเล็ก → ฝักเมล็ดหนามใหญ่ หางละอองเกสรเรืองแสง |
| 5 | Titan Bone Tower | ปืนใหญ่กระดูก (กะโหลกไดโน) | dino-skull bone cannon บนแท่นซี่โครง: กะโหลกอ้าปากดวงเล็ก → ปืนกะโหลกไดโนยักษ์ (ยิ้ม ไม่หลอน) มอส+รูนเรืองแสง | bone shard / fossil ball: เศษกระดูกเล็ก → ลูกฟอสซิลหินใหญ่ มอส+รูนเรืองแสง |

> 🔄 **หมุนได้ทั้งก้อน:** ฐานติดมากับตัวปืนเป็นชิ้นเดียว (ไม่ต้อง gen แยก) — ตัวป้อมคือ base อยู่แล้ว
> turret แค่ **วางบนยอดป้อมแล้วหมุนทั้งชิ้น** (yaw รอบแกนตั้ง) รายละเอียดทรงฐานอยู่ใน addendum ด้านบน

## 6D) อุปกรณ์ขุดของ Miner (Mining Tool) — 1 ภาพ (×3 level)

> Miner มี **3 level** → prop แยก 3 คอลัมน์ (จับคู่กับตัว miner ตัวตุ่นในข้อ 4). isolated prop เหมือนกัน

```
[STYLE BLOCK]
[WEAPON STYLE addendum — แต่เปลี่ยน "3 rows x 5 columns" เป็น "3 rows x 3 columns", และ level 1 to 3]
The prop is a cute mining tool set (a chunky pickaxe/shovel) for the mole
miner, natural wood-and-stone materials, no character, no hands.
Grid layout, 3 rows x 3 columns, evenly spaced, no overlapping, no text.
Each COLUMN is the tool at level 1 to 3 left to right:
level 1 a small simple wooden pickaxe with a stone tip;
level 2 a sturdier pickaxe with a leaf-wrapped handle and a small lantern;
level 3 a big ornate pickaxe with a glowing crystal tip, golden bands and
tiny gears. Each ROW is a view: row 1 front, row 2 side, row 3 back.
Pure white background.
```

> ถ้าอยากได้ทั้ง **จอบ + พลั่ว** แยกด้วย: gen อีกภาพ เปลี่ยน "pickaxe" เป็น "a rounded wooden shovel" (level 1 พลั่วไม้ธรรมดา → level 3 พลั่วโลหะขอบทอง+คริสตัลเรืองแสง)

---

# 🔧 Prompt สำรอง (ถ้าตารางใหญ่แล้วภาพเพี้ยน)

ตาราง 4×5 = 20 ช่อง บางทีโมเดลวาดหลุด/ตัวไม่ตรงกัน — ให้แตกเป็นทีละ level:

```
[STYLE BLOCK]
Character turnaround sheet of [ชื่อ+คำอธิบายตัวเดิม] at upgrade level [N]:
[จุดเด่นของ level นั้น]. 4 views in ONE row, evenly spaced, no overlapping,
left to right: front view, back view, left side view, right side view.
Exactly the same design, pose, size and colors in all 4 views.
[T-POSE ถ้าเป็นตัวละคร]. White background, no weapons, no text.
```

แล้วบอก ChatGPT เพิ่มว่า: *"same character as the previous image, only change the upgrade level details"* เพื่อคุมให้เป็นตัวเดียวกันทุก level

---

# 📝 Checklist หลังได้ภาพ

- [ ] ทุกมุมมอง (หน้า/หลัง/ซ้าย/ขวา) เป็นตัวเดียวกัน สี/สัดส่วนตรงกัน
- [ ] ตัวละคร T-pose จริง (แขนกางตรง ขาแยกเล็กน้อย) — ถ้าไม่ตรง สั่ง "strict T-pose, arms perfectly horizontal"
- [ ] ไม่มีอาวุธ/ของในมือ (จะใส่ทีหลัง)
- [ ] พื้นหลังขาวล้วน ไม่มีเงาพื้นหนาๆ (ถ้ามีเงา สั่ง "no ground shadow")
- [ ] level 1→5 ไล่ความอลังชัด แต่ยังเป็น "ตัวเดียวกัน" (silhouette เดิม)
- [ ] โทนน่ารัก ไม่หลอน — ตาโต ยิ้ม ทรงมน (สำคัญ: ธีมฟักไข่)

> เผ่าถัดไป: `conceptArt-Human.md`, `conceptArt-Galax.md`, `conceptArt-Darkside.md` (โครง prompt เดียวกัน เปลี่ยน palette+ธีม)
