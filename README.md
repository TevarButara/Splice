# Splice

Reverse Tower Defense — Roguelite Deckbuilder + Idle Meta. Mobile (iOS/Android), Unity 6 LTS, C#.

รายละเอียดสถาปัตยกรรมเต็ม ๆ อยู่ที่ [technical-architecture.md](technical-architecture.md) — อ่านไฟล์นั้นก่อนแก้โครงสร้างระบบใหญ่ ๆ

> **🟢 = อัปเดตล่าสุด / ยังต้องไปตั้งค่าต่อใน Unity** — มองหา 🟢 เพื่อดูว่ามีอะไรใหม่ที่ยังไม่ได้ประกอบใน Editor (เมื่อทำใน Unity เสร็จแล้ว ลบ 🟢 ออกได้)

## ตำแหน่งโปรเจกต์ Unity จริง

โปรเจกต์ Unity (สร้างจริงผ่าน Unity Hub, Editor **6000.5.2f1**) อยู่ที่:

```
Splice/
```

เปิดผ่าน Unity Hub → Add → เลือกโฟลเดอร์ `Splice` (โฟลเดอร์ย่อยใต้ repo root — ไม่ใช่โฟลเดอร์ที่ README นี้อยู่)

### สิ่งที่ต้องทำหลังเปิดโปรเจกต์ครั้งแรก

1. ติดตั้ง **Netcode for GameObjects** และ **Unity Transport** ผ่าน Window → Package Manager → `+` → Add package by name → พิมพ์ `com.unity.netcode.gameobjects` และ `com.unity.transport` (ให้ Package Manager เลือกเวอร์ชันที่ compatible กับ Editor 6000.5.2f1 เอง — ผมไม่ hardcode เลขเวอร์ชันให้เพราะไม่มี Editor ให้เช็ค compatibility จริง)
2. **ParrelSync** เพิ่มไว้ใน `Packages/manifest.json` แล้ว (เป็น git dependency) — Unity จะ resolve ให้อัตโนมัติตอนเปิด
3. โปรเจกต์มาจากเทมเพลต Universal 3D (Mobile) ของ Unity Hub เอง มี `Assets/TutorialInfo`, `Assets/Readme.asset`, `Assets/Settings/*` ติดมาด้วย — ลบได้เลยถ้าไม่ต้องการ ไม่กระทบ scaffold ที่เพิ่มไป
4. **ล็อกจอเป็น Portrait (แนวตั้ง)**: Edit → Project Settings → Player → Resolution and Presentation → Default Orientation = **Portrait** (ปิด auto-rotation ตัวอื่น) — เกมเล่นแนวตั้งทุกโหมด (เลนวิ่งบน→ล่าง, เล่นมือเดียว, UI สำคัญอยู่ครึ่งล่างจอ) ดูเหตุผลใน `technical-architecture.md` §5.9

## โครงสร้างโฟลเดอร์ (ภายใต้ `Splice/`)

```
Assets/
  Scripts/
    Core/         GameBootstrap — เลือกโหมด PvE (local host) / PvBot / PvP (client-server)
    Data/         ScriptableObject definitions: Monster, Tower, Card, CardDatabase (cardId lookup)
    Core/         GameBootstrap, Team (Invaders/Defenders — รองรับทุกโหมด matching)
    Characters/   CharacterBase (health, server-authoritative), MonsterCharacter (เดินตาม LanePath waypoint + ยิงระหว่างผ่าน ไม่หยุด),
                  TowerCharacter (attack in range), FortCore (win/lose objective), LanePath (เส้นทางเดิน monster ต่อเลน map-authored),
                  MinerCharacter (NavMesh: ขุดทองจาก GoldNode เต็ม load → วิ่งกลับ MinerBase → ฝากเข้า GoldController), TargetingUtility
    Network/      DeploymentManager — invader: validate gold/lane, spawn MonsterCharacter ตาม lanePaths,
                  TowerDeploymentManager — defender: validate gold, วางป้อมตามตำแหน่ง, ทั้งคู่ server-authoritative RPC
    Combat/       GoldController (ยอดทองต่อทีม, ไม่มี regen), GoldNode (บ่อทองจำกัดบนแผนที่),
                  MinerBase (จุดส่งทองต่อทีม), RaidManager (win/lose 1:1: Fort แตก / Timer หมด / invader ตกรอบ)
    Draft/        DraftManager — server-seeded hand draw from CardDatabaseSO
    Lair/         LairManager — idle currency gen + local save (PlayerPrefs)
    Bot/          BotController — calls DeploymentManager's ServerRpc directly, same path as players
    Input/        DeployInputController (invader: tap เลน → deploy monster), TowerPlacementInputController (defender: tap build zone → วางป้อม),
                  TowerInteractionController (defender: tap ป้อมเดิม → เมนู ซ่อม/อัพเกรด/ทำลาย), CameraPanController (tap+slide เลื่อนกล้อง + Home) — client intent เท่านั้น
    UI/           GoldDisplay (TMP ยอดทองผู้เล่น), RaidResultUI (TMP หน้าจอแพ้-ชนะ + เหตุผล),
                  MatchTimerDisplay (TMP นับถอยหลัง match), GoldNodeBarDisplay (bar+TMP ทองเหลือในบ่อ),
                  SideSelectionController (เลือกฝั่ง Fort/Monster ตอนเริ่ม + สลับ camera),
                  HealthBarDisplay — world-space HP bar for any CharacterBase (monster/tower/fort)
  ScriptableObjects/
    Monsters/ Towers/ Cards/   ที่เก็บ asset instance ของ SO ด้านบน
  Prefabs/
  Art/Prototype/  greybox primitive placeholders (cube/capsule material overrides ฯลฯ)
  Scenes/      มี SampleScene.unity จากเทมเพลตติดมา — สร้างเพิ่มเอง (Bootstrap, PvE, Lair, MainMenu) แล้วลบ SampleScene ทิ้งได้
Packages/manifest.json
ProjectSettings/ProjectVersion.txt
```

## Next Steps (Phase 1 — Greybox Core Loop Prototype)

โค้ดครบแล้วตาม `splice-development-roadmap.md` ขั้นตอนที่ 1 แต่ยังต้องประกอบผ่าน Unity Editor เอง (ไม่มี Editor ให้ทดสอบตอน scaffold) ทำตามลำดับนี้:

### 1. Scene `Bootstrap`
- สร้าง Scene ใหม่ชื่อ `Bootstrap`
- วาง GameObject เปล่า ใส่ `NetworkManager` (Netcode for GameObjects component) + `UnityTransport` component บนตัวเดียวกัน
- วาง GameObject อีกตัว ใส่ `GameBootstrap` (`Assets/Scripts/Core/GameBootstrap.cs`) ตั้ง `mode = PvE` ใน Inspector

### 2. Scene raid greybox (เช่น `Raid_Greybox`)
- **Ground**: Plane primitive เปล่า ๆ พอมองเห็นพื้น
- **Lane markers** (3 เลน): สร้าง Empty GameObject 3 ตัว วาง Collider (BoxCollider พอ ไม่ต้อง `isTrigger`) + component `LaneMarker` (`Assets/Scripts/Network/LaneMarker.cs`) ตั้ง `laneId` เป็น 0/1/2 ตามลำดับ — นี่คือจุดที่ `DeployInputController` raycast หาเวลา tap เลือกเลน (ยังใช้เหมือนเดิม)
- **Lane paths** (เส้นทางเดิน monster ต่อเลน — ใหม่): แต่ละเลนสร้าง Empty GameObject ใส่ `LanePath` (`Assets/Scripts/Characters/LanePath.cs`) ตั้ง `laneId` ให้ตรงกับ marker + สร้าง Empty ลูกๆ เป็น waypoint เรียงจาก **จุดเกิด → ฐาน/Fort** แล้วลากใส่ array `waypoints` ตามลำดับ — monster เดินตาม waypoint พวกนี้ (ไม่ใช้ NavMesh แล้ว) **วาง Fort ให้อยู่ในระยะ `attackRange` ของ waypoint สุดท้าย** monster ถึงจะทุบ Fort ได้
- **Fort prefab**: Capsule primitive → เพิ่ม `NetworkObject` + `NetworkTransform` + `FortCore` (`Assets/Scripts/Characters/FortCore.cs`) + Collider → ลาก asset instance ของ `TowerDefinitionSO` (สร้างขั้นตอน 3) มาผูกก่อน spawn หรือเรียก `Initialize()` เอง เพราะ `FortCore` extend `TowerCharacter` ซึ่งรอ `Initialize(TowerDefinitionSO)` — วางไว้ scene ตรงๆ 1 ตัว (ไม่ผ่าน `Instantiate` runtime)
- **Tower prefab**: Cube primitive → `NetworkObject` + `NetworkTransform` + `TowerCharacter` → save เป็น prefab ใน `Assets/Prefabs/`
- **Monster prefab**: Cube/Capsule primitive → `NetworkObject` + `NetworkTransform` (จำเป็น เพราะ `MonsterCharacter` ขับ movement ฝั่ง server เท่านั้น ไม่มี component นี้ = client เห็นตัวนิ่ง) + `MonsterCharacter` → save เป็น prefab. **⚠️ ไม่ต้องใส่ `NavMeshAgent` แล้ว** — monster เดินตาม `LanePath` (waypoint) เอง ถ้ามี NavMeshAgent เก่าติดมาให้ **ลบทิ้ง** (ไม่งั้นมันจะแย่งคุม transform / snap กลับ navmesh สู้กับการเดิน waypoint)
- **Bake NavMesh** (เฉพาะ **miner** — monster ไม่ใช้แล้ว): หลังวาง ground ครบใน scene แล้ว เปิด Window → AI → Navigation (หรือแอด `NavMeshSurface` จาก `com.unity.ai.navigation` บน ground) → mark พื้นเป็น walkable → กด Bake — ถ้าไม่ bake, miner จะเดินไม่ได้ (แต่ monster เดินได้ปกติเพราะไม่พึ่ง NavMesh)
- **Health bar** (ทำซ้ำสำหรับ Fort/Tower/Monster prefab ทั้ง 3): เพิ่ม child GameObject → Canvas (Render Mode = **World Space**, scale ให้เล็กพอ เช่น 0.01) → Image ลูก (Image Type = **Filled**, Fill Method = Horizontal) เป็น fill bar → แอด `HealthBarDisplay` (`Assets/Scripts/UI/HealthBarDisplay.cs`) บน root ของ Canvas นั้น → ผูก field `fillImage` เข้ากับ Image ที่สร้าง (`character`/`billboardCamera` ปล่อยว่างได้ เพราะ script auto-find จาก `GetComponentInParent<CharacterBase>()` และ `Camera.main`) → วาง Canvas ให้ลอยเหนือหัวโมเดล
- **Scene managers**: วาง GameObject (จะรวมไว้ตัวเดียวหรือแยกก็ได้) ใส่ `RaidManager`, `DraftManager`, `DeploymentManager`, `DeployInputController`
- **Economy — GoldController**: วาง GameObject ใส่ `NetworkObject` + `GoldController` (`Assets/Scripts/Combat/GoldController.cs`) ตั้ง `team = Invaders` และ `startingGold` ตามต้องการ — ตัวนี้คือยอดทองของทีม ไม่มี regen (ทองมาจาก miner เท่านั้น) — โหมด 1:1 วาง 1 ตัวต่อทีม (ตอนนี้ทำฝั่ง Invaders ก่อน)
- **Economy — MinerBase (ฐานส่งทอง)**: วาง Empty GameObject ตรงจุดที่อยากให้เป็นฐานของทีม (เช่น ใกล้ fort) → `MinerBase` (`Assets/Scripts/Combat/MinerBase.cs`) ตั้ง `team = Invaders` — miner จะวิ่งกลับมาที่ตำแหน่งนี้เพื่อฝากทอง **ต้องวางบนพื้นที่ NavMesh เดินถึงได้**. ตั้ง `depositRadius` = ระยะที่ miner เข้ามาถึงแล้วฝากทองได้เลย (ไม่ต้องชนจุดกลาง กันแย่งจุดเดียว) — เห็นเป็น **วงเขียว** ใน Scene view; **ต้องกว้างกว่า `arrivalRadius` ของ miner** (default 2.5 พอ)
- **Economy — GoldNode (บ่อทอง)**: วาง primitive หลายอันบนแผนที่ → `NetworkObject` + `NetworkTransform` (ถ้าอยากให้ client เห็น) + **Collider** (primitive มีให้อยู่แล้ว — ใช้วัดว่า miner ถึงบ่อ) + `GoldNode` (`Assets/Scripts/Combat/GoldNode.cs`) ตั้ง `totalGold` — วางกระจายให้บางบ่อไกลจากฐาน (ยิ่งไกล เที่ยวไป-กลับยิ่งนาน income ยิ่งช้า). miner ถือว่า "ถึง" เมื่อชนผิว collider ของบ่อ (ไม่ต้องถึงจุดกลาง) จึงไม่ต้องตั้ง `arrivalRadius` ใหญ่ตามขนาดบ่อ
- **Economy — Miner prefab**: primitive → `NetworkObject` + `NetworkTransform` + **`NavMeshAgent`** + `MinerCharacter` (`Assets/Scripts/Characters/MinerCharacter.cs`) ตั้ง `team = Invaders` + ผูก `definition` เป็น `MinerDefinitionSO` → save เป็น prefab. วางลง scene ตรงๆ 1 ตัวเป็น miner ตั้งต้นของทีม (วงจร: ไปบ่อ → ขุดเต็ม `carryCapacity` (ใช้เวลา `mineDurationSeconds`) → วิ่งกลับ MinerBase → ทองเข้าทีม) — ต้อง **Bake NavMesh** เหมือน monster ไม่งั้น miner เดินไม่ได้
- **Camera**: ตั้งกล้องมองลงมาที่ scene, ลาก camera ตัวนี้ไปที่ field `raycastCamera` ของ `DeployInputController`

