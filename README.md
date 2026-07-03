# Splice

Reverse Tower Defense — Roguelite Deckbuilder + Idle Meta. Mobile (iOS/Android), Unity 6 LTS, C#.

รายละเอียดสถาปัตยกรรมเต็ม ๆ อยู่ที่ [technical-architecture.md](technical-architecture.md) — อ่านไฟล์นั้นก่อนแก้โครงสร้างระบบใหญ่ ๆ

> **🟢 = อัปเดตล่าสุด / ยังต้องไปตั้งค่าต่อใน Unity** — มองหา 🟢 เพื่อดูว่ามีอะไรใหม่ที่ยังไม่ได้ประกอบใน Editor (เมื่อทำใน Unity เสร็จแล้ว ลบ 🟢 ออกได้)

## ตำแหน่งโปรเจกต์ Unity จริง

โปรเจกต์ Unity (สร้างจริงผ่าน Unity Hub, Editor **6000.5.1f1**) อยู่ที่:

```
Splice Game Client/Splice/
```

เปิดผ่าน Unity Hub → Add → เลือกโฟลเดอร์ `Splice Game Client/Splice` (ไม่ใช่โฟลเดอร์นี้ที่ README อยู่)

### สิ่งที่ต้องทำหลังเปิดโปรเจกต์ครั้งแรก

1. ติดตั้ง **Netcode for GameObjects** และ **Unity Transport** ผ่าน Window → Package Manager → `+` → Add package by name → พิมพ์ `com.unity.netcode.gameobjects` และ `com.unity.transport` (ให้ Package Manager เลือกเวอร์ชันที่ compatible กับ Editor 6000.5.1f1 เอง — ผมไม่ hardcode เลขเวอร์ชันให้เพราะไม่มี Editor ให้เช็ค compatibility จริง)
2. **ParrelSync** เพิ่มไว้ใน `Packages/manifest.json` แล้ว (เป็น git dependency) — Unity จะ resolve ให้อัตโนมัติตอนเปิด
3. โปรเจกต์มาจากเทมเพลต Universal 3D (Mobile) ของ Unity Hub เอง มี `Assets/TutorialInfo`, `Assets/Readme.asset`, `Assets/Settings/*` ติดมาด้วย — ลบได้เลยถ้าไม่ต้องการ ไม่กระทบ scaffold ที่เพิ่มไป
4. 🟢 **ล็อกจอเป็น Portrait (แนวตั้ง)**: Edit → Project Settings → Player → Resolution and Presentation → Default Orientation = **Portrait** (ปิด auto-rotation ตัวอื่น) — เกมเล่นแนวตั้งทุกโหมด (เลนวิ่งบน→ล่าง, เล่นมือเดียว, UI สำคัญอยู่ครึ่งล่างจอ) ดูเหตุผลใน `technical-architecture.md` §5.9

## โครงสร้างโฟลเดอร์ (ภายใต้ `Splice Game Client/Splice/`)

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
- **Economy — MinerBase (ฐานส่งทอง)**: วาง Empty GameObject ตรงจุดที่อยากให้เป็นฐานของทีม (เช่น ใกล้ fort) → `MinerBase` (`Assets/Scripts/Combat/MinerBase.cs`) ตั้ง `team = Invaders` — miner จะวิ่งกลับมาที่ตำแหน่งนี้เพื่อฝากทอง **ต้องวางบนพื้นที่ NavMesh เดินถึงได้**
- **Economy — GoldNode (บ่อทอง)**: วาง primitive หลายอันบนแผนที่ → `NetworkObject` + `NetworkTransform` (ถ้าอยากให้ client เห็น) + **Collider** (primitive มีให้อยู่แล้ว — ใช้วัดว่า miner ถึงบ่อ) + `GoldNode` (`Assets/Scripts/Combat/GoldNode.cs`) ตั้ง `totalGold` — วางกระจายให้บางบ่อไกลจากฐาน (ยิ่งไกล เที่ยวไป-กลับยิ่งนาน income ยิ่งช้า). miner ถือว่า "ถึง" เมื่อชนผิว collider ของบ่อ (ไม่ต้องถึงจุดกลาง) จึงไม่ต้องตั้ง `arrivalRadius` ใหญ่ตามขนาดบ่อ
- **Economy — Miner prefab**: primitive → `NetworkObject` + `NetworkTransform` + **`NavMeshAgent`** + `MinerCharacter` (`Assets/Scripts/Characters/MinerCharacter.cs`) ตั้ง `team = Invaders` + ผูก `definition` เป็น `MinerDefinitionSO` → save เป็น prefab. วางลง scene ตรงๆ 1 ตัวเป็น miner ตั้งต้นของทีม (วงจร: ไปบ่อ → ขุดเต็ม `carryCapacity` (ใช้เวลา `mineDurationSeconds`) → วิ่งกลับ MinerBase → ทองเข้าทีม) — ต้อง **Bake NavMesh** เหมือน monster ไม่งั้น miner เดินไม่ได้
- **Camera**: ตั้งกล้องมองลงมาที่ scene, ลาก camera ตัวนี้ไปที่ field `raycastCamera` ของ `DeployInputController`

