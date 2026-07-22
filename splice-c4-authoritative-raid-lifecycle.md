# Splice — C4 Authoritative Raid Lifecycle

อัปเดต: 2026-07-22

## สถานะ

C4A implemented และ verified แบบ local-only. ยังไม่มี Cloud/server bill.

## Flow

1. Player ยืนยัน quote: backend หัก attacker stake และ reserve เงินที่เมืองรับอาจเสียเข้า raid escrow
2. Player ขอ allocation: backend ออก allocation ID + ticket อายุสั้น
3. Trusted Raid Server claim ticket ผ่าน internal route แล้วรับ immutable target snapshot และ attacker loadout ID
4. Trusted Raid Server ส่ง outcome/rings/duration/simulation hash
5. Backend คำนวณ payout, post ledger, บันทึก immutable result และปิด raid
6. Player อ่าน lifecycle/result ได้ แต่ส่งผลหรือจำนวนรางวัลเองไม่ได้

## Zero-sum FAIR Example

| ผล | ผู้บุกจ่าย | เมือง reserve | ผู้บุกได้รับ | เมืองรับคืน | เมืองสุทธิ |
|---|---:|---:|---:|---:|---:|
| Full Victory | 100 | 80 | 180 | 0 | -80 |
| Core Extraction | 100 | 80 | 120 | 60 | -20 |
| Inner Extraction | 100 | 80 | 90 | 90 | +10 |
| Outer Extraction | 100 | 80 | 60 | 120 | +40 |
| Defeat | 100 | 80 | 0 | 180 | +100 |

เงินทุกหน่วยมี backing ก่อน Raid เริ่ม ไม่มีการเชื่อ payout ที่ client ส่ง และไม่มีการ mint จาก settlement.

## Security Boundary

- `/v1/...` ใช้ player identity และเข้าถึง Allocate/Status เท่านั้น
- `/internal/v1/.../start|result` ใช้ trusted Raid Server identity
- Unity player API client block `/internal/` ตั้งแต่ transport policy
- allocation table เก็บ ticket เป็น SHA-256, มี expiry และผูก raid/allocation/server; raw ticket อยู่ใน idempotency response ชั่วคราวเพื่อ replay เท่านั้น
- result หนึ่งรายการต่อ raid, immutable ที่ DB และ idempotent ทุก retry

Development key ปัจจุบันมีไว้เฉพาะ loopback. Production ต้องใช้ mTLS/workload identity, secret manager, network policy และลด/เข้ารหัส sensitive idempotency retention; ห้ามใส่ trusted credential ใน game client.

## Recovery

- FUNDED ที่ไม่เคย start: คืน attacker stake + defender reserve
- ACTIVE ที่ worker หายเกิน timeout: infrastructure refund และปิด allocation
- settlement/result replay: คืนผลเดิมโดยไม่ post ledger ซ้ำ

## C4B ที่ยังต้องทำ

- headless Unity Raid Worker หรือ deterministic simulator ที่ claim allocation จริง
- server-side immutable attacker army/loadout snapshot
- worker heartbeat/job queue และ replay artifact
- player UI poll/subscribe lifecycle จน server ยืนยันผล
- load test, secret rotation, monitoring และ production deployment
