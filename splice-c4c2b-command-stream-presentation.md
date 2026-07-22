# Splice C4C2B — Authoritative Command-Stream Presentation

## เป้าหมาย

ทำให้ผลจาก C4C2A มองเห็นได้จริงใน `RaidArena` โดย client เป็นผู้แสดงผลเท่านั้น ความจริงของการรบยังอยู่ที่ fixed-tick worker และ immutable command stream

## Contract

1. รับเฉพาะ `simulationVersion` ที่รองรับ, stream เรียง tick, มี `COMPLETE` และ SHA-256 hash ที่ถูกรูปแบบ
2. presentation ทำได้เฉพาะ interpolate การเคลื่อนที่, spawn visual proxy, pulse, ซ่อน breached ring และแสดง HUD
3. presentation ห้ามเขียน raid result, wallet, escrow, loot หรือ settlement
4. content prefab เป็น decoration; actor root และ authority marker ต้องอยู่รอดแม้ network component ของ prefab ปิดตัว
5. anchor ใช้ Spawn/Core ของ scene จริง แล้วฉาย ring ลงในช่วง lane ที่ `DefenderCamera` มองเห็น

## Scene behavior

- Root: `[Authoritative Raid Replay]` ใน `RaidArena`
- Auto demo: Editor/development build เท่านั้น, 1.5×
- มุม: Defender presentation ที่อนุมัติไว้
- Legacy role picker ถูกปิดเมื่อเข้า replay
- `IncomingRaidScenarioController` ยังเรียกผ่าน Context Menu ได้ แต่ไม่ auto-run แข่งกับ authoritative replay

## Command mapping

| Command | Presentation |
|---|---|
| `SPAWN` | สร้าง Hero + formation |
| `MOVE` | interpolate formation ไป ring ถัดไป |
| `ENGAGE` | HUD + ring pulse |
| `ATTACK` | attacker/defender pulse |
| `ABILITY` | Hero ability pulse + damage text |
| `BREACH` | ซ่อนกำแพง ring ที่แตก |
| `COMPLETE` | แสดง outcome + command-stream hash |

## Verification gate

- Stream validation/forgery regression
- Scene มี authoritative replay เพียงตัวเดียว และ legacy incoming demo ไม่ auto-run
- Role picker ไม่บัง replay
- Actor root ยังอยู่จน `COMPLETE`
- Anchor เรียงอยู่ระหว่าง Spawn/Core
- Full EditMode, PlayMode และ Content Validator ต้องผ่าน

## งานถัดไป

C4C2C ควรแทน aggregate army/defender power ด้วย immutable per-unit Monster/Tower payload และออก command ที่ระบุ actor/target/HP/cooldown รายตัว โดย presentation ตัวนี้ยังใช้ contract เดิมได้ผ่าน simulation-version adapter