### 3. ScriptableObject instances
- Assets → Create → Splice → **Monster Definition** / **Tower Definition** / **Miner Definition** / **Card Definition** / **Card Database** ตามจำนวนที่ต้องการ เก็บไว้ที่ `ScriptableObjects/Monsters/`, `Towers/`, `Cards/` ตามลำดับ
- แต่ละ `MonsterDefinitionSO`/`TowerDefinitionSO`/`MinerDefinitionSO` ตั้งค่า stat แล้วลาก prefab ที่ทำในขั้นตอน 2 มาใส่ field `prefab` — `MinerDefinitionSO` มี `goldPerTick`/`mineInterval` คุมอัตราขุด
- แต่ละ `CardDefinitionSO` ตั้ง `cardId`, `goldCost`, ลาก `MonsterDefinitionSO` ที่เกี่ยวข้องมาใส่ `linkedMonster`
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
- 🟢 **กดป้อม → เมนู ซ่อม/อัพเกรด/ทำลาย**:
  - **Upgrade chain**: ที่ `TowerDefinitionSO` แต่ละ tier ตั้ง `upgradeCost` (ราคาตายตัว) + ลาก SO ของ gen ถัดไปใส่ `nextTier` (เว้นว่าง = level สูงสุด). แต่ละ tier = SO+prefab แยก
  - **Cost factors** (ชั่วคราวบน `TowerDeploymentManager`, รอย้ายเข้า main config ข้อ B): `repairFactor` (default 0.5), `demolishRefundFactor` (default 1) — ค่าซ่อม `ceil(goldCost × HPหาย/maxHP × repairFactor)`, คืนเงินทำลาย `floor(goldCost × HPเหลือ/maxHP × demolishRefundFactor)`
  - **Tower layer**: ตั้ง **layer ให้ป้อม/Fort prefab** แล้วใส่ layer นั้นใน `TowerInteractionController.towerLayerMask`
  - **Action menu (Panel)**: สร้าง Panel มี 3 ปุ่ม (ซ่อม/อัพเกรด/ทำลาย) บน Screen Space Canvas, **ปิดไว้ตอนเริ่ม** → ลากใส่ `TowerInteractionController.actionMenu`
  - วาง `TowerInteractionController` (`Assets/Scripts/Input/TowerInteractionController.cs`) ผูก `towerDeploymentManager`, `raycastCamera`, `towerLayerMask`, `actionMenu`
  - ปุ่มเมนู `OnClick`: ซ่อม → `RepairSelected()`, อัพเกรด → `UpgradeSelected()`, ทำลาย → `DemolishSelected()`
  - พฤติกรรม: tap ป้อม → เมนูเด้งติดตัวป้อม; tap ที่ว่าง → เมนูปิด; Fort ซ่อมได้แต่ทำลาย/อัพเกรดไม่ได้ (server reject). ทองไม่พอ/max level → console "Tower action rejected: ..."

### 6. 🟢 Side selection (เลือกฝั่ง Fort/Monster ตอนเริ่ม + สลับกล้อง)
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

### 6.1 🟢 กล้อง Free-pan + กติกา deploy/build
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
- ⏳ ยังเหลือ: ฝั่ง Invader "deploy อิงเลนจาก UI" (เลือกเลนด้วยปุ่ม แทน tap lane marker ในโลก) ยังไม่ได้ทำ — ตอนนี้ deploy ยังต้อง tap lane marker ที่มองเห็น

