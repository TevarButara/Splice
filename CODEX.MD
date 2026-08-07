# CODEX Coach Notes

วันที่เริ่มบทบาท: 2026-07-14

## บทบาทของ Codex ในโปรเจกต์นี้

- ทำหน้าที่เป็น executive coach / production advisor สำหรับเกม Splice
- ไม่แก้โค้ดเกมหรือ asset gameplay โดยตรง เว้นแต่ผู้ใช้สั่งชัดเจน
- ช่วยคิดลำดับงานที่ทำให้เกมสมบูรณ์ที่สุดโดยใช้เวลาน้อยที่สุด
- ช่วยตัด scope, ลดความเสี่ยง, และหาวิธีแก้ปัญหาเมื่อเจอ blocker
- หากมี note สำคัญระหว่างทาง ให้เพิ่มลงไฟล์นี้ต่อเนื่อง

## North Star ตอนนี้

พิสูจน์ให้เร็วที่สุดว่า core loop สนุกจริง:

1. ผู้เล่นเลือก faction / เข้าเมือง / จัดฐานได้
2. ผู้เล่นเลือกเป้า raid และเริ่ม raid ได้
3. raid เล่นจบได้ตั้งแต่ต้นจนจบ
4. ได้ reward กลับเข้า meta wallet / base loop
5. ผู้เล่นอยากเล่นซ้ำอีก 20-30 รอบ แม้ยังเป็น greybox

ถ้าข้อ 1-5 ยังไม่ลื่น ห้ามรีบสเกล art, content, PvP, live ops, หรือ monetization หนัก

## Priority ปัจจุบัน

- Focus: Phase 1 playable greybox + Player Base / Async Raid loop
- สถานะตาม roadmap: 5.1-5.4 โค้ดหลักเสร็จ, 5.5 เหลือ idle economy / base level-up / loot scaling / matchmaking-by-strength
- งานที่น่าจะคุ้มที่สุดตอนนี้คือ Editor assembly + playtest checklist + ตัดสินใจ balance แบบหยาบ

## กติกาการตัดสินใจ

- ถ้างานไม่ได้ช่วยให้เล่นจบรอบได้เร็วขึ้น ให้เลื่อนไป backlog
- ถ้างานเป็น art/content scale แต่ยังไม่พิสูจน์ loop ให้ทำแค่ 1 ตัวอย่างเพื่อ validate pipeline
- ถ้าระบบหนึ่งต้องใช้เวลานาน ให้ทำ version ที่วัด fun ได้ก่อน แล้วค่อย harden
- Server-authoritative และ data-driven เป็น architectural guardrail ห้ามทำทางลัดที่ต้องรื้อทีหลัง
- Defender เป็น async base-building, Invader เป็น live play core; อย่ากลับไปทำ live PvP 1:1 ก่อนเวลา

## Risk ที่ต้องจับตา

- Scope creep จาก faction/tier/content เยอะเกินก่อน core loop สนุก
- Editor setup/prefab/scene debt ทำให้โค้ดที่เขียนแล้วไม่เคยถูกทดสอบ end-to-end
- Economy defense snowball: เงินต้องไม่ซื้อฐานกันแตก 100%; ใช้ DefenseCapacity/base level เป็นเพดาน
- Async raid ถ้า reward ไม่รู้สึกคุ้ม หรือแพ้แล้วไม่อยากลองอีก จะทำให้ meta loop แห้ง
- UX 30 วินาทีแรกสำคัญมาก เพราะ genre ผสมหลายชั้น

## Next Coaching Agenda

- สร้าง short milestone แบบ "เล่นได้ใน 1 session" สำหรับ Editor assembly
- นิยาม playtest script 20-30 รอบ และ metric ที่ต้องจด
- แยก backlog เป็น Must / Should / Later เพื่อกันงานบาน
- ตัดสินใจว่า 5.5 economy minimum viable version ต้องมีอะไรบ้างก่อนสเกล content

## Map / Mirror Design Decision

วันที่จด: 2026-08-05