### 3. ScriptableObject instances
- Assets → Create → Splice → **Monster Definition** / **Tower Definition** / **Miner Definition** / **Card Definition** / **Card Database** ตามจำนวนที่ต้องการ เก็บไว้ที่ `ScriptableObjects/Monsters/`, `Towers/`, `Cards/` ตามลำดับ
- แต่ละ `MonsterDefinitionSO`/`TowerDefinitionSO`/`MinerDefinitionSO` ตั้งค่า stat แล้วลาก prefab ที่ทำในขั้นตอน 2 มาใส่ field `prefab` — `MinerDefinitionSO` มี `goldPerTick`/`mineInterval` คุมอัตราขุด; `MonsterDefinitionSO` มี `buildTimeSeconds` (เวลา "เกิด" หลังกดสั่งสร้างในกระท่อม ตั้งต่างกันตามตัวได้ — ดูข้อ 6.2) + `movement` (Ground/Flying) และ `flightHeight` (ดูข้อ 6.4)
- แต่ละ `CardDefinitionSO` ตั้ง `cardId`, `goldCost`, `requiredLevel` (เลเวลขั้นต่ำที่ปลดล็อก — >1 = เริ่มเกมยังเทาอยู่), ลาก `MonsterDefinitionSO` ที่เกี่ยวข้องมาใส่ `linkedMonster`
- `CardDatabaseSO` ลาก `CardDefinitionSO` ทั้งหมดมาใส่ list `cards`

### 4. ผูก Inspector references ข้าม component
- `DeploymentManager`: `cardDatabase` → `CardDatabaseSO`, `deployTeam` → `Invaders` (หายอดทองผ่าน `GoldController.For(team)` อัตโนมัติ), `lanePaths` → ลาก `LanePath` ทั้ง 3 (**ต้องเรียง index ให้ตรงกับ `laneId`**) — monster เกิดที่จุดเริ่มเส้นแล้วเดินตาม waypoint
- `DraftManager`: `cardDatabase` → `CardDatabaseSO` ตัวเดียวกัน
- `DeployInputController`: `deploymentManager`, `draftManager`, `raycastCamera` ตามที่สร้างไว้
- `RaidManager`: ตั้ง `invaderTeam = Invaders`, `matchDurationSeconds` (เช่น 180). ต้องมี `FortCore` instance หนึ่งตัวใน scene ก่อน spawn เพื่อให้ `FortCore.Instance` ไม่เป็น null (Fort แตก = Invader ชนะ). Fort ชนะเมื่อ timer หมด หรือ invader ตกรอบ (ไม่มี miner + ทอง 0 + ไม่มี monster)
- **UI Canvas**: สร้าง Canvas (TMP ทั้งหมด — Import TMP Essentials ครั้งแรก)
  - `GoldDisplay`: TMP Text + ตั้ง `team = Invaders` (หายอดผ่าน `GoldController.For(team)` เองไม่ต้องลาก reference)
  - `MatchTimerDisplay`: TMP Text + ลาก `raidManager` (นับถอยหลังจาก `RemainingSeconds`)
  - `RaidResultUI`: Panel + TMP Text + Button → ลาก `raidManager`/`resultPanel`/`resultLabel`/`playAgainButton`
  - `GoldNodeBarDisplay` (ใน prefab GoldNode): World-Space Canvas → Image (Filled Horizontal) + TMP Text → ลาก `node`/`fillImage`/`label`

### 5. Defender deploy flow (ฝั่ง Fort วางป้อมด้วยทอง)
- **GoldController ฝั่ง Defender**: วาง GameObject อีกตัว `NetworkObject` + `GoldController` ตั้ง `team = Defenders` + `startingGold` — ฝั่ง Fort ก็มีเศรษฐกิจของตัวเอง (มี MinerBase/miner/GoldNode ของทีม Defenders ได้เหมือนกันถ้าต้องการให้ mine)
- **Tower Definition + goldCost**: ที่ `TowerDefinitionSO` แต่ละตัวตั้ง `goldCost` (ราคาวางป้อม) + ลาก tower prefab เข้า `prefab`
- **TowerDatabaseSO**: Assets → Create → Splice → **Tower Database** → ลาก `TowerDefinitionSO` ทุกตัวเข้า list `towers`
- **TowerDeploymentManager**: วางบน scene manager → `NetworkObject` (ถ้ายังไม่มี) + `TowerDeploymentManager` (`Assets/Scripts/Network/TowerDeploymentManager.cs`) ตั้ง `towerDatabase` และ `deployTeam = Defenders`
- **Build zone + TowerPlacementInputController**: ทำพื้นที่ที่วางป้อมได้ (พื้น/แผ่น collider) ตั้งเป็น layer เฉพาะ เช่น "BuildZone" → วาง `TowerPlacementInputController` (`Assets/Scripts/Input/TowerPlacementInputController.cs`) ผูก `towerDeploymentManager`, `raycastCamera`, ตั้ง `buildLayerMask` = layer นั้น
- **UI เลือกป้อม**: ปุ่ม UI เรียก `TowerPlacementInputController.SelectTower(towerId)` เพื่อเลือกว่าจะวางป้อมไหน แล้ว tap บน build zone เพื่อวาง — **วางแบบ grid (snap ลงช่องตาราง) ดูข้อ 6.6**
- **กดป้อม → เมนู ซ่อม/อัพเกรด/ทำลาย**:
  - **Upgrade chain**: ที่ `TowerDefinitionSO` แต่ละ tier ตั้ง `upgradeCost` (ราคาตายตัว) + ลาก SO ของ gen ถัดไปใส่ `nextTier` (เว้นว่าง = level สูงสุด). แต่ละ tier = SO+prefab แยก
  - **Cost factors** (ชั่วคราวบน `TowerDeploymentManager`, รอย้ายเข้า main config ข้อ B): `repairFactor` (default 0.5), `demolishRefundFactor` (default 1) — ค่าซ่อม `ceil(goldCost × HPหาย/maxHP × repairFactor)`, คืนเงินทำลาย `floor(goldCost × HPเหลือ/maxHP × demolishRefundFactor)`
  - **Tower layer**: ตั้ง **layer ให้ป้อม/Fort prefab** แล้วใส่ layer นั้นใน `TowerInteractionController.towerLayerMask`
  - **Action menu (Panel)**: สร้าง Panel มี 3 ปุ่ม (ซ่อม/อัพเกรด/ทำลาย) บน Screen Space Canvas, **ปิดไว้ตอนเริ่ม** → ลากใส่ `TowerInteractionController.actionMenu`
  - วาง `TowerInteractionController` (`Assets/Scripts/Input/TowerInteractionController.cs`) ผูก `towerDeploymentManager`, `raycastCamera`, `towerLayerMask`, `actionMenu`
  - ปุ่มเมนู `OnClick`: ซ่อม → `RepairSelected()`, อัพเกรด → `UpgradeSelected()`, ทำลาย → `DemolishSelected()`
  - พฤติกรรม: tap ป้อม → เมนูเด้งติดตัวป้อม; tap ที่ว่าง → เมนูปิด; Fort ซ่อมได้แต่ทำลาย/อัพเกรดไม่ได้ (server reject). ทองไม่พอ/max level → console "Tower action rejected: ..."