### 7. ทดสอบ
- [ ] Play scene `Bootstrap` → `GameBootstrap` (mode PvE) จะ `StartHost()` ให้อัตโนมัติ
- [ ] 🟢 เลือกฝั่ง: เริ่มเกมเห็น Panel เลือก Fort/Monster → กดฝั่งหนึ่ง → camera โดดไป viewpoint ฝั่งนั้น, controller/UI ฝั่งนั้นเปิด อีกฝั่งปิด, Panel หาย
- [ ] เช็คว่า miner เดินไป GoldNode → ขุดจนเต็ม → วิ่งกลับ MinerBase → `GoldDisplay` เพิ่มตอนถึงฐาน (ทองมาจาก miner เท่านั้น ไม่มี regen, ทองเข้าเฉพาะตอนถึงฐาน)
- [ ] ทดสอบ deploy monster: tap/click บน lane marker → gold ลด, monster spawn ที่จุดเริ่ม `LanePath` แล้ว **เดินตาม waypoint ไปเรื่อยๆ ไม่หยุด** จนถึง Fort (ไม่ติดซอกแบบ NavMesh)
- [ ] ทดสอบ advance-and-shoot: วางป้อมข้างเลน → monster เดินผ่านแล้ว **ยิงป้อมไปด้วยโดยไม่หยุดเดิน**, ถ้าโดนพอ ป้อมแตก; ถึงปลายเลน monster ทุบ Fort จน HP ลด
- [ ] ทดสอบ defender วางป้อม: `SelectTower(towerId)` แล้ว tap บน build zone → gold ฝั่ง Defenders ลด, ป้อม spawn ตรงตำแหน่ง, เริ่มยิง monster; ทองไม่พอ → console "Tower deploy rejected: Not enough gold"
- [ ] 🟢 ทดสอบเมนูป้อม (tap ป้อม → เมนูเด้ง):
  - **ซ่อม**: ปล่อยให้ป้อม HP หาย → กดซ่อม → HP กลับเต็ม, ทอง Defenders ลดตาม `ceil(goldCost × HPหาย/maxHP × repairFactor)`; HP เต็มอยู่แล้ว = ไม่เสียทอง
  - **อัพเกรด**: ป้อมมี `nextTier` → กดอัพเกรด → ป้อมเก่าหาย, gen ใหม่ spawn ที่เดิม HP เต็ม, ทองลด `upgradeCost`; ไม่มี nextTier → console "Tower action rejected: Already max level"
  - **ทำลาย**: กดทำลาย → ป้อมหาย, ทองคืน `floor(goldCost × HPเหลือ/maxHP × demolishRefundFactor)`
  - **Fort guard**: tap Fort → ซ่อมได้ แต่กดทำลาย/อัพเกรด → console reject
  - ทองไม่พอ (ซ่อม/อัพเกรด) → console "Tower action rejected: Not enough gold"; tap ที่ว่าง → เมนูปิด
- [ ] ทดสอบ win/lose 1:1:
  - ปล่อยให้ fort แตก → "Invaders Win!"
  - ปล่อยให้ timer หมด (ตั้ง `matchDurationSeconds` สั้นๆ เช่น 20 ตอนเทส) โดย fort ยังอยู่ → "Fort Wins! (Time)"
  - ทำให้ invader ตกรอบ (ฆ่า miner + ทองเหลือ 0 + ไม่มี monster) → "Fort Wins! (Invader Eliminated)"
  - เช็คว่า `RaidResultUI` panel เด้ง + ปุ่ม Play Again reload scene ได้ และ `MatchTimerDisplay` นับถอยหลังถูกต้อง
- [ ] ค่อยเปิด `BotController` (ต่อกับ `DeploymentManager` ตัวเดียวกัน ใส่ `hand` เป็น cardId ที่มีจริงใน database) ตอนทดสอบโหมด PvBot

รายละเอียด roadmap เต็มดู [splice-development-roadmap.md](splice-development-roadmap.md) และ [technical-architecture.md](technical-architecture.md) หัวข้อ 12