- สำหรับ map แบบ mirror คล้าย MOBA ให้ใช้ world coordinate เดียวเป็น source of truth เสมอ
- ห้ามกลับ logic position แยกตามทีม เพราะจะทำให้ AI, targeting, replay, snapshot, network และ debug ซับซ้อนเกินจำเป็น
- Server/logic ควรรู้แค่ตำแหน่งจริงในโลก เช่น Attacker spawn อยู่ด้านล่าง, Defender/Fort อยู่ด้านบน หรือกลับกันตาม scene ที่ author ไว้
- สิ่งที่ mirror ได้ควรเป็น presentation layer: camera angle, UI lane order, minimap, icon direction, input mapping
- ถ้าต้องให้ผู้เล่นแต่ละฝั่งรู้สึกว่า "ทีมตัวเองอยู่ล่างเสมอ" ให้ทำผ่าน camera transform / view adapter ไม่ใช่แก้ตำแหน่ง object จริง
- สำหรับ Splice ตอนนี้ Invader เป็น role หลักและ Defender เป็น async snapshot จึงควรล็อก raid view แบบเดียวก่อน เพื่อประหยัดเวลาและลดบั๊ก

## Natural Art Direction Note

วันที่จด: 2026-08-06

- Natural faction style target is now locked to clean 3D-stylized fantasy / mobile RPG / gacha-style concept art.
- Prefer smooth premium materials, soft sculpted forms, clean silhouettes, ivory/green/wood/gold palette, gentle gradients and controlled highlights.
- Avoid overly noisy MOBA ruin style, dark dirty stone, dense rubble, heavy moss, excessive vines, and hard comic/anime ink outlines unless explicitly testing a separate style.
- For main-base concepts, keep the building grand and layered, but preserve the same clean stylized material language as the Natural hero references.
- Apply this direction broadly to Natural heroes, monsters, towers, resource buildings, props, and base assets so the faction reads as one coherent premium mobile-RPG art family.
- Natural Main Base 01 selected from the first approved clean 3D-stylized base concept. The central top socket must stay empty in the base mesh; crystal/core is a separate future asset and should not be fused into front/back/side reference sheets.
- Main base 01 was visually strong but too complex/noisy for AI-to-3D generation. Future main-base concepts should be modular and easy to split into lower foundation, middle body, and top crown/socket.
- For Natural main base 3D reference, prefer clear large forms, broad stone tiers, simple leaf panels, sparse roots, and readable part boundaries over intertwined organic detail.
- Modular Natural main base must still preserve soft clean 3D-stylized fantasy: creamy ivory stone, broad soft gold, low noise, and leaf/enamel panels with clear green-to-yellow gradients. Do not let modular clarity become dry, hard, or flat.
- Natural main base should not be forced into cylinder + literal leaf-panel language. It may use soft lotus, shell, petal, shrine, or pavilion silhouettes as long as it stays clean 3D-stylized fantasy and remains modular for 3D generation.
- Selected new Natural main base direction: lotus/shell exploration design 1. Modular breakdown should use four major pieces: lower foundation/base, left large lotus shell petal, right large lotus shell petal, and central shrine/core.
- BaseNatural component 3D reference set is complete: 4 components x 4 views saved under `ConceptArt/Natural/New Stylize/Base/BaseNatural/`.
- Natural asset lighting should feel soft, creamy, and diffuse, with gentle ambient shadows and smooth gradients. Avoid harsh contrast, hard black cracks, and overly sharp cel-shaded material breaks.
- Direction sheets must force silhouette-level variation first; changing only leaf panels, trim, or ornament is not enough for useful selection.
- Natural towers must stay strictly in the clean 3D-stylized fantasy / mobile RPG style of Main Base 01. Do not drift into painterly MOBA, comic/anime lineart, hard cel-shading, or realistic PBR.
- Natural tower palette progression: TW1 = green/yellow/gold; TW2 = green/orange/yellow/gold; TW3 = green/orange/red/gold. Ivory stone and warm wood remain shared Natural materials.
- Natural tower colored insets may use crystal-like gradients: translucent faceted gemstone panels, soft inner glow, and emerald-to-yellow/amber/coral transitions by tower level.
- Keep tower top sockets empty and readable for future weapons or VFX; crystal-gradient color should live on panels, leaves, shields, and trim rather than occupying the turret socket.
- Crystal-gradient accents should not be pastel. Use richer gem ranges: TW1 emerald/golden-lime, TW2 emerald-to-amber/honey orange, TW3 burgundy or oxblood red -> ruby red -> warm orange.
- Natural tower crystal/faceted direction was rejected as too hard and off-style. Return to soft leaf/enamel panels: smooth shaded color fields, gentle leaf-vein embossing, warm saturated colors, and no sharp gemstone facets.
- For tower-to-3D reference, do not make one combined turnaround sheet. Export separate sheets/files for front, back, left, and right views so 3D generation/modeling has clean directional inputs.
- Selected Natural tower direction from soft-enamel sheet: TW1 = design 2, TW2 = design 4, TW3 = design 9.
- Clarify tower naming: `TW1/TW2/TW3` should be treated as tower types/chains, and each selected tower still needs its own upgrade levels such as Lv1/Lv2/Lv3.
- Natural TW1 selected chain starts from design 2. Upgrade-chain preview is saved as `TW1 Design 02/nat-tw1-design02-lv1-lv3-upgrade-chain-v1.png`.
- Tower upgrade levels need clear silhouette milestones, not only bigger scale or extra ornament. Use visible progression such as added base tiers, guardian posts, larger socket rings, crown/guard leaves, perimeter pylons, and stronger root supports.
- Natural TW1 clearer progression preview is saved as `TW1 Design 02/nat-tw1-design02-lv1-lv3-upgrade-chain-v2-clear-progression.png`.
- Tower upgrade progression must not sacrifice the clean 3D-stylized fantasy softness. Prefer large rounded forms, broad gold trims, simple silhouette milestones, and low visual noise over many small pylons, cracks, roots, or busy ornaments.
- Natural TW1 soft-clean progression preview is saved as `TW1 Design 02/nat-tw1-design02-lv1-lv3-upgrade-chain-v3-soft-clean.png`.
- Natural TW1 design 02 Lv2 and Lv3 four-view references are saved under `TW1 Design 02/Lv2/` and `TW1 Design 02/Lv3/`, matching the approved v3 soft-clean progression direction.
- Natural resource/well assets should follow the same latest Nat material language: rounded ivory stone, refined warm gold, soft green leaf/enamel panels, small controlled grass, and aqua magical water as soft glow rather than hard crystal.
- Natural main well pillar latest approved direction has separated 3D reference files: front/back/left/right using `nat-main-well-pillar-*-latest-nat-theme-v1.png`.
- Natural miner base must read as an economy/drop-off building, not a tower: use entrances, worker lodge shapes, resource bins, small aqua wells/channels, low footprints, and storage cues while keeping the latest Nat soft-enamel material language.
- Natural miner base correction: avoid water wells/aqua channels because they confuse it with a resource well. Miner base should show a clear gold drop-off entrance, visible gold coin/nugget piles, sacks, crates, carts, and storage bins.
- Selected Natural miner base direction: design 2 from the gold-dropoff exploration sheet. Four-view 3D reference is saved under `Miner Base 02 Gold Dropoff/` with front/back/left/right files.
- Rowan refresh direction: use the latest Natural clean 3D-stylized fantasy language for hero art too. Keep Rowan as a young male fox beastfolk Wildblade with cream/white fur, green facial runes, gold ear cuffs, agile swordsman outfit, living-wood katana, soft green blade glow, creamy studio lighting, smooth premium materials, and clear heroic action silhouettes.
- Rowan key-pose exploration v1 is saved at `ConceptArt/Natural/New Stylize/1 Rowan/KeyPose/rowan-wildblade-clean-3d-stylized-keypose-v1.png`.
- Rowan creamy 3D-generation reference set is split into character and weapon: character front/back/left/right saved under `ConceptArt/Natural/New Stylize/Heroes/Rowan-creamy/`; living-wood katana front/back/left/right saved under `ConceptArt/Natural/New Stylize/Heroes/Rowan-creamy/Sword/`. This set prioritizes softer creamy lighting, simplified readable shapes, open empty hands, and separated weapon modeling.
- For rig/model generation references, dangling parts such as sword tassels, cords, leaves, charms, cloth strips, and ribbons should be in a neutral straight pose aligned to the prop's functional axis. For swords, tassels/charms at the hilt should point straight opposite the blade tip, not downward by gravity, unless intentionally modeled as a separate simulated piece. Avoid default references where hanging parts swing sideways.
- For model/reference generation backgrounds, choose contrast by object value: light/white/cream assets should use a slightly darker neutral BG so edges stay sharp; dark assets should use a lighter neutral BG. Do not use same-value backgrounds that blur the silhouette.
- Rowan creamy sword v2 corrected the hilt tassel into a straight neutral rigging pose and uses darker warm-grey BG for the light cream sword. Saved under `ConceptArt/Natural/New Stylize/Heroes/Rowan-creamy/Sword/` as `rowan-creamy-sword-*-v2-straight-tassel.png`.
- Rowan creamy sword v3 is the corrected production reference: hilt tassel/leaf points straight opposite the blade tip along the sword axis, with darker warm-grey BG for light cream sword contrast. Saved under `ConceptArt/Natural/New Stylize/Heroes/Rowan-creamy/Sword/` as `rowan-creamy-sword-*-v3-opposite-tip-tassel.png`.
