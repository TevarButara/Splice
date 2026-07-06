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
- **UI เลือกป้อม**: ปุ่ม UI เรียก `TowerPlacementInputController.SelectTower(towerId)` เพื่อเลือกว่าจะวางป้อมไหน แล้ว tap บน build zone เพื่อวาง (ตำแหน่งอิสระ ไม่ผูกเลน — วางใกล้เลนให้อยู่ในระยะยิง)
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

### 6.5 🟢 เจ้าของบ่อทอง (Gold Node ownership) — กัน miner ข้ามฝั่ง

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

### 7. ทดสอบ
- [ ] Play scene `Bootstrap` → `GameBootstrap` (mode PvE) จะ `StartHost()` ให้อัตโนมัติ
- [ ] เลือกฝั่ง: เริ่มเกมเห็น Panel เลือก Fort/Monster → กดฝั่งหนึ่ง → camera โดดไป viewpoint ฝั่งนั้น, controller/UI ฝั่งนั้นเปิด อีกฝั่งปิด, Panel หาย
- [ ] เช็คว่า miner เดินไป GoldNode → ขุดจนเต็ม → วิ่งกลับ MinerBase → `GoldDisplay` เพิ่มตอนถึงฐาน (ทองมาจาก miner เท่านั้น ไม่มี regen, ทองเข้าเฉพาะตอนถึงฐาน)
- [ ] 🟢 ทดสอบเจ้าของบ่อ (ข้อ 6.5): ตั้งบ่อ `owner=Invaders`/`Defenders`/`Neutral` → miner **ไม่ข้ามไปบ่อฝั่งศัตรู** (ขุดเฉพาะบ่อทีมตัวเอง + บ่อ Neutral); บ่อ Neutral มี miner สองฝั่งมาแย่งได้; miner กระจายบ่อใกล้ๆ ตาม `minersPerNode` พอบ่อใกล้หมดค่อยไปบ่อไกล
- [ ] ทดสอบ deploy monster: tap/click บน lane marker → gold ลด, monster spawn ที่จุดเริ่ม `LanePath` แล้ว **เดินตาม waypoint ไปเรื่อยๆ ไม่หยุด** จนถึง Fort (ไม่ติดซอกแบบ NavMesh)
- [ ] ทดสอบ deploy ผ่านกระท่อม (flow ใหม่ ข้อ 6.2):
  - กดกระท่อมของเลน → card UI ของเลนนั้นเปิด
  - การ์ดเงินไม่พอ/level ไม่ถึง = **เทา + กดไม่ได้** (level ไม่ถึงมี lock overlay); พอทองถึงเกณฑ์ = การ์ดกลับมากดได้เอง
  - กดการ์ดที่เรียกได้ → ทองลดทันที, การ์ดขึ้น **นับถอยหลัง** → ครบ `buildTimeSeconds` → monster เกิดที่ต้นเลนแล้วเดินตาม waypoint
  - กดการ์ดเดิมรัวๆ → ป้าย **`xN`** เพิ่มตาม, มอนทยอยเกิดทีละตัว (คิว FIFO ต่อเลน); ทองไม่พอสำหรับตัวถัดไป → การ์ดเทาเอง
  - แต่ละเลนคิวแยกกัน (กดเลน 0 ไม่กระทบคิวเลน 1)
- [ ] ทดสอบ advance-and-shoot: วางป้อมข้างเลน → monster เดินผ่านแล้ว **ยิงป้อมไปด้วยโดยไม่หยุดเดิน**, ถ้าโดนพอ ป้อมแตก
- [ ] ทดสอบหยุดที่ระยะ Fort: monster เดินเข้าใกล้ Fort → **หยุดทันทีพอ Fort เข้าระยะ `attackRange`** แล้วยิง (ไม่เดินเข้าไปกองที่ตัว Fort)
- [ ] ทดสอบ separation (ข้อ 6.4): ปล่อยมอนหลายตัวเข้าไปที่ Fort → **กระจายเป็นวงไม่ซ้อนกองที่จุดเดียว**; เพิ่ม `separationRadius` แล้วเว้นห่างขึ้น
- [ ] ทดสอบ Ground vs Flying (ข้อ 6.4): มอน `movement=Ground` เดินติดพื้น; มอน `movement=Flying` ลอยสูง `flightHeight` เหนือพื้นตลอดทาง; ตัวบิน (attackRange ≥ flightHeight) เข้าตี Fort ได้
- [ ] ทดสอบ defender วางป้อม: `SelectTower(towerId)` แล้ว tap บน build zone → gold ฝั่ง Defenders ลด, ป้อม spawn ตรงตำแหน่ง, เริ่มยิง monster; ทองไม่พอ → console "Tower deploy rejected: Not enough gold"
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
