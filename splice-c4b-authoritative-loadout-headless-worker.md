# Splice C4B — Authoritative Loadout + Headless Raid Worker

อัปเดต: 2026-07-22
สถานะ: **Implemented / verified แบบ local-only**

## ผลลัพธ์

C4B ปิดช่องที่ `attackerLoadoutId` เดิมเป็นเพียง UUID จาก client โดยเพิ่ม server-validated loadout,
immutable loadout snapshot, trusted worker queue/lease และ Unity batchmode worker ที่ให้ผล deterministic
จากข้อมูลที่ server ตรึงไว้เท่านั้น

## Flow ปัจจุบัน

1. Unity ขอ selected army ผ่าน `SpliceServiceHub`
2. `PUT /v1/attacker-loadouts/{id}` ส่ง intent ของ faction/hero/card/count
3. ASP.NET ตรวจ card, faction, count, enabled/content version และคำนวณ `raid_power` จาก catalog
4. เมื่อสร้าง quote server copy loadout revision ปัจจุบันเป็น immutable snapshot
5. หลัง Fund + Allocate, trusted worker เรียก `/internal/v1/raid-jobs/claim`
6. server lock งานด้วย `FOR UPDATE SKIP LOCKED`, กำหนด worker ID และ lease แล้วคืน town/loadout snapshot IDs + power
7. Unity headless worker จำลองผลด้วย integer-only deterministic algorithm
8. worker owner เท่านั้นส่ง result ระหว่าง lease ได้; server คำนวณ payout และ settle escrow

## Security Rules

- player build เรียกได้เฉพาะ `/v1/*`; internal client แยก class และยอมรับเฉพาะ `/internal/v1/*`
- `SPLICE_RAID_SERVER_KEY` อ่านจาก environment ของ worker เท่านั้น ไม่อยู่ใน PlayerPrefs/scene/repository
- loadout/town snapshot แก้หรือ delete ไม่ได้ด้วย database trigger
- client ไม่ส่ง raid power, defender power, stake หรือ payout ที่ server เชื่อถือ
- worker result ต้องตรง `raid_server_id`, `worker_id`, allocation และ lease ที่ยังไม่หมดอายุ
- queue รองรับหลาย worker ด้วย row lock; lease ที่หมดอายุ reclaim ได้

## รัน Worker ในอนาคต

ใช้ Unity Player build เดียวกันใน batchmode โดยส่ง flags:

```text
-batchmode -nographics -spliceRaidWorker
```

environment ที่ต้องมี:

```text
SPLICE_RAID_SERVER_URL=http://127.0.0.1:8080
SPLICE_RAID_SERVER_ID=local-authoritative-raid-1
SPLICE_RAID_SERVER_KEY=<local-secret>
SPLICE_RAID_WORKER_ID=unity-worker-01
```

เพิ่ม `-spliceRaidWorkerOnce` สำหรับ claim/simulate/settle หนึ่งรอบแล้วปิด process เหมาะกับ smoke test และ autoscaling job

## Automated Verification

- PostgreSQL + HTTP C2/C3/C4B suite: PASS
- forged/unknown loadout: rejected
- server catalog power: verified
- immutable loadout snapshot: verified by trigger regression
- trusted worker claim + explicit empty queue: verified
- different worker stealing lease/result: rejected
- result replay/conflict/zero-sum settlement/recovery: PASS
- Unity EditMode: 48/48
- Unity PlayMode: 2/2
- Unity compile: 0 errors
- .NET build: 0 warnings, 0 errors
- Content Validator: 0 errors, 0 warnings

## ขอบเขตที่ยังไม่ใช่ Final Combat Simulation

worker รอบนี้เป็น **deterministic tactical proxy** จาก immutable attacker/defender power เพื่อพิสูจน์ authority,
queue, lease, crash-safe settlement และ replay ก่อน ยังไม่ได้เปิด scene แล้วจำลอง NavMesh/AI/projectile แบบ frame-by-frame

Hero ID ถูกบันทึกใน snapshot เพื่อรักษา identity แต่ยังไม่เพิ่ม power เพราะ Hero catalog/upgrade inventory ยังไม่เป็น
backend-authoritative การให้ client hero stats มีผลตอนนี้จะเปิดช่องโกง

## ขั้นถัดไปที่แนะนำ — C4C

1. ทำ Hero inventory/loadout/gear เป็น server authority และเพิ่ม hero power budget
2. เปลี่ยน proxy เป็น deterministic command-stream หรือ full physical headless scene runner
3. ให้ player Raid Arena poll/subscribe authoritative lifecycle และแสดง Pending/Completed/Replay
4. เพิ่ม end-to-end smoke ด้วย headless Player executable จริง
5. หลัง combat truth เสถียรจึงทำ autoscaling, metrics, queue broker และ production secrets
