# Splice C4C2C — Per-Unit Combat Authority

อัปเดต: 2026-07-23

## เป้าหมาย

ให้ผล raid มาจากข้อมูลที่ server เชื่อถือได้ราย actor ไม่ใช่ตัวเลข power รวมจาก client และยังคง replay ได้เหมือนกันทุกครั้งเมื่อ input, content version และ simulation version ตรงกัน

## Authority Boundary

### Attacker

1. Client ส่งเพียง loadout intent: Hero, gear instance IDs และ card/count
2. API ตรวจ ownership/content version และคำนวณ combat/power จาก server catalog
3. API materialize `army_items`, Hero และ Gear ลง immutable loadout snapshot ตอน quote
4. การแก้ loadout ภายหลังไม่เปลี่ยน raid ที่ fund แล้ว

### Defender

1. Client ส่ง town layout draft
2. API ตรวจตำแหน่ง, level และ content definition
3. ตอน deploy API materialize Tower/Garrison/Core เป็น `defenseUnits` ใน immutable town snapshot schema 2
4. snapshot schema เก่าถูกปฏิเสธตั้งแต่สร้าง quote ก่อน reserve/หัก stake

### Worker

- รับ input ที่ pre-materialized แล้ว ไม่ query inventory/content รายยูนิต
- เป็น stateless และ scale-out ตาม queue depth ได้
- ไม่มีสิทธิ์แก้ wallet โดยตรง; ส่ง immutable result กลับ trusted settlement endpoint
- result/replay ผูกกับ input hash, `contentVersion` และ `simulationVersion`

## Deterministic Kernel

- Fixed tick และ integer combat math
- stable sort ด้วย ring, position และ actor ID
- actor state: HP, armor, damage, cooldown, target, defeated
- หนึ่ง authoritative Core ต่อ snapshot
- `ATTACK`, `ABILITY`, `RING_BREACHED`, `DEFEATED`, `COMPLETE` เป็น command stream แบบ append-only
- ขีดจำกัดป้องกัน resource abuse:
  - attacker ไม่เกิน 50 units
  - defender ไม่เกิน 201 units
  - command stream ไม่เกิน 25,000 commands
  - cooldown ขั้นต่ำ 100 ms
  - actor ID ต้อง unique

## Performance และ Scale

- Materialize-on-write ทำให้ claim job อ่าน snapshot คงที่จำนวนไม่กี่แถวและไม่เกิด N+1 query
- API และ worker เป็น stateless จึงเพิ่ม replica ได้อิสระ
- PostgreSQL เป็น source of truth; queue/lease ป้องกัน worker สองตัวทำ raid เดียวพร้อมกัน
- จำกัด payload/command count เพื่อให้ CPU, memory และ network cost มี upper bound
- replay storage ระยะถัดไปควรเก็บ compressed stream/object storage และเก็บ metadata/hash/index ใน PostgreSQL

## Security และ Economy Safety

- ไม่เชื่อ client power, HP, payout, outcome หรือ balance
- validate count, aggregate power และ combat payload consistency ก่อน simulate
- allocation ticket ใช้ครั้งเดียวและ worker lease ผูกกับ worker ID
- settlement เป็น double-entry, idempotent และตรวจ immutable result identity
- legacy/stale target ถูก reject ก่อน stake ด้วย `TARGET_COMBAT_SNAPSHOT_STALE`

## Rollout

1. Apply migration 009
2. Publish content catalog `content-c4c2-v1`
3. Pause deployment ที่ snapshot schema ต่ำกว่า 2
4. ให้เจ้าของเมือง redeploy เพื่อสร้าง authoritative `defenseUnits`
5. เปิด worker `fixed-tick-c4c2c-v2`
6. Monitor stale rejection, worker duration, command count และ reconciliation

ห้ามเปิด worker version ใหม่ก่อน deployment เก่าถูก pause เพราะการปล่อยให้ raid เริ่มแล้วค่อย refund ทำให้ UX แย่และเพิ่มช่องทาง abuse

## Automated Verification

- deterministic replay/hash
- collection-order independence
- position-sensitive simulation
- forged army power rejection
- per-actor target/defeat
- Monster/Tower/Hero combat catalog export
- snapshot defense materialization
- stale snapshot pre-stake rejection
- full C2/C3/C4 backend integration
- Unity EditMode/PlayMode, Content Validator และ visual smoke

## Next — C4C2D

- client lifecycle polling/subscription: Pending → Active → Completed/Refunded
- persist/retrieve replay stream with hash verification
- worker crash/retry and duplicate-delivery regression
- headless executable end-to-end smoke
- performance/load-test budget สำหรับ API, PostgreSQL, queue และ worker concurrency