### 6. Side selection (เลือกฝั่ง Fort/Monster ตอนเริ่ม + สลับกล้อง)
- map ทำแบบ **reverse** (Fort อยู่ปลายหนึ่ง, จุดเกิด monster อยู่ปลายตรงข้าม) → เลือกฝั่งก่อนเล่นเพื่อให้ input ไปฝั่งที่ถูก และสลับไปกล้องของฝั่งนั้น
- **กล้อง 3 ตัว** (แนะนำ): `overviewCamera` (เช่น Main — โชว์ตอนกำลังเลือก, เว้นว่างได้), `fortCamera` (มุมฝั่ง Fort), `monsterCamera` (มุมฝั่ง monster). Controller จะ**เปิดกล้องทีละตัว** (ปิด GameObject ของอีก 2 ตัว) → **AudioListener เหลือทำงานตัวเดียว จึงหายวอร์น "N audio listeners"**
- **⚠️ ห้ามวาง `SideSelectionController` ไว้บน Panel ที่มันต้องปิด/เปิด หรือบน GameObject ที่ inactive** — ไม่งั้น `Start()` ไม่รัน แล้ว panel จะไม่โผล่ (ถ้าไม่มี log อะไรขึ้นเลย = เจอเคสนี้). วางบน GameObject แยกที่ active เสมอ (เช่นตัวเดียวกับ scene managers)
- สร้าง Canvas (Screen Space - Overlay แนะนำ เพราะไม่ผูกกับกล้องตัวไหน) + Panel เลือกฝั่ง มีปุ่ม **Fort** / **Monster** + ต้องมี **EventSystem** ในซีน
- วาง GameObject (active) ใส่ `SideSelectionController` (`Assets/Scripts/UI/SideSelectionController.cs`):
  - `selectionPanel` → Panel เลือกฝั่ง, `overviewCamera`/`fortCamera`/`monsterCamera` → กล้อง 3 ตัว
  - `fortObjects` → ลาก `TowerPlacementInputController` + `TowerInteractionController` + UI ฝั่ง Fort (GameObject)
  - `monsterObjects` → ลาก `DeployInputController` + UI การ์ดฝั่ง Monster (GameObject)
  - ปุ่ม Fort → `OnClick` เรียก `SideSelectionController.ChooseFort()`, ปุ่ม Monster → `ChooseMonster()`
- เริ่มเกม: สองฝั่งถูกปิด + เปิด overviewCamera + โชว์ Panel; เลือกแล้ว → เปิดกล้องฝั่งนั้น, controller/UI ฝั่งนั้นเปิด อีกฝั่งปิด, Panel หาย
- ขอบเขต: local เท่านั้น (PvE host คุมสองฝั่งอยู่แล้ว) — PvP ค่อย assign ฝั่งจาก server

### 6.1 กล้อง Free-pan + กติกา deploy/build
- เลนยาวกว่าจอ + มุมกล้อง 3D ~50° → เห็นทั้งแมปในจอเดียวไม่ได้ → ทั้งสองฝั่งใช้ **กล้อง free-pan (tap + slide เลื่อนดูทั่วแมป)** + ปุ่ม **Home** quick กลับฐาน
- **Invader:** สั่ง deploy ได้ทุกที่ที่กล้องอยู่ (เลือกมอน → เลือกเลน → ลง) เพราะ deploy อิง "เลน" ไม่อิงตำแหน่งนิ้ว → นั่งดูป้อมปะทะไกลๆ พร้อมสั่งลงมอนได้
- **Fort:** pan ไปดูแนวรบได้ **แต่วางป้อมไม่ได้ถ้ากล้องไม่อยู่ที่ฐาน** → ต้องกด Home กลับไปวาง (วางป้อม raycast แตะพิกัดจริงบน build zone)

**วิธีประกอบใน Unity:**
- แปะ `CameraPanController` (`Assets/Scripts/Input/CameraPanController.cs`) ไว้ที่ **GameObject ของกล้องแต่ละฝั่ง** (`FortCamera`, `MonCamera`) — SideSelection เปิดกล้องทีละตัว จึงมีแค่ฝั่งที่เล่นเท่านั้นที่ pan ได้
  - `panSpeed` = world units ต่อ 1 pixel ที่ลาก (ปรับตามความสูง/มุมกล้อง)
  - `panBounds` = **กล่องขอบเขตแมป**: สร้าง empty object 1 ตัวชื่อ `PanBounds` → ใส่ `BoxCollider` (ติ๊ก Is Trigger) → ลากขยายกล่องให้คลุมพื้นที่ที่ **"ตำแหน่งกล้อง" เลื่อนไปได้** (clamp X/Z ตาม bounds) → เห็นกรอบ gizmo ใน Scene view ปรับด้วยมือจับได้เลย; เว้นว่าง = เลื่อนไม่จำกัด. **Tip:** ลากขอบกล่องจนภาพที่ขอบสุดสวยพอดี (กล้องเอียง 50° ขอบภาพจะเลยกล่องไปนิด เป็นเรื่องปกติ)
  - `homeThreshold` = ระยะที่ถือว่า "อยู่ที่ฐาน", `homeReturnSpeed` = ความเร็วเลื่อนกลับตอนกด Home
  - ตำแหน่งเริ่มต้นของกล้องในซีน = จุด "บ้าน" (Home) อัตโนมัติ (จับตอน `Start`)
- **ปุ่ม Home:** สร้าง Button บน Canvas → `OnClick` เรียก `CameraPanController.GoHome()` (ลากกล้องของฝั่งนั้นมาใส่ช่อง object)
- **Guard วางป้อม (Fort):** ที่ `TowerPlacementInputController` มีช่องใหม่ `Camera Pan` → ลาก `CameraPanController` ของ FortCamera มาใส่ → จะวางป้อมได้เฉพาะตอน `IsAtHome` (เว้นว่าง = วางได้ทุกที่)
- การลากที่**เริ่มบน UI** จะไม่ทำให้กล้อง pan (กันชนกับปุ่มการ์ด/ปุ่มป้อม) — ต้องมี EventSystem ในซีน
- ✅ ฝั่ง Invader "deploy อิงเลนจาก UI" ทำแล้วในข้อ **6.2** (กดกระท่อมของเลน → การ์ด UI) — เลิกพึ่ง tap lane marker ในโลก. `DeployInputController` (tap lane marker) ยังอยู่เป็น legacy/ทางเลือก ไม่จำเป็นแล้วในโหมดใหม่

### 6.2 Invader deploy ผ่านกระท่อม + Card UI (คิวสร้าง + นับถอยหลัง + stack)

**ภาพรวม flow:** แต่ละเลนมี **กระท่อม (SoldierHut)** 1 หลัง → กดกระท่อม → เปิด **card UI ของเลนนั้น** โชว์มอนสเตอร์ที่เรียกได้ → กดการ์ด = สั่งคิวสร้าง (หักทองทันที) → มอนสเตอร์ "เกิด" หลัง `buildTimeSeconds` ของตัวมันแล้วโผล่ที่ต้นเลน. กดซ้ำ = ต่อคิว (stack). ทุกอย่าง server-authoritative — client แค่แสดงผลจากสถานะที่ replicate มา

- **การ์ดเทา (เรียกไม่ได้):** เงินไม่ถึง `goldCost` **หรือ** level ยังไม่ถึง `requiredLevel` → การ์ดหรี่ + กดไม่ได้ (level ไม่ถึงจะโชว์ lock overlay ด้วย)
- **นับถอยหลัง (CD):** การ์ดที่กำลังถูกสร้างอยู่หัวคิวของเลน จะโชว์ overlay นับถอยหลัง (fill + วินาที) จนเกิด
- **เลข stack:** ป้าย `xN` บนการ์ด = จำนวนตัวการ์ดนั้นที่ยังค้างในคิวเลนนี้ (รวมตัวที่กำลังสร้าง)

**โค้ดที่เพิ่ม (พร้อมแล้ว เหลือประกอบใน Editor):**
- `SoldierHut` (`Assets/Scripts/Input/SoldierHut.cs`) — marker ต่อเลน (ตั้ง `laneId`)
- `SoldierHutInputController` (`Assets/Scripts/Input/SoldierHutInputController.cs`) — tap กระท่อม → เปิด panel
- `LaneDeployPanel` (`Assets/Scripts/UI/LaneDeployPanel.cs`) — panel การ์ดผูกกับเลนที่กด
- `MonsterCardView` (`Assets/Scripts/UI/MonsterCardView.cs`) — การ์ด 1 ใบ (เทา/stack/CD/กดเรียก)
- `PlayerProgression` (`Assets/Scripts/Core/PlayerProgression.cs`) — แหล่ง level ต่อทีม (placeholder, server-authoritative)
- ต่อยอดใน `DeploymentManager`: `RequestQueueMonsterServerRpc` + คิวสร้างต่อเลน (`buildQueue`) + นับถอยหลัง (ของเดิม `RequestDeployMonsterServerRpc` แบบเกิดทันทียังอยู่ให้ bot ใช้)
- ฟิลด์ใหม่ใน SO: `MonsterDefinitionSO.buildTimeSeconds` (เวลาเกิด/ตัว), `CardDefinitionSO.requiredLevel`

**วิธีประกอบใน Unity:**
1. **กระท่อมต่อเลน:** วาง primitive (เช่น cube) 1 อันต่อเลนใกล้ต้นเลน → ใส่ **Collider** + `SoldierHut` ตั้ง `laneId` ให้ตรงกับ `LanePath`/`LaneMarker` ของเลนนั้น → **ตั้ง layer เฉพาะ เช่น "Hut"**
2. **ตั้งเวลาเกิด/level ใน SO:** ที่ `MonsterDefinitionSO` แต่ละตัวตั้ง `buildTimeSeconds` (เช่น 2-6 วิ ต่างกันตามตัว); ที่ `CardDefinitionSO` ตั้ง `requiredLevel` (การ์ดที่อยากล็อกไว้ตั้ง >1)
3. **PlayerProgression (ถ้าจะใช้ level gate):** วาง GameObject ใส่ `NetworkObject` + `PlayerProgression` ตั้ง `team = Invaders`, `startingLevel` — ถ้ายังไม่วาง ระบบถือว่า level = 1 (การ์ด `requiredLevel` 1 เรียกได้ปกติ)
4. **Card UI (Screen Space Canvas, ต้องมี EventSystem):**
   - วาง GameObject **active เสมอ** (เช่นตัวเดียวกับ scene managers) ใส่ `LaneDeployPanel` → ผูก `deploymentManager`, `content` (= root ของภาพ panel ที่รวมการ์ด — **ตัวนี้แหละที่ถูกเปิด/ปิด**), `laneLabel` (option)
   - ใต้ `content` สร้างการ์ด 1 ใบต่อมอนสเตอร์: แต่ละใบใส่ `MonsterCardView` → ผูก `panel` (= LaneDeployPanel), `card` (= `CardDefinitionSO` ของมอนตัวนั้น), `button`, `canvasGroup` (สำหรับหรี่เทา), `lockOverlay`, `nameLabel`, `costLabel`, `stackLabel`, `cooldownOverlay`, `cooldownFill` (Image = **Filled**), `cooldownLabel`
   - ปุ่มปิด (option): Button → OnClick → `LaneDeployPanel.Close()`
