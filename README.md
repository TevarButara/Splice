# Splice

Reverse Tower Defense — Roguelite Deckbuilder + Idle Meta. Mobile (iOS/Android), Unity 6 LTS, C#.

รายละเอียดสถาปัตยกรรมเต็ม ๆ อยู่ที่ [technical-architecture.md](technical-architecture.md) — อ่านไฟล์นั้นก่อนแก้โครงสร้างระบบใหญ่ ๆ

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

## โครงสร้างโฟลเดอร์ (ภายใต้ `Splice Game Client/Splice/`)

```
Assets/
  Scripts/
    Core/      GameBootstrap — เลือกโหมด PvE (local host) / PvBot / PvP (client-server)
    Data/      ScriptableObject definitions: Monster, Tower, Card
    Network/   Server-authoritative RPC (deploy, validate)
    Combat/    Mana regen server-side timer ฯลฯ
    Draft/     Stub — ระบบ draft การ์ด (phase 1)
    Lair/      Stub — meta idle/collection (phase 1)
    Bot/       Stub — AI bot ที่เรียก RPC เดียวกับผู้เล่นจริง (phase 2)
  ScriptableObjects/
    Monsters/ Towers/ Cards/   ที่เก็บ asset instance ของ SO ด้านบน
  Prefabs/
  Scenes/      มี SampleScene.unity จากเทมเพลตติดมา — สร้างเพิ่มเอง (Bootstrap, PvE, Lair, MainMenu) แล้วลบ SampleScene ทิ้งได้
Packages/manifest.json
ProjectSettings/ProjectVersion.txt
```

## Next Steps (Phase 1 — PvE)

- [ ] สร้าง Scene: `Bootstrap`, `PvE`, `Lair`, `MainMenu`
- [ ] วาง `NetworkManager` + `UnityTransport` + `GameBootstrap` ใน scene Bootstrap
- [ ] สร้าง ScriptableObject instance แรกใน `Assets/ScriptableObjects/Monsters` และ `Towers` เพื่อทดสอบ data pipeline
- [ ] ต่อ `DeploymentManager` เข้ากับ UI จริง (ตอนนี้ validate logic เป็น stub คืน true เสมอ)
- [ ] ทดสอบ local host mode (PvE) ก่อนแตะ dedicated server

รายละเอียด roadmap เต็มดู [technical-architecture.md](technical-architecture.md) หัวข้อ 12
