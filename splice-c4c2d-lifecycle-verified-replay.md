# Splice C4C2D — Lifecycle Polling + Verified Replay

อัปเดต: 2026-07-23

## เป้าหมาย

ผู้เล่นเห็นสถานะ raid จาก source of truth และดู replay ที่ตรวจสอบได้ โดย Unity client ไม่มีสิทธิ์ส่ง outcome, payout หรือแก้ economy

## Flow

1. Player client อ่าน `GET /v1/raids/{raidId}` ด้วย bounded polling
2. Worker จำลองจาก immutable town/loadout snapshots
3. Worker ส่ง result + command stream ผ่าน trusted internal route
4. Backend ตรวจ schema/version/count/order/final command และ SHA-256
5. Result, replay และ ledger settlement commit ใน PostgreSQL transaction เดียว
6. Lifecycle เปลี่ยนเป็น `SETTLED` พร้อม replay metadata
7. Participant อ่าน `GET /v1/raids/{raidId}/replay`
8. Unity ตรวจ raid/result/version/hash ซ้ำก่อน presentation

## Security Invariants

- player API อ่าน lifecycle/replay ได้เท่านั้น
- result route ต้องใช้ Raid Server identity + active worker lease/ticket
- command stream ผิด hash ถูกปฏิเสธก่อน settlement
- final `COMPLETE` ต้องตรง outcome/breached rings
- replay ไม่เกิน 25,000 commands และ tick ไม่เกิน 36,000
- result/replay เป็น immutable และ one-to-one กับ raid
- raw allocation ticket ไม่ถูกเก็บใน result/replay
- attacker หรือเจ้าของเมืองเท่านั้นที่อ่านข้อมูลได้

## Performance / Scale

- lifecycle query อ่าน metadata ขนาดเล็ก ไม่ส่ง command streamทุก poll
- polling ใช้ exponential backoff สูงสุด 4 วินาทีและ reset เร็วเมื่อ raid Active
- replay input reconstruct จาก immutable snapshots จึงไม่เก็บข้อมูลซ้ำและลด inconsistency
- API/worker stateless เพิ่ม replica ตาม traffic/queue depth ได้
- Prototype เก็บ command stream JSONB เพื่อพัฒนาเร็วและ transactional
- Production scale: ใช้ object storage สำหรับ compressed stream; PostgreSQL เก็บ result ID, URI, byte size, hash, simulation version และ retention metadata

## Failure Model

- network ชั่วคราว: retry เฉพาะ error ที่ประกาศ retryable
- timeout: HUD แสดง replay unavailable แต่ไม่แก้ outcome/economy
- `REFUNDED/CANCELLED/FAILED`: หยุด polling และไม่ขอ replay
- settled แต่ไม่มี replay: fail closed
- metadata/hash mismatch: ไม่เล่น replay
- duplicate result: คืนผลเดิมเฉพาะ payload/hash เดิม; payload ต่างถูก conflict

## Verification

- Backend compile 0 warnings/errors
- C2/C3/C4 integration ผ่าน
- tamper, wallet-no-mutation, immutable replay และ idempotent result regression ผ่าน
- Unity EditMode 66/66 และ PlayMode 4/4
- Content Validator 0 errors/warnings
- RaidArena lifecycle HUD visual smoke ผ่าน

## Next

C4C2E จะเพิ่ม headless process E2E, worker crash/retry/duplicate delivery, performance budget และ load-test harness ก่อนออกแบบ production autoscaling