5. **Tap controller:** วาง GameObject (active) ใส่ `SoldierHutInputController` → ผูก `raycastCamera` (กล้องฝั่ง Monster), `hutLayerMask` = layer "Hut", `deployPanel` = LaneDeployPanel
   - ⚠️ ให้ `SideSelectionController.monsterObjects` รวม `SoldierHutInputController` + card UI ฝั่ง Monster เข้าไปด้วย (แทน/เพิ่มจาก `DeployInputController` เดิม) เพื่อให้เปิดเฉพาะตอนเลือกฝั่ง Monster

### 6.3 เห็นระยะโจมตี (attack range) — Gizmo จูนเลข + วง preview ตอนวางป้อม

แก้ปัญหา "ใส่แค่ตัวเลข `attackRange` มองไม่เห็นระยะจริง กะยาก". มี 2 ส่วนแยกกัน:

**A) Gizmo วงระยะใน Scene view (จูนเลข — ไม่ต้องตั้งค่าอะไรใน Unity เลย):**
- คลิกเลือก monster / tower / Fort (prefab หรือใน scene) → เห็น **วงระยะ `attackRange`** วาดบนพื้นทันทีใน Scene view (แดง = ป้อม/Fort, ส้ม = monster)
- อ่านค่าจาก field `definition` ที่ผูกไว้ → ปรับเลขใน SO แล้ววงเปลี่ยนตาม ใช้กะระยะตอนออกแบบด่าน/วาง waypoint ได้เลย ไม่ต้องกด Play
- **ติ๊ก `Always Show Range`** บน `MonsterCharacter`/`TowerCharacter` (Fort ได้ด้วยเพราะ extend Tower) → วงโชว์ **ตลอดเวลา** ไม่ต้องเลือกก่อน (ดูหลายตัวพร้อมกันเพื่อกะ coverage ทั้งแนว). ไม่ติ๊ก = โชว์เฉพาะตอนเลือก (เหมือนเดิม)
- Gizmo แสดงใน **Scene view เสมอ**; อยากเห็นใน **Game view** ให้เปิดปุ่ม **Gizmos** มุมบนขวาของแท็บ Game (เป็น editor-only หลุดจาก build จริง — ถ้าต้องการวงระยะในเกม/บิลด์จริงบอกได้ ผมแปะ `RangeIndicator` (LineRenderer) บน prefab ให้แทน)

**B) วง preview ตอนวางป้อมในเกมจริง (โชว์ก่อนกด → วางจริงแล้วซ่อน):**
- เลือกป้อมจากปุ่ม (`SelectTower`) → วงระยะลอยตามเคอร์เซอร์/นิ้วบน build zone ให้เห็นว่าป้อมจะครอบเลนไหม → tap วาง → **วงหายทันที** (ต้องเลือกป้อมใหม่เพื่อวางต่อ)
- ทำงานเฉพาะตอนกล้องอยู่ที่ฐาน (กติกาเดียวกับการวางป้อม); บนมือถือวงจะโชว์ตอนแตะค้าง, ในเอดิเตอร์เมาส์ hover ได้เลย

**วิธีประกอบใน Unity (เฉพาะส่วน B):**
1. สร้าง GameObject ว่าง เช่น `TowerRangePreview` → เพิ่ม **`LineRenderer`** (ตั้ง Width เล็กๆ เช่น 0.05, ใส่ material/สีเส้นตามชอบ, ปิด Cast/Receive Shadows) + `RangeIndicator` (`Assets/Scripts/UI/RangeIndicator.cs`) — วางไว้ในซีน 1 ตัวพอ
2. ที่ `TowerPlacementInputController` (ตัวที่ตั้งไว้ข้อ 5) ผูก field ใหม่:
   - `towerDatabase` → `TowerDatabaseSO` ตัวเดียวกับ `TowerDeploymentManager`
   - `placementPreview` → GameObject `TowerRangePreview` ข้างบน (เว้นว่าง = ไม่โชว์ preview, ยังวางป้อมได้ปกติ)

### 6.4 ประเภทการเดินของ monster (Ground/Flying) + separation กันกองทับ

**ภาพรวม:** monster มี 2 แบบ ตั้งที่ `MonsterDefinitionSO` — **ทั้งสองแบบเดินตาม waypoint ของ `LanePath` เส้นเดียวกัน (XZ เหมือนกันเป๊ะ)** ต่างแค่ความสูง:
- **Ground** — ติดพื้น (y อิงจุด waypoint/Fort ที่เดินไป → พื้นอยู่ตรงไหนก็ติดตรงนั้น)
- **Flying** — ลอย**สูงคงที่** `flightHeight` เหนือพื้นตลอดทาง (ไม่ไต่สูงขึ้นเรื่อยๆ — ฐานความสูงคิดจากจุดบนแมป ไม่ใช่ y ตัวเอง)

**Separation (แก้ปัญหากองกันตรง core):** มอนดันตัวออกจากมอน**ชนิดเดียวกัน**ที่อยู่ใกล้ (ในรัศมี `separationRadius`) → ฝูงกระจายเป็นวงรอบ Fort แทนที่จะซ้อนกองที่จุดเดียว. ทำงานแม้ตอนหยุดยิง Fort แล้ว. Ground กับ Flying แยกชั้นกัน (ไม่ดันข้ามชนิด)

**โค้ดพร้อมแล้ว — สิ่งที่ต้องตั้งใน Unity:**
- ที่ `MonsterDefinitionSO` แต่ละตัว: เลือก `movement` = Ground/Flying; ถ้า Flying ตั้ง `flightHeight`
  - ⚠️ ตัวบินตี Fort บนพื้น: ตั้ง `attackRange ≥ flightHeight` ไม่งั้นบินอยู่เหนือ Fort แต่เข้าไม่ถึงระยะ (การเช็คระยะเป็นระยะ 3 มิติ)
- (option) จูน `separationRadius` / `separationStrength` บน **Monster prefab** (`MonsterCharacter`) — ค่า default 1.0 / 0.6 ใช้ได้เลย; อยากให้เว้นห่างขึ้นเพิ่ม radius, ปิด separation ตั้ง radius = 0
- **เงื่อนไข "ติดพื้น" ที่ต้องมี:** วาง waypoint ของ `LanePath` (และ Fort) ให้อยู่บนพื้นจริง — เพราะ Ground monster อิงความสูงจากจุดพวกนี้ (ไม่ได้ raycast หาพื้นเอง)

### 6.5 เจ้าของบ่อทอง (Gold Node ownership) — กัน miner ข้ามฝั่ง

แก้ปัญหา miner วิ่งข้ามไปขุดบ่อฝั่งศัตรู. `GoldNode` มี field ใหม่ **`owner`** 3 แบบ:
- **Invaders** — เฉพาะ miner ฝั่ง Invader ขุดได้
- **Defenders** — เฉพาะ miner ฝั่ง Fort ขุดได้
- **Neutral** — บ่อกลาง **ทั้งสองฝั่งขุดแย่งกันได้** (จุดปะทะเชิงเศรษฐกิจ — miner สองฝั่งไปเจอกันตรงนี้ได้)

miner จะ **ข้ามบ่อของฝั่งศัตรูเสมอ** (ไม่เลือกเป็นเป้า) → ไม่วิ่งข้ามฝั่งอีก. บ่อ Neutral cap การกระจายแยกต่อทีม (แต่ละฝั่งเอา miner ไปได้ถึง `minersPerNode` ของตัวเอง)

**วิธีประกอบใน Unity:**
- ที่ **GoldNode แต่ละบ่อ** ตั้ง `owner`:
  - บ่อฝั่ง Invader → `Invaders`, บ่อฝั่ง Fort → `Defenders`, บ่อกลางที่อยากให้แย่งกัน → `Neutral`
  - ⚠️ ค่า default = **Neutral** — บ่อเก่าที่ไม่ได้ตั้งจะกลายเป็นบ่อกลางทันที (ทั้งสองฝั่งขุดได้) ถ้าอยากแยกฝั่งชัดต้องไล่ตั้งให้ครบ
- วางบ่อให้สมดุลสองฝั่ง (จำนวน/ระยะจากฐานพอกัน) เพื่อ balance income

**ผลต่อ gameplay:** เศรษฐกิจแยกฝั่งชัดขึ้น (income แต่ละฝั่งอิงบ่อของตัวเอง + บ่อกลางที่แย่งได้). win condition เดิม (invader ตกรอบเมื่อไม่มี miner+ทอง0+ไม่มีมอน) ยังทำงานถูก. "สงครามเศรษฐกิจ" (ฆ่า miner ตัด income) ย้ายไปเกิดที่บ่อ Neutral กับการดันหน่วยเข้าโซนขุดของศัตรู

### 6.6 วางป้อมแบบ Grid (snap ช่องตาราง + เขียว/แดง + กันทับ)

เปลี่ยนการวางป้อมฝั่ง Fort จาก "วางอิสระ" เป็น **grid**: tap บน build zone → ป้อม **snap ลงกลางช่องตาราง** ที่ใกล้สุด → วง preview เป็น **เขียว (วางได้) / แดง (วางไม่ได้)**. server เป็นคนตัดสิน (snap + เช็คซ้ำ) — client แค่โชว์

**กติกาที่เช็ค (server):**
- ศูนย์กลางช่องต้องอยู่**เหนือ build zone collider** (นอกเขต = แดง/วางไม่ได้)
- **ห้ามทับป้อมอื่น** (เช็คจาก `TowerCharacter.Active` — 1 ป้อม/ช่อง)
- ทองพอ (กติกาเดิม)
- *(ไม่ต้องห่วงบล็อกทางตัน — มอนเดินตาม `LanePath` fix ไม่ reroute)*

**โค้ดพร้อมแล้ว — ตั้งค่าใน Unity:**
- ที่ `TowerDeploymentManager` (scene manager) section **Placement grid**:
  - `cellSize` = ขนาดช่อง (world units) — ตั้งให้พอดีกับขนาดฐานป้อม (เช่น 2)
  - `gridOrigin` = ขยับแนวช่องให้เรียงสวยกับ build zone/เลน
  - `buildLayerMask` = **layer ของ build zone** (ตัวเดียวกับที่ตั้งใน `TowerPlacementInputController`)
