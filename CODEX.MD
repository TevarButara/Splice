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

## Rigging Notes

Date noted: 2026-08-10

- Keep `SpringBoneChain` as-is for single-chain secondary motion such as tails, hair locks, small tassels, and narrow cape strips. It remains driven by `spring_` bones from the Blender autorig.
- Clothing panels should use a separate authoring/runtime path: Blender creates multiple vertical `cloth_` strips via `Add Cloth Panel`, then Unity uses `ClothBoneChain` on the character prefab.
- `ClothBoneChain` is intentionally stiffer and more constrained than tail spring motion: use multiple short strips, modest gravity, higher damping, max bend angle, and sphere/capsule colliders around hips, torso, and legs to prevent cloth from passing through the body.
- For mobile performance, start with 3 strips x 3 segments for tabards/coat tails and only increase after the silhouette needs it. Avoid full cloth simulation until hero pipeline and device budgets are proven.
- Blender autorig v0.6.2 adds `Bind Body Safe`: temporarily excludes `spring_` and `cloth_` deform bones from Blender Auto Weights so core body bones receive reliable weights first. Use this when normal `Bind (Auto Weights)` creates only `cloth_*`/dynamic groups or the body does not move in Pose Mode.
- Blender autorig v0.6.3 improves `Bind Body Safe`: if Blender Bone Heat/Auto Weights fails or creates no usable non-dynamic body weights, it now falls back to guaranteed nearest-body-bone weights. This makes the mesh move in Pose Mode for diagnosis/first pass, but production heroes still need Weight Paint cleanup around shoulders, hips, fingers, cloth edges, and accessories.
- Recommended bind order for heroes with clothing: build/adjust skeleton -> add spring/cloth guide bones if needed -> run `Bind Body Safe` and confirm Hips/Spine/Arm/Leg move the mesh in Pose Mode -> then paint/assign cloth vertices to `cloth_` strips where secondary motion is needed.
- If moving an arm pulls torso skin, or moving the head pulls nearby hair/collar/shoulder parts, treat it as vertex-weight bleed rather than bone force. Clean the affected vertex groups: torso vertices should mostly belong to `Spine/Chest`, arm vertices to `Shoulder/Arm/ForeArm/Hand`, head rigid parts to `Head`, and cloth/accessories to their intended body or `cloth_`/`spring_` bones. The guaranteed nearest-bone fallback is useful only to make the mesh move; it is not production-quality skinning.
- Blender autorig v0.6.4 default animation clips use numbered combat names: `Attack1`, `Attack2`, `Attack3`, and `Skill1`-`Skill4`. The old plain `Attack` and `Spell` defaults are removed so hero clips line up with the intended game animation set.
- Blender autorig v0.6.5 adds `Adopt Selected Character` for Mixamo-first workflow. Import Mixamo FBX normally, select its armature or skinned mesh, then Adopt to rename that existing rig to `SpliceRig`, strip namespaces such as `mixamorig:` from bones/vertex groups, and preserve the original Mixamo skin weights/animation data. Use this when Mixamo rigging is the main body rig, then continue adding `spring_`/`cloth_` bones and Splice animation/export setup on top.
- When editing `splice_autorig.py`, preserve UTF-8 carefully because Blender UI strings include Thai. Avoid PowerShell `Get-Content | Set-Content` rewrites; use Python `read_text/write_text(encoding="utf-8")` or a UTF-8-aware editor to prevent mojibake in the addon panel.
- Blender autorig v0.6.6 fixes dynamic bones after Mixamo Adopt: `Adopt Selected Character` disables stale 3D-cursor placement by default, and `Add Spring Chain`/`Add Cloth Panel` force the adopted armature visible, resolve parent bones by stripped Mixamo names, and place new `spring_`/`cloth_` bones into a visible `Splice Dynamic` bone collection.
- Blender autorig v0.6.7 improves tail visibility after Adopt: dynamic armatures are forced to `STICK` display with bone names visible, and `Add Spring Chain` auto-uses `BACKDOWN` direction for `TAIL` when the UI is still on `DOWN`, because tail bones created straight down from `Hips` often hide inside the body/legs.
- Blender autorig v0.6.8 fixes tiny stacked `spring_`/`cloth_` bones on Mixamo FBX imports. Dynamic chain auto-length now uses the adopted armature's local bone height instead of `arm.dimensions`, because FBX object scale can make `arm.dimensions` tiny while the actual edit-bone coordinate space is normal-sized.
- For separate clothing meshes, keep one character armature only. Do not re-bind the Mixamo body mesh. Add clothing mesh as a separate skinned mesh using the same `SpliceRig` armature modifier, transfer or assign torso weights from the body for fitted areas, then paint hanging cloth areas to `cloth_` bones. This keeps Mixamo body skinning intact while letting clothes flutter through `ClothBoneChain` in Unity.
- Clothing meshes can only Weight Paint to vertex groups that exist on that mesh. Adding `cloth_` bones to `SpliceRig` does not automatically create matching groups on already imported/cloned clothing meshes; create vertex groups on the clothing mesh with exactly the same names as the target bones such as `cloth_skirt_back_01_1`, then paint or assign vertices to them.
- Blender autorig v0.6.9 adds mesh group helpers: `Create Cloth Groups on Mesh` creates missing `cloth_` vertex groups on selected mesh(es), and `Create Tail Groups on Mesh` creates missing `spring_tail` vertex groups. These buttons only create empty groups and ensure the selected mesh uses the `SpliceRig` Armature modifier; they do not paint weights automatically.
- Blender autorig v0.6.10 adds `Arm Spread Offset` in the Animation Clips retarget panel. It applies extra outward rotation to `LeftShoulder`/`RightShoulder` and `LeftArm`/`RightArm` during retarget bake to compensate when adopted character rest poses keep arms too close to the body. Start around 10-20 degrees; use negative values if the arms move inward.
- Blender autorig v0.6.11 extends `Arm Spread Offset` to `LeftForeArm`/`RightForeArm` at 65% strength, so elbow/forearm pose opens along with the shoulder and upper arm instead of leaving the lower arm too tucked in.
- Blender autorig v0.6.12 adds `Elbow Distance Offset` as a separate retarget slider. It adds extra outward rotation only to `LeftArm`/`RightArm`, making elbows sit farther from the torso without increasing shoulder/forearm spread as much.
- For Mixamo-first characters, the most reliable animation pipeline is same-skeleton action transfer, not corrective retarget offsets. Upload/rig the final character in Mixamo once, adopt that exact armature as `SpliceRig`, then use animations downloaded from Mixamo for the same skeleton and copy/bake action curves by bone name onto the adopted rig. Use delta retarget and offset sliders only when source and target skeleton/rest pose differ.
- Blender autorig v0.6.13 adds `Import Same-Rig Clip` for Mixamo-first characters. It imports the selected FBX action, maps fcurves directly by stripped bone name onto the adopted `SpliceRig`, pushes the result as an NLA clip, and avoids delta-retarget/rest-pose offset issues. Prefer this over `Retarget Selected Clip` when the animation FBX comes from the same Mixamo skeleton as the adopted character.
- Blender autorig v0.6.14 changes `Import Same-Rig Clip` to bake source pose to target pose frame-by-frame by matching stripped bone names, instead of creating/copying action fcurves directly. This avoids Blender 5.1 `Action` API errors where `Action.fcurves` is no longer available in the same way.
- Blender autorig v0.6.15 hardens `Export Unity FBX`: it selects meshes using the `SpliceRig` Armature modifier even if they are not direct children, rebuilds/ensures NLA tracks for every completed clip action before export, temporarily unmutes only export tracks, and reports exported clip/mesh counts. If Unity shows no animation clips, first confirm the Blender export report says `clips > 0`, then enable `Import Animation` on the FBX importer and check the Animation tab.
- `SpringBoneChain` was simplified into a VRM/Unity-chan style spring solver to reduce tuning confusion. Inspector controls are now only `Stiffness`, `Drag`, `Gravity Direction`, `Gravity Power`, optional `Center`, `Hit Radius`, and colliders. Use `(0,-1,0)` for tails that fall toward the ground or `(0,1,0)` for lifted/magical tails. If motion still looks segmented, fix Blender vertex weights between adjacent `spring_tail_*` groups before adding more runtime parameters.
- `SpringBoneChain` adds one movement-specific control: `Motion Influence`. Set `Center` to Hips or the character root, then use higher Motion Influence for tail lag/whip from character movement and lower values when the tail should follow the body more tightly. If the tail vibrates too fast, lower `Stiffness`, raise `Drag`, and keep `Gravity Power` modest.
- `SpringBoneChain` adds `Simulation Speed` as the first control to adjust when the motion is too fast to read. Lower values slow spring/gravity response and smooth direction changes while keeping the Inspector simpler than separate wind/wave controls.
- `ClothBoneChain` now uses the same readability controls as spring tails: `Simulation Speed`, optional `Center`, `Motion Influence`, and direction smoothing. For cloth panels, keep Motion Influence lower than tails so fabric follows the body more tightly while still lagging on movement.
