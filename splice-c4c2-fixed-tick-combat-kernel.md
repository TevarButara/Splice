# Splice C4C2A — Fixed-Tick Combat Kernel

อัปเดต: 2026-07-22
สถานะ: **Implemented / verified แบบ local-only**

## ผลลัพธ์

Headless Raid Worker ไม่ตัดสินผลจากอัตราส่วน attacker/defender power แบบครั้งเดียวแล้ว แต่จำลองการบุก
ด้วย fixed tick 100 ms ตั้งแต่ spawn, เคลื่อนที่, ปะทะแนวป้องกัน, Hero ability, ฝ่ายรับสวนกลับ,
breach 3 ชั้น จนถึง Full Victory, Extraction หรือ Defeat

Kernel เป็น combat truth ที่ไม่พึ่ง frame rate, Physics, Animator หรือ VFX จึง replay ข้าม headless worker
ได้เหมือนกัน ส่วน RaidArena ใน C4C2B จะอ่าน command stream ไปแสดงภาพ โดยไม่มีสิทธิ์แก้ผล

## Immutable Input

- raid, target snapshot และ loadout snapshot UUID
- Army/Hero/Gear/Defender power breakdown ที่ server ตรวจแล้ว
- Hero identity, level และ combat payload
- Gear instance UUID และ scaled power
- Army card/count
- immutable town layout พร้อมตำแหน่ง Tower/Garrison และ upgrade levels
- maximum tick budget

ก่อนเริ่ม kernel จะ reject identity ผิดรูปแบบ, Hero combat ไม่ครบ, army ว่าง และ
`attackerPower != armyPower + heroPower + gearPower`

## Simulation Contract

```text
SimulationVersion = fixed-tick-c4c2a-v1
Tick              = 100 ms
DefaultTimeout    = 1,800 ticks / 180 seconds
DefenseObjective  = ring-1 -> ring-2 -> ring-3
```

ตำแหน่ง Tower/Garrison ถูก quantize เป็นหน่วย milli และจัดลงแนวป้องกันแบบ deterministic
วัตถุที่อยู่ไกลศูนย์กลางกว่าจะอยู่แนวนอกก่อน จากนั้นกระจาย defense health ตามจำนวน/upgrade ของแต่ละแนว

คำสั่งที่เกิดใน stream รอบนี้:

- `SPAWN`
- `MOVE`
- `ENGAGE`
- `ATTACK`
- `ABILITY`
- `BREACH`
- `COMPLETE`

ทุก command มี tick, actor, target และ integer value; hash ของ stream รวมกับ canonical immutable input
เพื่อสร้าง final simulation hash

## Determinism Rules

- ไม่มี mutable/random RNG
- ไม่มี `Time.deltaTime`, Rigidbody, NavMesh หรือ floating-point combat calculation
- position float จาก snapshot ใช้เฉพาะตอน quantize เป็น integer milli
- Army/Gear/Tower/Garrison canonicalize order ก่อน hash
- UI, animation, camera และ VFX อ่านผลได้อย่างเดียว
- เปลี่ยน input, position, version หรือ command ใดๆ ต้องทำให้ hash เปลี่ยน

## Worker Flow

1. trusted worker claim immutable job
2. deserialize town layout + loadout + Hero/Gear payload
3. แปลงเป็น `FixedTickRaidSimulationInput`
4. จำลองจน Complete หรือ timeout
5. ส่งเฉพาะ authoritative outcome, breached rings, duration และ simulation hash ไป settlement API
6. log tick/command count และ command-stream hash สำหรับตรวจสอบ

## Automated Verification

- deterministic result/command hash: PASS
- collection reorder ให้ผลเท่าเดิม: PASS
- forged power breakdown: rejected
- immutable town position เปลี่ยน: simulation hash เปลี่ยน
- worker JSON รับ Hero/Gear/Town payload ครบ: PASS
- Unity EditMode: 54/54 passed
- Unity PlayMode: 2/2 passed
- Unity compile: 0 errors
- Content Validator: 0 errors, 0 warnings
- C2/C3/C4 PostgreSQL + HTTP integration: PASS

## ข้อจำกัดปัจจุบัน

- Hero ใช้ authoritative combat stats จริง
- Army และฝ่ายรับยังใช้ aggregate authoritative power กระจายเป็น damage/health; ยังไม่ได้ export combat payload
  ของ Monster/Tower ราย definition
- command stream ยังอยู่ใน memory ของ worker และ hash เท่านั้น ยังไม่ได้ persist เพื่อดาวน์โหลด replay
- ยังไม่มี GameObject presentation adapter จึงยังไม่เห็นการจำลองนี้ใน RaidArena

## ขั้นต่อไป — C4C2B

1. สร้าง RaidArena presentation adapter ที่ spawn proxy Hero/Army/Defense ตาม immutable input
2. เล่น `MOVE/ATTACK/ABILITY/BREACH/COMPLETE` ตาม tick โดยรองรับเร่งความเร็วและ replay
3. ยืนยันว่าปิด VFX/animation แล้ว outcome/hash ไม่เปลี่ยน
4. เพิ่ม PlayMode visual smoke test และภาพ screenshot จากมุม Attacker/Defender
5. หลังเห็นภาพครบ เพิ่ม per-unit Monster/Tower combat payload และ replay persistence