- ที่ `TowerPlacementInputController` (มีอยู่แล้ว) — `placementPreview` ต้องผูก `RangeIndicator` (ข้อ 6.3 B) เพื่อเห็นวงเขียว/แดง; ปรับสี `validColor`/`invalidColor` ได้
- **build zone**: ทำพื้น/แผ่น collider ครอบพื้นที่ที่อยากให้วางได้ (ข้างเลน) ตั้ง layer "BuildZone" — กริดจะปูทับเขตนี้เอง ไม่ต้อง mark ทีละช่อง

**แถบเลือกป้อม (`TowerCardView`) — เทียบเท่าการ์ดฝั่ง Invader:**
- สร้าง UI container (แถบล่างจอ) เป็นลูกใน UI ฝั่ง Fort → ในนั้นสร้างการ์ด 1 ใบต่อป้อม ใส่ `TowerCardView` (`Assets/Scripts/UI/TowerCardView.cs`) ผูก:
  - `placement` → `TowerPlacementInputController`, `tower` → `TowerDefinitionSO` ของป้อมใบนั้น
  - `button`, `canvasGroup` (หรี่เทาเมื่อเงินไม่พอ), `selectedHighlight` (โชว์ตอนป้อมนี้ถูกเลือก — ปิดไว้ตอนเริ่ม), `nameLabel`, `costLabel`
- กดการ์ด → เรียก `SelectTower(towerId)` เอง (แทนปุ่มดิบ) → แล้ว tap ช่องกริดเพื่อวาง; วางเสร็จ selection เคลียร์ ไฮไลต์หาย
- การ์ดโชว์แค่ราคา + เทา + ไฮไลต์ที่เลือก (ไม่มี stack — วางทีละป้อม)

**เวลาสร้างป้อม (build time) — คุม balance:**
- ที่ `TowerDefinitionSO` ตั้ง `buildTimeSeconds` (เช่น 3) — วางแล้วป้อม**โผล่ทันทีแต่ยังยิงไม่ได้** จนสร้างเสร็จ (โจมตี/ทำลายได้ระหว่างสร้าง → มอนรีบทุบก่อนเสร็จได้). `0` = ใช้งานได้ทันที; Fort/ป้อมที่วางในซีนไม่นับ build (ไม่ผ่าน Initialize)
- **แถบ progress สร้าง (option):** ใน tower prefab เพิ่ม child World-Space Canvas → Image (Filled) + `TowerBuildBarDisplay` (`Assets/Scripts/UI/TowerBuildBarDisplay.cs`) ผูก `fillImage` (+ `bar` = root ที่จะปิดเมื่อเสร็จ) — แถบเติมตอนสร้าง แล้วซ่อนเอง (คล้าย `HealthBarDisplay`)

### 6.7 ป้อมหลายแบบ + อัพเกรดแยกสเตตัส (armor % + multi-target)

ป้อมมีสเตตัสเพิ่ม + อัพเกรดได้ทีละสเตตัส (5 อย่าง): **attack / HP / armor / range / targets**

**สเตตัสใหม่:**
- **`armor`** — ลดดาเมจแบบ **%** : `ดาเมจจริง = dmg × 100/(100+armor)` (armor 100 = ลดครึ่ง; อย่างน้อยโดน 1 เสมอ). อยู่ใน `CharacterBase` แล้ว — มอน/Fort ก็มี armor ได้
- **`maxTargets`** — ป้อมยิงหลายเป้าพร้อมกัน (ยิง N ตัวใกล้สุดในระยะต่อรอบ)

**อัพเกรดแยกสเตตัส (per-stat):** แต่ละป้อมเก็บ level ต่อสเตตัสเอง → stat จริง = ฐาน + level×ต่อระดับ. **ราคาแพงขึ้นทุกระดับ** (`baseCost × growth^level`). ยัง**เก็บระบบ tier เดิม** (nextTier swap) ไว้ด้วย — คนละปุ่ม (tier evolution = ป้อม gen ใหม่ HP เต็ม, per-stat = อัพป้อมตัวเดิม)

**โค้ดพร้อมแล้ว — ตั้งค่าใน Unity:**
- ที่ `TowerDefinitionSO` แต่ละตัว:
  - ฐาน: `armor`, `maxTargets`
  - section **Per-stat upgrades** — ตั้งต่อสเตตัส (`attackUpgrade`/`healthUpgrade`/`armorUpgrade`/`rangeUpgrade`/`targetsUpgrade`): `amountPerLevel` (เพิ่มต่อระดับ), `maxLevel` (0 = อัพไม่ได้), `baseCost`, `costGrowthPerLevel` (เช่น 1.5)
- **ป้อมหลายแบบ:** สร้าง `TowerDefinitionSO` หลายตัว (stat/prefab/upgrade ต่างกัน) + ใส่ใน `TowerDatabaseSO` + ทำการ์ด `TowerCardView` ต่อป้อม — โค้ดรองรับจำนวนเท่าไรก็ได้
- **เมนูอัพเกรด (ต่อจากข้อ 5):** ที่ Action menu ของ `TowerInteractionController` เพิ่มปุ่ม 5 ปุ่ม → `OnClick` เรียก `UpgradeAttack()` / `UpgradeHealth()` / `UpgradeArmor()` / `UpgradeRange()` / `UpgradeTargets()` (ปุ่ม tier `UpgradeSelected()` เดิมยังอยู่); ซ่อม/ทำลายคงเดิม
  - reject: max level → console "Already max level"; เงินไม่พอ → "Not enough gold"; Fort → "Cannot upgrade the Fort"

### 6.8 Animation มอน (Invader) + พฤติกรรม stop-and-attack

ผูก animation ผ่าน **`animator.CrossFade`** (ขับด้วยโค้ด ไม่ต้องลากเส้น transition) + เปลี่ยนพฤติกรรมโจมตีจาก "ยิงระหว่างเดิน" → **"หยุด หันหน้าเข้าเป้า เล่น Attack แล้วเดินต่อ"**

**สเตต animation (server สั่ง → replicate ทุก client ผ่าน NetworkVariable):**
- `Walk` เดิน / `Idle` หยุดนิ่ง (holding ที่ป้อมรอ cooldown) / `Injured Walk` เดินตอน HP ต่ำกว่า `injuredHealthFraction`
- `Attack` ตอนหยุดตี (หันหน้าเข้าเป้าก่อน, ดาเมจลงตอนจบ swing) / `Death` ตอนตาย (ค้างโชว์ `deathAnimSeconds` แล้ว despawn) / `Victory`(invader ชนะ) / `Lose`(แพ้) ตอนจบแมตช์

**ตั้งค่าใน Unity (ต่อ Monster prefab แต่ละตัว):**
1. เพิ่ม **Animator** + Animator Controller ที่มี state 7 ท่า **ชื่อเป๊ะ**: `Idle` `Walk` `Attack` `Injured Walk` `Death` `Victory` `Lose` — **ไม่ต้องลากเส้น transition** (โค้ด CrossFade เอง); ลาก clip เข้าเป็น state ลอยๆ ได้เลย
2. **⚠️ ปิด Apply Root Motion** ที่ Animator (การเคลื่อนที่ขับด้วยโค้ด — ถ้าเปิดจะสู้กัน); ใช้ clip แบบ **In Place** (Mixamo ติ๊ก "In Place")
3. ที่ `MonsterCharacter` (บน prefab) ผูก field **`animator`** + จูน:
   - `attackDurationSeconds` = ให้พอดีความยาว **Attack clip** (ดาเมจลงตอนจบ)
   - `injuredHealthFraction` (เช่น 0.3), `turnSpeedDegPerSec`, `deathAnimSeconds`
4. หมายเหตุ: มอน**หยุดตอนตี**แล้ว (ต่างจากเดิม) → advance ช้าลงนิด ปรับ balance ด้วย `attackCooldown`/`attackDurationSeconds`

### 6.9 ระบบ Faction (asset-based) — เพิ่มเผ่าได้โดยไม่แก้โค้ด

เปลี่ยนจาก master database + field faction → **1 เผ่า = 1 asset (`FactionSO`)** + **`FactionRegistrySO` ตัวเดียว** เป็นจุดเข้า. network id = `factionId/localId` ประกอบให้เอง → **id ไม่ชนข้ามเผ่า, จัดการต่อเผ่า**

**โครง asset ที่ต้องทำ:**
1. ต่อเผ่า: **Assets → Create → Splice → Faction** (`FactionSO`) — ตั้ง `factionId` (เช่น "human", ห้ามมี `/`, unique ทั้งเกม), `family` (หมวด 4), แล้วลาก `CardDefinitionSO`/`TowerDefinitionSO` ของเผ่านั้นเข้า list `cards`/`towers`
2. ตัวเดียว: **Create → Splice → Faction Registry** (`FactionRegistrySO`) → ลาก `FactionSO` ทุกเผ่าเข้า list `factions`
3. **ผูก registry** เข้า 3 ที่: `DeploymentManager.registry`, `TowerDeploymentManager.registry`, `DraftManager.registry`

**id:**
- `CardDefinitionSO.cardId` / `TowerDefinitionSO.towerId` = **local id (unique เฉพาะในเผ่าตัวเอง)** เช่น "swordsman" — ไม่ต้องคุมทั้งเกม
- network id เต็ม = `factionId/localId` (เช่น `human/swordsman`) — resolve ผ่าน registry อัตโนมัติ

**เพิ่มเผ่าใหม่ในอนาคต:** สร้าง `FactionSO` ใหม่ + ใส่ cards/towers + ลากเข้า `FactionRegistry.factions` → **ไม่ต้อง recompile**

**หมายเหตุ:**
- `CardDatabaseSO`/`TowerDatabaseSO` เดิม + asset ของมัน (`Human_CardDatabase.asset` ฯลฯ) **เลิกใช้แล้ว** — ย้ายการ์ด/ป้อมไปไว้ใน `FactionSO` แทน แล้วลบ asset เก่าได้
- `BotController.hand` ต้องใช้ **composite id** (`factionId/cardId`) แล้ว
- ต้องมี `cardId`/`towerId` ครบทุกใบ + อยู่ใน `FactionSO` สักเผ่า (ถ้าไม่อยู่ → `IdOf` คืน null, deploy ไม่ได้)

### 6.10 ซื้อ Miner ผ่านการ์ด (buildtime + stack + spawn point)

miner แยกต่อเผ่า + มีการ์ดของตัวเอง + spawn point ของตัวเองบนแมป — **ซื้อได้เหมือนมอน** (กดซื้อ → หักทอง → นับถอยหลัง → เกิดที่ spawn point → ทำงานเอง)

