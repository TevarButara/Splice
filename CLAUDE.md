# CLAUDE.md — Splice

Reverse Tower Defense + Roguelite Deckbuilder + Idle Meta. Mobile (iOS/Android), Unity 6 (Editor **6000.5.2f1**), C#, portrait-only.

## เอกสารต้องอ่านก่อนแก้ระบบใหญ่
อยู่ที่ **git root** (โฟลเดอร์เดียวกับไฟล์นี้):
- [`technical-architecture.md`](technical-architecture.md) — สถาปัตยกรรมเต็ม (อ้างเป็น §x.y ในคอมเมนต์โค้ด เช่น "architecture 5.6")
- [`README.md`](README.md) — checklist ประกอบ scene/prefab ใน Unity Editor (Phase 1)
- [`splice-development-roadmap.md`](splice-development-roadmap.md) — ลำดับงานเป็น phase
- [`splice-faction-design.md`](splice-faction-design.md) — faction/tier design (4 หมวด: Human / Galax / Natural [Beast·Elf·Thorn·Swarm] / Darkside [Undead·Demon])

## ตำแหน่งโปรเจกต์
- **Git root**: `<repo>/Splice/` (เอกสาร + `.git`)
- **Unity project**: `<repo>/Splice/Splice/` ← เปิดโฟลเดอร์นี้ใน Unity Hub และเป็น working dir ของ Claude Code
- โค้ดเกมทั้งหมดอยู่ที่ `Splice/Assets/Scripts/` — ทุกอย่างใน `Library/`, `Temp/`, `Logs/` เป็น cache ของ Unity (Unity สร้างใหม่เอง อย่าแก้)

## สถานะปัจจุบัน
**Phase 1 — Greybox Core Loop Prototype.** โค้ดครบตาม roadmap ขั้น 1 แต่ scene/prefab หลายอย่างยังต้องประกอบมือใน Unity Editor (ดู checklist ใน README). ใช้ primitive (cube/capsule) แทนอาร์ตจริง — **ยังไม่ลงอาร์ตจริงในเฟสนี้**

## หลักการสถาปัตยกรรม (สำคัญเวลาเขียนโค้ด)
- **Server-authoritative ทุกอย่าง** ผ่าน Netcode for GameObjects — HP/ทอง/spawn คำนวณที่ server เท่านั้น, client ส่งได้แค่ intent (ServerRpc) และอ่านผลผ่าน `NetworkVariable`. Input controller = client intent ล้วน
- **3 โหมด** (`GameBootstrap`): PvE = local host (ไม่มี network จริง) / PvBot / PvP = client ต่อ dedicated server. Bot เรียก ServerRpc เส้นทางเดียวกับผู้เล่น
- **Data-driven** ด้วย ScriptableObject: `MonsterDefinitionSO` / `TowerDefinitionSO` / `MinerDefinitionSO` / `CardDefinitionSO` + database SO (`CardDatabaseSO`, `TowerDatabaseSO`) lookup ด้วย id
- **เศรษฐกิจไม่มี regen** — ทองมาจาก miner (NavMesh) ขุด `GoldNode` → กลับ `MinerBase` → `GoldController` ต่อทีม (`GoldController.For(team)`). `GoldNode.owner` = `Invaders`/`Defenders`/`Neutral` (บ่อกลางแย่งได้) — miner ขุดเฉพาะบ่อทีมตัวเอง+Neutral, เลือกบ่อใกล้สุดที่ยังมีที่ว่าง (`minersPerNode`) ไม่ข้ามฝั่ง. **ซื้อ miner เพิ่มได้ผ่านการ์ด** (`MinerDeploymentManager` — buildtime/stack) เกิดที่ spawn point ต่อทีมแล้วทำงานเอง
- **มอนสเตอร์เดินตาม `LanePath` waypoint (ไม่ใช้ NavMesh)** ; miner ใช้ NavMesh. อย่าใส่ `NavMeshAgent` บน monster prefab. มอนหยุดพอ Fort เข้า `attackRange` (ไม่กองที่ core) + มี separation กันซ้อน + 2 แบบ `MonsterMovement` Ground/Flying (`flightHeight`)
- **จบแมตช์ 1:1** (`RaidManager`): Fort แตก = Invader ชนะ / timer หมด หรือ invader ตกรอบ (ไม่มี miner + ทอง 0 + ไม่มี monster) = Fort ชนะ
- Team = `Invaders` / `Defenders` (`Splice.Core.Team`)

