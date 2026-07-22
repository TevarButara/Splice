# Splice C4C1 — Authoritative Hero + Gear

อัปเดต: 2026-07-22
สถานะ: **Implemented / verified แบบ local-only**

## เป้าหมาย

C4C1 ทำให้ Hero และ Gear เป็นส่วนหนึ่งของ raid loadout ที่ server เชื่อถือได้จริง ก่อนเปลี่ยน worker
จาก deterministic power proxy ไปเป็น physical headless combat ใน C4C2 โดย client ส่งได้เฉพาะ “สิ่งที่เลือก”
แต่ไม่สามารถกำหนด ownership, stats, level หรือ power เองได้

## Authority Flow

1. Unity ส่ง army entries, `heroId` และรายการ Gear instance UUID
2. API ตรวจ army จาก content catalog และตรวจ Hero/Gear จาก inventory ของ player
3. server โหลด content version, base power และ combat payload จาก PostgreSQL
4. server คำนวณ power ตาม level และบันทึก mutable loadout revision
5. ตอนออก raid quote ระบบ copy army, Hero, Gear และ power breakdown ลง immutable snapshot
6. trusted worker claim งานจาก snapshot เท่านั้น จึงไม่เปลี่ยนแม้ผู้เล่นแก้ loadout ภายหลัง

## Database Contract

- `content_definitions` รองรับ `HERO`, `GEAR` และ `combat_payload jsonb`
- `player_heroes`: ownership ต่อ player/content พร้อม level และ XP
- `player_gear_items`: Gear instance UUID, owner, definition และ level
- `attacker_loadouts` เก็บ `army_power`, `hero_power`, `gear_power`; `raid_power` เป็น generated sum
- `attacker_loadout_snapshots` เก็บ breakdown เดียวกัน พร้อม immutable `hero_payload` และ `gear_items`
- Hero ใช้ canonical ID รูปแบบ `hero/<heroId>`; Gear ใช้ UUID เพื่อแยกแต่ละชิ้นอย่างปลอดภัย

## Power Contract ปัจจุบัน

ค่าทั้งหมดเป็น integer เพื่อให้ผลตรงกันระหว่าง Unity, API และ worker:

```text
HeroPower = MaxHealth / 20
          + Armor * 2
          + AttackDamage * 1000 / AttackCooldownMs
          + AbilityDamage / 5

RaidPower = ArmyPower + HeroPower + GearPower
HeroLevelPower = BasePower * (100 + (Level - 1) * 5) / 100
GearLevelPower = BasePower * (100 + (Level - 1) * 10) / 100
```

fixture ปัจจุบัน:

- Army `2 × 1/1` = 130
- Hero `hero/hero_test` = 2,830
- Gear test instance = 200
- รวม = 3,160

สูตรนี้เป็น authority/security baseline ไม่ใช่ final balance; การปรับ balance ต้องเปลี่ยน content version และเก็บ
snapshot เดิมให้ replay ได้เสมอ

## Security Rules

- reject Hero/Gear ที่ไม่มี ownership หรือปิดใช้งาน
- reject content version ที่ไม่ตรงกับ server
- ไม่รับ stats, power, level, stake หรือ payout จาก player client
- Gear ใช้ instance UUID และตรวจ owner ทุกชิ้น; ID ซ้ำใน request ไม่เพิ่ม power
- quote และ worker job อ้าง immutable snapshot ไม่อ่าน inventory/loadout ปัจจุบัน
- internal worker route ใช้ trusted server identity แยกจาก player bearer token

## Unity / Live Content

- `HeroDefinitionSO` ถูก export เป็น authoritative `HERO` พร้อม health, armor, attack, movement และ ability payload
- generated JSON และ PostgreSQL seed ใช้ `content-c4c1-v1`
- Content Validator ทำให้ catalog ที่ขาดหรือ stale เป็น release gate
- Unity contracts รองรับ `gearInstanceIds` และแสดง power breakdown จาก server
- รอบนี้ยังไม่สร้าง Gear art/UI หรือ Gear definition จริง; schema/API รองรับไว้แล้วและ empty Gear loadout ใช้งานได้

## Automated Verification

- C2/C3/C4 PostgreSQL + HTTP integration: PASS
- Hero not owned / Gear not owned: rejected
- immutable Hero combat + Gear instance payload: verified at worker claim
- mutable loadout changed after quote: worker still receives power 3,160
- immutable snapshot database trigger: PASS
- .NET build/format: 0 errors, 0 warnings
- Unity EditMode: 49/49 passed
- Unity worker Hero/Gear deserialize targeted regression: 1/1 passed (suite รวม 50 tests)
- Unity PlayMode: 2/2 passed
- Content Validator: Errors 0, Warnings 0

หมายเหตุ: Unity MCP wrapper timeout ตอนเริ่ม EditMode แต่ Unity Test Runner ทำงานเสร็จและบันทึก
`TestResults.xml` เป็น Passed 49/49; PlayMode job ผ่านตรงผ่าน MCP 2/2

## ขั้นถัดไป — C4C2

1. สร้าง physical headless scene runner จาก immutable town/loadout payload
2. กำหนด deterministic tick, command stream, RNG seed และ simulation hash
3. แยก simulation truth ออกจาก visual interpolation/VFX
4. เพิ่ม timeout/crash/reclaim regression และ executable end-to-end smoke
5. หลัง simulation เสถียร จึงทำ player Pending/Completed/Replay และ Gear progression UI