**data ที่ต้องทำ:**
- `MinerDefinitionSO`: มี `buildTimeSeconds` แล้ว (เวลาสร้างหลังกดซื้อ) — ตั้งค่า + ลาก miner prefab
- **การ์ด miner** = `CardDefinitionSO` ตั้ง `cardType = Miner` + ลาก `MinerDefinitionSO` เข้า `linkedMiner` + ตั้ง `cardId`(local)/`goldCost`/`requiredLevel`
- ใส่การ์ด miner เข้า **`FactionSO.minerCards`** (ของเผ่านั้น) — registry จะ resolve id ให้เอง (`factionId/cardId`)

**scene setup:**
1. วาง **`MinerDeploymentManager`** (มี `NetworkObject`) → ตั้ง `registry`, `deployTeam`, `spawnPoint` (Transform จุดเกิด miner ของทีมนั้น บน NavMesh)
2. UI แถบซื้อ miner (คล้ายการ์ดมอน แต่ไม่ผูกเลน): การ์ด 1 ใบ/miner ใส่ **`MinerCardView`** ผูก `deployment` (= MinerDeploymentManager), `card`, + widgets (`button`/`canvasGroup`/`lockOverlay`/`nameLabel`/`costLabel`/`stackLabel`/`cooldownOverlay`/`cooldownFill`/`cooldownLabel`) — เหมือน `MonsterCardView`
3. ใส่แถบนี้ใน `SideSelectionController` ฝั่งที่เกี่ยว (เปิดเฉพาะตอนเลือกฝั่งนั้น)

**พฤติกรรม:** กดการ์ด → หักทอง → คิว FIFO (stack `xN`) → ครบ `buildTimeSeconds` → miner เกิดที่ `spawnPoint` → `MinerCharacter` วิ่งขุดทองเอง (บ่อทีมตัวเอง+Neutral). จบแมตช์หยุด spawn

> miner ที่วางในซีนตั้งแต่แรกยังใช้ได้ (ตั้งต้น) — อันนี้เพิ่ม "ซื้อเพิ่ม" ระหว่างเกม

### 6.11 🟢 Player Base snapshot (ขั้น 5.1 — data foundation ของ Invader-live / Defender-async)

> **Role Model v0.2** (architecture §1.1, §5.10): Invader เล่นสดเป็นแกน / Defender จัดผังฐานไว้ล่วงหน้า (async) รวมร่างกับ Lair — ขั้นนี้คือรากฐานข้อมูล: **ผังฐานเก็บเป็น snapshot แล้วโหลดกลับมา spawn เป็นฝั่งตั้งรับได้**

**โค้ดใหม่ (`Scripts/Base/`, namespace `Splice.Base`):**
- `PlayerBaseData` — โครง save: `BaseLayout` (ป้อม+upgrade levels / garrison / miner / ทองคลัง) + `ArmyPreset` (ทัพบุกจัดล่วงหน้า) — id เป็น composite id ของ registry
- `PlayerBaseStore` — save/load ลง PlayerPrefs (local JSON, เปลี่ยนเป็น cloud ทีหลังได้)
- `RaidSnapshotLoader` — ฝั่ง server: อ่าน `BaseLayout` → spawn ป้อม (apply upgrade + ข้ามเวลาก่อสร้าง) + miner + ตั้งทองคลังทีมตั้งรับ

**scene setup (ในซีน raid):**
1. วาง GameObject **`RaidSnapshotLoader`** (มี `NetworkObject`) → ตั้ง `registry`, `defendSide=Defender`, `minerSpawnPoint` (จุดเกิด miner ฝั่งรับ บน NavMesh), `targetFactionId` (เผ่าของเมืองที่จะโหลด — เว้นว่าง=เผ่าที่เลือกอยู่ `PlayerProfile.ActiveFactionId`)
2. ยังไม่ต้องติ๊ก `loadLocalSaveOnSpawn` จนกว่าจะ capture ผังครั้งแรก (ดูวิธีเทสด้านล่าง)

> **v0.2:** ผังเก็บ **แยกต่อ faction** (โมเดล B: 1 เมือง/เผ่า) + stamp `ownerAccountId`. ตอน capture/build ต้องมี faction (ตั้ง `targetFactionId`/`factionId` ใน Inspector หรือเลือก faction ผ่าน `FactionSelectionController` ก่อน)

**วิธีเทส loop snapshot (ยังไม่มี Build Mode — ใช้ debug capture ไปก่อน):**
1. Play → เล่นฝั่ง Fort → วางป้อม/อัพเกรดตามใจ
2. คลิกขวาที่ component `RaidSnapshotLoader` (Inspector, ตอน Play) → **`Debug/Capture Scene Towers -> Save Layout`** → ผัง+ทองคลังลง local save
3. หยุด Play → ติ๊ก **`loadLocalSaveOnSpawn`** → Play ใหม่ → ป้อมชุดเดิมโผล่เองพร้อมยิงทันที (ไม่มีแถบก่อสร้าง) + ทองทีม Defenders = ค่าที่ capture ไว้

**หมายเหตุ:** `FortCore` ไม่อยู่ใน layout (เป็นของแมปฐาน ตายตัวต่อซีน); garrison มีที่เก็บใน data แล้วแต่ยัง**ไม่ spawn** (ขั้น 5.3); การเลือกเป้าหมาย/loot คือขั้น 5.4

### 6.12 🟢 Build Mode — จัดผังฐานนอกแมตช์ (ขั้น 5.2)

หน้าจัดผังฐานแบบ **offline ล้วน (ไม่มี network)** — วาง/ย้าย/ลบป้อมบน grid แล้ว save เป็น `BaseLayout` (แทน debug capture ในขั้น 5.1). ใช้ **กติกา grid เดียวกับตอนวางป้อมในแมตช์**: refactor ออกมาเป็น `Splice.Core.BuildGrid` แล้วทั้ง `TowerDeploymentManager` (สด) และ `BaseBuildManager` (build) ใช้โค้ดเดียวกัน

**โค้ดใหม่ (`Scripts/Base/`):**
- `BaseBuildManager` — core: โหลดผังเดิมตอน Start, `ArmTower()`/`ArmGarrison()` เลือกชนิด, วาง/ย้าย/ลบ, `Save()` (ป้อม+garrison)
- `BaseBuildInputController` — แตะวาง/เลือก/ย้าย (+ วง preview เขียว/แดง) — offline ไม่มี ServerRpc
- `BaseBuildPalette` — **สร้างปุ่มเอง (dynamic) ตามเผ่าที่เลือก** (ป้อมจาก `FactionSO.towers`, มอนจาก `FactionSO.cards`)
- `BaseBuildPaletteButton` — ปุ่ม 1 ชนิด (ผูก dynamic ผ่าน `BindTower/BindGarrison` หรือลากใส่ Inspector เองก็ได้)
- `BaseBuildPiece` — component preview runtime ป้อม/มอน (ปิด network/combat) + ถือ `Cost`/`Paid` สำหรับ checkout
- **เศรษฐกิจ (checkout):** `Splice.Core.PlayerWallet` (meta gold ถาวร) + `BaseBuildCostDisplay` (โชว์ยอด/ทอง) + `BaseBuildCheckoutController` (ปุ่ม Checkout + panel ยืนยัน)
- `Splice.Core.BuildGrid` — กติกา grid ที่แชร์กัน (snap + build-zone probe + กรอบสี่เหลี่ยม `halfExtentCells`)

**⚠️ ผลกระทบจาก refactor:** ฟิลด์ grid ใน `TowerDeploymentManager` ย้ายเข้า struct `BuildGrid` (foldout ชื่อ **Grid**) — ค่า `cellSize`/`gridOrigin` ในซีนตรงกับ default อยู่แล้ว แต่ **เช็ค `buildLayerMask` ใน foldout Grid ของ TowerDeploymentManager ทั้ง 2 ตัวใน Bootstrap.unity ซ้ำ** (อาจรีเซ็ตเป็น Everything)

**scene setup (แนะนำแยกเป็น scene `BaseBuild` ต่างหาก — ไม่มี GameBootstrap/NetworkManager):**
1. สร้าง **Plane พื้น bg** ของเมือง + ใส่ **Collider** (Mesh/Box) บนแผ่น + ตั้ง layer ให้อยู่ใน buildLayerMask — นี่คือ floor ที่ grid จะปูทับ
2. วาง GameObject **`BaseBuildManager`** → ตั้ง `registry`, `factionId` (เมืองของเผ่าไหน — เว้นว่าง=เผ่าที่เลือกอยู่), **`floor`** (ลาก **Collider ของ Plane** ข้อ 1 → จัด gridOrigin + ขนาดพื้นที่เมืองบนผิวแผ่นให้เอง), `placedRoot` (เว้นว่างได้), **`placeYOffset`** (ยกจากผิว floor กันลอย — 0=แปะพื้น)
   - **ขนาดช่อง grid = dynamic** มาจาก **`footprint` ในแต่ละ SO** → ตั้ง `footprint` (world units) ใน `TowerDefinitionSO`/`MonsterDefinitionSO` = ขนาดที่ชิ้นนั้นกินบน grid (`grid.cellSize` เป็นแค่ fallback)
   - **ขยายเมือง = scale Plane floor** → พื้นที่วางเพิ่มเอง
   - *(คลิกขวาที่ `BaseBuildManager` → **Fit Grid To Floor** เพื่อพรีวิวใน editor ได้; ตอน Play มันจัดให้เองอยู่แล้ว)*
3. วาง **`BaseBuildInputController`** → ตั้ง `buildManager`, `raycastCamera` (กล้อง Build Mode), `placementPreview` (ลาก `RangeIndicator` — optional). *(ไม่มี buildLayerMask แล้ว — กด/preview คำนวณจากระนาบ grid ตรงๆ กัน parallax ตอนกล้องเอียง)*
4. **Palette (dynamic ตามเผ่า — แนะนำ):** palette จะ **สร้างปุ่มเองจากเผ่าที่กำลังจัดเมือง** (ไม่ต้องทำปุ่มมือทีละตัว/ทีละเผ่า)
   - ทำ **prefab ปุ่ม 1 อัน**: GameObject มี `Button` + child TMP label (+ highlight optional) + component **`BaseBuildPaletteButton`** ผูก `button`(ตัวเอง)/`nameLabel`/`selectedHighlight` — **ไม่ต้องใส่ `tower`/`garrisonCard`** (palette จะ bind ให้ตอนรัน)
   - วาง **`BaseBuildPalette`** ในซีน → ผูก `buildManager`, `buttonPrefab` (prefab ปุ่มด้านบน), `towerContainer` (ที่วางปุ่มป้อม, มี Layout Group), `garrisonContainer` (ที่วางปุ่มมอน — เว้นว่าง=รวมกับ tower)
   - ตอนรัน `BaseBuildPalette.Start()` จะ resolve เผ่าจาก `PlayerProfile.ActiveFactionId` (หรือ `factionId` ที่ override ใน BaseBuildManager) → สร้างปุ่ม 1 อัน/ป้อม + 1 อัน/มอน. **สลับเผ่าแล้วเรียก `Rebuild()`** palette เปลี่ยนตาม
   - *(ทางเลือก manual: ถ้าอยากตรึงปุ่มบางอัน ใช้ `BaseBuildPaletteButton` ลาก `tower`/`garrisonCard` ใส่เองใน Inspector ก็ยังได้)*

   > **"หลายเผ่า" จัดการยังไง:** ไม่ต้องทำ palette แยกต่อเผ่า — เมือง 1 หลัง = 1 เผ่า (โมเดล B), palette อ่านเผ่าของเมืองที่กำลังจัดแล้วโชว์เฉพาะป้อม/มอนของเผ่านั้นเอง