## แผนที่โค้ด (`Splice/Assets/Scripts/`, namespace `Splice.*`)
- `Core/` — `GameBootstrap` (เลือกโหมด+เริ่ม network), `Team`, `PlayerProgression` (per-team level, placeholder สำหรับ card unlock gating)
- `Characters/` — `CharacterBase` (HP + armor ลดดาเมจ %), `MonsterCharacter` (เดินเลน + **stop-and-attack** หยุด/หันหน้า/ตี/เดินต่อ + animation ผ่าน `animator.CrossFade` replicate ด้วย NetworkVariable: Idle/Walk/Attack/Injured Walk/Death/Victory/Lose), `TowerCharacter` (build time + อัพเกรด per-stat: attack/HP/armor/range/targets + multi-target + tier chain), `FortCore` (win/lose objective, extend TowerCharacter), `MinerCharacter`, `LanePath`, `TargetingUtility`, `RangeGizmo` (helper วาดวง attackRange ใน Scene view)
- `Combat/` — `GoldController`, `GoldNode`, `MinerBase`, `RaidManager`
- `Network/` — `DeploymentManager` (invader spawn + build queue ต่อเลน: `RequestQueueMonsterServerRpc` คิว+นับถอยหลัง / `RequestDeployMonsterServerRpc` เกิดทันทีสำหรับ bot), `TowerDeploymentManager` (defender สร้าง/ซ่อม/อัพเกรด/ทำลายป้อม — วางแบบ grid snap ช่องตาราง + เช็คทับ/เขต), `MinerDeploymentManager` (ซื้อ miner ผ่านการ์ด: คิว+buildtime+stack → spawn ที่ spawn point ต่อทีม → ทำงานเอง), `LaneMarker`
- `Data/` — SO definitions; **faction เป็น asset**: `FactionSO` (1/เผ่า รวม cards+towers) + `FactionRegistrySO` (จุดเข้าเดียว — resolve composite id `factionId/localId` ↔ card/tower ผ่าน `ResolveCard`/`ResolveTower`/`IdOf`) + `FactionFamily` enum (4 หมวด). เพิ่มเผ่า = สร้าง `FactionSO` + ใส่ registry (ไม่แตะโค้ด). `cardId`/`towerId` = local id (unique ในเผ่าพอ). `CardDatabaseSO`/`TowerDatabaseSO` = legacy ไม่ใช้แล้ว
- `Draft/` — `DraftManager` (server-seeded hand)
- `Lair/` — `LairManager` (idle currency + PlayerPrefs save)
- `Bot/` — `BotController`
- `Input/` — `SoldierHut` (กระท่อมต่อเลน) + `SoldierHutInputController` (tap → เปิด card UI, flow deploy หลักของ invader), `DeployInputController` (legacy tap lane marker), `TowerPlacementInputController`, `TowerInteractionController`, `CameraPanController` (client intent เท่านั้น)
- `UI/` — `LaneDeployPanel` + `MonsterCardView` (card UI สั่งเกิดมอน: เทา/stack/นับถอยหลัง), `MinerCardView` (card ซื้อ miner: เทา/stack/นับถอยหลัง), `TowerCardView` (card เลือกป้อมฝั่ง Fort: เทา/ไฮไลต์ที่เลือก), `TowerBuildBarDisplay` (แถบเวลาสร้างป้อม), `RangeIndicator` (วงระยะ world-space ตอนวางป้อม), `GoldDisplay`, `RaidResultUI`, `MatchTimerDisplay`, `GoldNodeBarDisplay`, `SideSelectionController`, `HealthBarDisplay`

## Convention
- โค้ด/คอมเมนต์เขียนไทยผสมอังกฤษได้ (ตาม style เดิมในโปรเจกต์) — match ไฟล์รอบข้าง
- ผมทดสอบ compile/Editor เองไม่ได้ (ไม่มี Unity Editor ใน session นี้) — เขียนโค้ดให้ครบแล้วให้ผู้ใช้ประกอบ/รันใน Editor ต่อ; อย่า hardcode เวอร์ชัน package
- ในเอกสาร มาร์ก 🟢 = ของใหม่ที่ยังต้องไปตั้งค่าต่อใน Unity Editor