5. **ปุ่ม Checkout / Discard / Clear / Cancel (economy — ไม่มี auto-save):**
   - **`BaseBuildCostDisplay`** (TMP) โชว์ยอด `NetCost` + ทอง meta + **Defense (used/max)** — วาง/ลบแล้วยอดขยับ; ทองไม่พอ **หรือเต็มเพดานฝ่ายรับ** ปุ่ม palette เทาเอง
   - 🔴 **DefenseCapacity (กัน defense snowball, architecture §5.10):** ตั้ง **`defenseCapacityCost`** ในแต่ละ `TowerDefinitionSO`/`MonsterDefinitionSO` (กิน capacity เท่าไหร่) + `baseCapacity`/`capacityPerLevel` ใน `BaseBuildManager` (เพดาน = f(base level)). วางรวมเกินเพดานไม่ได้ — เพิ่มเพดาน = อัพ base level (`PlayerProfile.SetBaseLevel`) ไม่ใช่จ่ายเงิน
   - **แสดงเพดาน:** ใช้ `BaseBuildCostDisplay.capacityLabel` (รวมกับยอด/ทอง) **หรือ** component เดี่ยว **`BaseBuildCapacityDisplay`** (ผูก `buildManager`+`label`, ปรับ format + สีตอนเต็ม) วางไม่ได้
   - **Checkout** → **`BaseBuildCheckoutController.OpenConfirm()`** → panel ยืนยัน 'จ่าย X?' → ตกลง `Confirm()` = **หักทองจริง + commit + persist**
   - **Discard** → **`BaseBuildManager.Discard()`** (ทิ้ง draft กลับ commit ล่าสุด)
   - **Clear** → `ClearAll()` (ลบทั้งเมือง — ชิ้นที่จ่ายแล้วคืนเงินบางส่วนเข้ายอด), **Cancel** → `CancelSelection()` (เลิกถือของ), (optional) **Delete** → `DeleteSelected()`
6. **กล้อง pan + zoom + เอียง:** แปะ **`CameraPanController`** บนกล้อง Build Mode → ตั้ง `panBounds` (BoxCollider คลุมพื้นที่เมือง), `minHeight`/`maxHeight` (**ตั้งคร่อมความสูงกล้องปัจจุบัน**) — ลากนิ้ว = pan (รอบทิศแม้มองดิ่ง 90°), จีบ 2 นิ้ว/scroll = zoom
   - **ปุ่มเอียงกล้อง:** wire ปุ่ม → **`CameraPanController.ToggleTilt()`** (สลับ `topDownAngle` 90° ↔ `tiltAngle` เช่น 55°) — **smooth** ปรับที่ `tiltSpeed` (องศา/วินาที), และ **หมุนรอบ object ล่าสุด** ถ้าผูก **`cameraPan`** ใน `BaseBuildManager` (มันจะ SetFocusPoint ตำแหน่งที่วาง/เลือกล่าสุดให้); ไม่ผูก = หมุนรอบจุดกลางจอบนพื้น (`groundY`)
7. **Grid overlay:** วาง **`BuildGridOverlay`** → ผูก `buildManager`, **`viewCamera`** (กล้อง Build Mode — เว้นว่าง=Camera.main) (+ optional `markerMaterial` โปร่งใส) → กดเลือกป้อม/มอนจะโชว์ช่องที่วางได้ (เขียว=ว่าง/แดง=มีของ):
   - **วาดจากกลางจอ ไล่ออกเป็นวงกลม** (grid อยู่กลางจอเสมอ, ช่องน้อยก็ไม่ไปกองขอบ)
   - **ขนาดช่อง = `footprint` ของชิ้นที่เลือก** (dynamic)
   - **view-culled** วาดแค่รัศมีที่ครอบจอ → floor ใหญ่/ขยายแค่ไหนก็ไม่ค้าง, pan/zoom กล้อง grid ตามไป

**การใช้งาน/เทส:**
- กดปุ่ม palette (ไฮไลต์) → **grid โชว์ช่องที่วางได้** → **แตะปล่อยบนช่อง = วาง** (snap กลางช่อง, ช่องมีของแล้ว/นอกกรอบเมืองวางไม่ได้)
- **ลากนิ้ว = pan / จีบ = zoom** (ถือของอยู่ก็ pan/zoom ได้ — แตะที่ลากไม่ถือว่าวาง) → กด **Cancel** เลิกถือของ
- ไม่ได้เลือก palette → แตะชิ้นที่วางแล้ว = เลือก → แตะช่องอื่น = ย้าย (ฟรี); ลบชิ้นที่จ่ายแล้วได้เงินคืนบางส่วน
- วางของ → **ยอด "ต้องจ่าย" ขึ้น**; ทองไม่พอ = palette เทา วางเพิ่มไม่ได้ → กด **Checkout** ยืนยันจ่าย (หักทอง + commit) หรือ **Discard** ทิ้ง draft
- checkout แล้ว → ไปซีน raid ที่ติ๊ก `loadLocalSaveOnSpawn` บน `RaidSnapshotLoader` (6.11) → **สิ่งที่ commit ไว้โผล่เป็นฝั่งตั้งรับ** = loop จัดเมือง→ถูก raid ครบวง

> ขั้น 5.2 แก้ผังป้อม; **ขั้น 5.3 เพิ่มมอนเฝ้า (garrison)** วางในผังเดียวกัน (ดู §6.13); ทองคลัง/miner ในผังคงค่าเดิมตอน save (economy = 5.5). ยังไม่มี UI อัพเกรดป้อมใน Build Mode (upgrade levels save/โหลดได้ ตั้งค่าเริ่ม 0)

> **พื้นที่เมือง = สี่เหลี่ยมจัตุรัส fix** (`grid.halfExtentCells`) — วางได้เฉพาะในกรอบ; **ขยายเมือง (ซื้อพื้นที่)** = เพิ่มค่านี้ (ระบบซื้อ = ขั้น 5.5)

### 6.13 🟢 Garrison — มอนเฝ้าเมือง (ขั้น 5.3)

มอนสเตอร์เฝ้าเมืองฝ่ายตั้งรับ — ยืนกับที่ (ไม่เดินเลน) ตื่นสู้เมื่อทัพผู้บุกเข้าระยะ. เพิ่ม **monster-vs-monster** (ของเดิมมีแค่ มอนตีป้อม / ป้อมตีมอน)

**โค้ด:**
- `MonsterCharacter` เพิ่มฟิลด์ **`side` (`RaidSide`)**: `Attacker` = เดินเลนบุก (เหมือนเดิม) / `Defender` = garrison ยืนเฝ้า (`InitializeGarrison`)
- **การเล็งเป้า (`FindAttackTarget`):** Attacker ตี **ป้อม/Fort + มอน garrison** ฝ่ายตรงข้าม; garrison ตี **มอน Attacker** เท่านั้น (ป้อมทั้งหมดถือเป็นฝ่าย Defender)
- `RaidManager` นับตกรอบเฉพาะมอน **ฝ่าย Attacker** (garrison ไม่นับ)
- `RaidSnapshotLoader.SpawnGarrison` — spawn มอน garrison จาก `BaseLayout.garrison` ตอนโหลดเมืองตั้งรับ

**prefab มอน:** ใช้ **prefab เดียวกับมอนบุก** ได้เลย (มี `MonsterCharacter`+`NetworkObject`) — RaidSnapshotLoader จะเรียก `InitializeGarrison` ตั้ง `side=Defender` ให้เอง; ถ้าวางมอนในซีนตรงๆ เป็น garrison ให้ตั้ง `side=Defender` ใน Inspector

**วิธีเทส (ต่อจาก §6.11/§6.12):**
1. Build Mode: กดปุ่ม palette ที่เป็น **garrisonCard** → วางมอนลงช่อง (เหมือนวางป้อม) → **Save**
2. ซีน raid ติ๊ก `loadLocalSaveOnSpawn` → Play → มอน garrison โผล่ยืนเฝ้า
3. ส่งมอนบุก (deploy ปกติ) เข้าไป → มอนบุกกับ garrison **ตีกัน**; garrison กันทางก่อนถึงป้อม/Fort

> **หมายเหตุ:** debug capture (§6.11) เก็บเฉพาะ **ป้อม** จากซีน — garrison ต้องวางผ่าน Build Mode (§6.12). ตอนนี้ garrison ยืนกับที่ล้วน (ไม่ไล่/ไม่กลับจุด) — พอสำหรับ greybox

### 6.14 🟢 Raid flow — เลือกเป้าหมาย → บุก → loot (ขั้น 5.4)

flow เมตา: เลือกฐานเป้าหมาย → โหลด snapshot มาเป็นฝ่ายตั้งรับ → เล่น → **ชนะ (ทำลาย Fort) ได้ loot** เข้ากระเป๋า meta

**โค้ดใหม่ (`Scripts/Base/`):**
- `RaidTarget`/`RaidContext` — เป้าหมาย + ตัวส่งต่อข้ามซีน (static)
- `RaidTargetProvider` — generate **ฐาน bot** (greybox ไม่มี cold start) จาก registry; owner = `bot_x` (≠ ผู้เล่น)
- `RaidTargetSelectionController` + `RaidTargetButton` — จอเลือกเป้า → `Raid(index)` → โหลดซีน raid
- `RaidRewardController` — จบ raid ชนะ → loot % ของทองคลังเป้าหมาย เข้า `PlayerWallet`
- `RaidSnapshotLoader` — โหลด `RaidContext.Target` (ถ้ามี) แทน local save

**scene setup:**
- **จอเลือกเป้า** (เช่นในเมือง/Bootstrap): `RaidTargetProvider` (ตั้ง `registry`, `baseOrigin` ให้ตรงโซนตั้งรับในซีน raid) + `RaidTargetSelectionController` (ตั้ง `provider`, `raidSceneName`) + ปุ่มเป้า N ปุ่ม ใส่ `RaidTargetButton` (ผูก controller + `index`)
- **ซีน raid:** `RaidSnapshotLoader` (ไม่ต้องติ๊ก `loadLocalSaveOnSpawn` — โหลดจาก RaidContext เอง) + `RaidRewardController` (ผูก `raidManager`, `lootPercent`) + `RaidResultUI.lootLabel` (โชว์ loot)
- **Build Settings:** ใส่ทั้งซีนเลือกเป้า + ซีน raid

**การใช้งาน/เทส:**
1. จอเลือกเป้า → กดปุ่มฐาน bot → โหลดซีน raid → ฐานเป้า spawn ตั้งรับ (ป้อม+garrison+ทองคลัง)
2. deploy ทัพบุก → ทำลาย Fort = ชนะ → **ได้ loot %** ของทองคลัง โชว์ในจอผล + เข้ากระเป๋า meta
3. **บุกเมืองบัญชีตัวเองไม่ได้** (attacker≠defender); **replay เป้าเดิมไม่ได้ loot ซ้ำ** (`Looted`)

> ⚠️ greybox local — เป้าเป็น bot generate. **raid ผู้เล่นจริง + หัก loot จากฐาน defender + shield/cooldown ต่อคู่เป้า = ตอนย้าย server** (architecture §5.10/§10)

### 7. ทดสอบ
- [ ] Play scene `Bootstrap` → `GameBootstrap` (mode PvE) จะ `StartHost()` ให้อัตโนมัติ
- [ ] เลือกฝั่ง: เริ่มเกมเห็น Panel เลือก Fort/Monster → กดฝั่งหนึ่ง → camera โดดไป viewpoint ฝั่งนั้น, controller/UI ฝั่งนั้นเปิด อีกฝั่งปิด, Panel หาย
- [ ] เช็คว่า miner เดินไป GoldNode → ขุดจนเต็ม → วิ่งกลับ MinerBase → `GoldDisplay` เพิ่มตอนถึงฐาน (ทองมาจาก miner เท่านั้น ไม่มี regen, ทองเข้าเฉพาะตอนถึงฐาน)
- [ ] ทดสอบเจ้าของบ่อ (ข้อ 6.5): ตั้งบ่อ `owner=Invaders`/`Defenders`/`Neutral` → miner **ไม่ข้ามไปบ่อฝั่งศัตรู** (ขุดเฉพาะบ่อทีมตัวเอง + บ่อ Neutral); บ่อ Neutral มี miner สองฝั่งมาแย่งได้; miner กระจายบ่อใกล้ๆ ตาม `minersPerNode` พอบ่อใกล้หมดค่อยไปบ่อไกล
- [ ] ทดสอบ deploy monster: tap/click บน lane marker → gold ลด, monster spawn ที่จุดเริ่ม `LanePath` แล้ว **เดินตาม waypoint ไปเรื่อยๆ ไม่หยุด** จนถึง Fort (ไม่ติดซอกแบบ NavMesh)
- [ ] ทดสอบ deploy ผ่านกระท่อม (flow ใหม่ ข้อ 6.2):
  - กดกระท่อมของเลน → card UI ของเลนนั้นเปิด
  - การ์ดเงินไม่พอ/level ไม่ถึง = **เทา + กดไม่ได้** (level ไม่ถึงมี lock overlay); พอทองถึงเกณฑ์ = การ์ดกลับมากดได้เอง
  - กดการ์ดที่เรียกได้ → ทองลดทันที, การ์ดขึ้น **นับถอยหลัง** → ครบ `buildTimeSeconds` → monster เกิดที่ต้นเลนแล้วเดินตาม waypoint
  - กดการ์ดเดิมรัวๆ → ป้าย **`xN`** เพิ่มตาม, มอนทยอยเกิดทีละตัว (คิว FIFO ต่อเลน); ทองไม่พอสำหรับตัวถัดไป → การ์ดเทาเอง
  - แต่ละเลนคิวแยกกัน (กดเลน 0 ไม่กระทบคิวเลน 1)
- [ ] ทดสอบ stop-and-attack: วางป้อมข้างเลน → monster เดินถึงระยะ → **หยุด หันหน้าเข้าป้อม ตี (Attack) แล้วเดินต่อ** (ไม่ยิงระหว่างเดินแล้ว — เปลี่ยนพฤติกรรมตามข้อ 6.8), ถ้าโดนพอ ป้อมแตก
- [ ] ทดสอบหยุดที่ Fort: monster เดินเข้าไป**ใกล้ Fort (`fortHoldDistance`)** ค่อยหยุด (ไม่กองที่ core), ยิงระหว่างเข้าได้; **ไม่หยุดค้างกลางทาง** แม้ `attackRange` กว้าง; ถ้าไม่มี Fort ในซีน มอนเดินหน้าต่อ (ไม่ค้างปลายเลน)
- [ ] ทดสอบ separation (ข้อ 6.4): ปล่อยมอนหลายตัวเข้าไปที่ Fort → **กระจายเป็นวงไม่ซ้อนกองที่จุดเดียว**; เพิ่ม `separationRadius` แล้วเว้นห่างขึ้น
- [ ] ทดสอบ Ground vs Flying (ข้อ 6.4): มอน `movement=Ground` เดินติดพื้น; มอน `movement=Flying` ลอยสูง `flightHeight` เหนือพื้นตลอดทาง; ตัวบิน (attackRange ≥ flightHeight) เข้าตี Fort ได้
- [ ] ทดสอบเวลาสร้างป้อม (ข้อ 6.6): วางป้อม → **โผล่แต่ยังไม่ยิง** จนครบ `buildTimeSeconds` (แถบ progress ถ้าทำ) → ค่อยเริ่มยิง; ทุบป้อมระหว่างสร้างได้
- [ ] ทดสอบอัพเกรด per-stat (ข้อ 6.7): tap ป้อม → กดอัพ attack/HP/armor/range/targets → stat เพิ่ม (range ดูวง gizmo กว้างขึ้น, targets ยิงหลายตัว, armor โดนตีเจ็บน้อยลง), ทองลดตามราคาที่**แพงขึ้นทุกระดับ**; ครบ maxLevel → "Already max level"
- [ ] ทดสอบ animation มอน (ข้อ 6.8): มอนเดิน = **Walk**, HP ต่ำ = **Injured Walk**; ถึงระยะ+cooldown → **หยุด หันหน้าเข้าป้อม เล่น Attack แล้วเดินต่อ**; ตาย = **Death** แล้วค่อยหาย; จบแมตช์ = **Victory/Lose**
- [ ] ทดสอบระบบ faction (ข้อ 6.9): สร้าง `FactionSO` ต่อเผ่า + ใส่ `FactionRegistry` → deploy มอน/วางป้อมได้ (id resolve ถูก), เพิ่มเผ่าใหม่แล้วใช้ได้โดยไม่แก้โค้ด
- [ ] 🟢 ทดสอบซื้อ miner (ข้อ 6.10): กดการ์ด miner → ทองลด, ขึ้นนับถอยหลัง/stack (xN) → ครบ `buildTimeSeconds` → miner เกิดที่ spawn point แล้ว **วิ่งขุดทองเองอัตโนมัติ**; เงินไม่พอ/level ไม่ถึง = การ์ดเทา
- [ ] ทดสอบ defender วางป้อมแบบ grid (ข้อ 6.6): `SelectTower(towerId)` → เลื่อนนิ้วบน build zone → วง preview **snap ลงช่อง** + **เขียว/แดง** (แดงเมื่อนอกเขต/ทับป้อม/เงินไม่พอ) → tap ช่องเขียว → ป้อม spawn กลางช่อง, gold ลด; tap ช่องแดง/ทับป้อม → วางไม่ได้ (console "Cannot build here"); ทองไม่พอ → "Not enough gold"
- [ ] ทดสอบเห็นระยะ (ข้อ 6.3): เลือก monster/tower ใน Scene view → เห็นวงระยะ (Gizmo); ติ๊ก `Always Show Range` → วงโชว์ตลอดโดยไม่ต้องเลือก; ในเกมกด `SelectTower` → วง preview ตามเคอร์เซอร์บน build zone → วางป้อม → วงหาย. ปรับ `attackRange` ใน SO แล้ววงเปลี่ยนขนาดตาม
- [ ] ทดสอบเมนูป้อม (tap ป้อม → เมนูเด้ง):
  - **ซ่อม**: ปล่อยให้ป้อม HP หาย → กดซ่อม → HP กลับเต็ม, ทอง Defenders ลดตาม `ceil(goldCost × HPหาย/maxHP × repairFactor)`; HP เต็มอยู่แล้ว = ไม่เสียทอง
  - **อัพเกรด**: ป้อมมี `nextTier` → กดอัพเกรด → ป้อมเก่าหาย, gen ใหม่ spawn ที่เดิม HP เต็ม, ทองลด `upgradeCost`; ไม่มี nextTier → console "Tower action rejected: Already max level"
  - **ทำลาย**: กดทำลาย → ป้อมหาย, ทองคืน `floor(goldCost × HPเหลือ/maxHP × demolishRefundFactor)`
  - **Fort guard**: tap Fort → ซ่อมได้ แต่กดทำลาย/อัพเกรด → console reject
  - ทองไม่พอ (ซ่อม/อัพเกรด) → console "Tower action rejected: Not enough gold"; tap ที่ว่าง → เมนูปิด
- [ ] ทดสอบ win/lose 1:1:
  - ปล่อยให้ fort แตก → "Invaders Win!" + **monster ทุกตัวหยุดเดินทันที** (รวมตัวที่กำลังเดินมา) และไม่มีตัวใหม่ spawn จากคิว
  - ปล่อยให้ timer หมด (ตั้ง `matchDurationSeconds` สั้นๆ เช่น 20 ตอนเทส) โดย fort ยังอยู่ → "Fort Wins! (Time)"
  - ทำให้ invader ตกรอบ (ฆ่า miner + ทองเหลือ 0 + ไม่มี monster) → "Fort Wins! (Invader Eliminated)"
  - เช็คว่า `RaidResultUI` panel เด้ง + ปุ่ม Play Again reload scene ได้ และ `MatchTimerDisplay` นับถอยหลังถูกต้อง
- [ ] ค่อยเปิด `BotController` (ต่อกับ `DeploymentManager` ตัวเดียวกัน ใส่ `hand` เป็น cardId ที่มีจริงใน database) ตอนทดสอบโหมด PvBot

รายละเอียด roadmap เต็มดู [splice-development-roadmap.md](splice-development-roadmap.md) และ [technical-architecture.md](technical-architecture.md) หัวข้อ 12
