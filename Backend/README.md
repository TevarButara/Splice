# Splice Backend (local-first)

ฐานข้อมูล backend ของ Splice แยกจาก Unity project เพื่อไม่ให้ Unity import server assets.

## C1: PostgreSQL ledger

ข้อกำหนด: PostgreSQL 16 และคำสั่ง `psql`, `createdb`, `dropdb`.

รันทดสอบทั้งหมดแบบ local-only:

```bash
bash Backend/database/scripts/test-c1.sh
bash Backend/database/scripts/test-c2.sh
bash Backend/database/scripts/test-c3.sh
```

สคริปต์ใช้ฐานข้อมูลชั่วคราว `splice_c1_test` / `splice_c2_test` เท่านั้น ลบเมื่อจบ และไม่เชื่อมต่อ cloud.

## C2: ASP.NET Core local API

- SDK ถูก pin ที่ .NET 10 LTS ใน `global.json`
- API อยู่ที่ `src/Splice.Backend.Api`
- C2 เปิดได้เฉพาะ `Development`; local auth ใช้ `Authorization: Bearer dev:<player-uuid>`
- ห้ามใช้ development bearer mode ใน production
- ตั้ง connection string ผ่าน `ConnectionStrings__Splice`; ไม่ commit password/secret
- `GET /health` ตรวจ PostgreSQL จริง
- reconciliation worker คืน FUNDED escrow ที่ไม่เคย start โดยอัตโนมัติ; ปรับค่าได้ผ่าน `Reconciliation:*`

ตัวอย่างรัน local หลัง apply migrations:

```bash
cd Backend
ConnectionStrings__Splice='Host=127.0.0.1;Database=splice;Username=YOUR_USER' \
  dotnet run --project src/Splice.Backend.Api
```

## C3: Immutable Town API

- `PUT /v1/towns/{factionId}/draft` เก็บ checked-out draft แบบ versioned
- deployment route ทำ server checkout, vault/escrow funding, immutable commit และ snapshot แบบ atomic
- latest/batch/by-ID routes รองรับ own town, target pool และ active raid ที่ล็อก snapshot เก่า
- `content_definitions` เป็น authority ของ ID, cost และ Defense Capacity; หากยังไม่ seed จะ fail closed
- `test-c3.sh` ครอบคลุม rollback, immutable history และ concurrent deployment

ไฟล์ migration ต้องถูกรันตามลำดับชื่อ ห้ามแก้ migration ที่ deploy แล้ว; ให้เพิ่มไฟล์เลขถัดไปแทน.

## Unity local integration (C2/C3)

เริ่ม API + PostgreSQL fixture สำหรับ Unity แบบ local-only ด้วยคำสั่งเดียวจาก root repository:

```bash
bash Tools/run-local-backend-dev.sh <unity-player-uuid>
```

จากนั้นใน Unity เลือก `Splice > Backend > Enable Local Remote Meta` เพื่อให้ Meta flow ใช้ API ที่ `http://127.0.0.1:5080`. UUID ที่ส่งให้ launcher ต้องตรงกับ Player ID ที่ Unity พิมพ์ใน Console. โหมดนี้เป็น development bearer และถูกปิดโดยค่าเริ่มต้น; ใช้ production build ไม่ได้.

สคริปต์จะสร้างฐานข้อมูลชั่วคราว `splice_unity_local_dev`, apply migration/seed, เติม wallet และสร้าง defender deployment fixture. กด `Ctrl+C` เพื่อหยุด API และลบฐานข้อมูลชั่วคราว.

## C4A: Authoritative Raid Lifecycle

- player route ทำได้เฉพาะ Fund, Allocate และอ่าน lifecycle
- trusted routes `/internal/v1/raids/{id}/start|result` ต้องมี `X-Raid-Server-Key` และ `X-Raid-Server-Id`
- result payout คำนวณที่ backend จาก immutable quote; client ส่งจำนวนเงินไม่ได้
- attacker stake และ defender reserve ถูกย้ายเข้า raid escrow ตอน Fund จึงไม่มีรางวัลลอยและ settlement เป็น zero-sum
- result เป็น immutable row; duplicate/retry ไม่จ่ายซ้ำ และ timed-out ACTIVE raid ถูก infrastructure refund

ค่า `RaidServer:DevelopmentKey` ใน launcher ใช้เฉพาะ local Development. Production ต้องเปลี่ยนเป็น workload identity/mTLS และห้ามฝังคีย์ใน Unity player.

## C4C2E: Worker process / crash recovery

ใน Unity เลือก `Splice > Backend > Build C4C2E Headless Worker` เพื่อสร้าง Development worker ใน temporary directory จากนั้นรัน proof แบบข้าม process:

```bash
bash Backend/database/scripts/test-c4c2e-process.sh \
  "/absolute/path/SpliceRaidWorkerC4C2E.app/Contents/MacOS/Splice"
```

proof จะเปิด API จริงบน loopback, เรียก Unity executable ด้วย `-batchmode -nographics`, ทำให้ worker แรกล่มหลัง claim, หมด lease, ให้ worker ใหม่ reclaim แล้วส่ง result ซ้ำ โดยต้องเกิด settlement เพียงครั้งเดียว สคริปต์ใช้ฐานข้อมูล `splice_c2_test` ชั่วคราวและลบเมื่อจบ.

macOS Dedicated Server module เป็น optimization สำหรับ production image และยังไม่จำเป็นต่อ local proof นี้; หากติดตั้ง module แล้วจึงเปลี่ยน build subtarget เป็น Server.

## C4C2E: Local load budget

```bash
bash Backend/database/scripts/test-c4c2e-load.sh
```

ค่าเริ่มต้นคือ 240 requests ที่ concurrency 16 ครอบคลุม health, authenticated wallet และ empty worker claim พร้อมรายงาน p50/p95/p99/RPS. Harness ปฏิเสธ non-loopback URL โดยค่าเริ่มต้น; การยิง environment ภายนอกต้องตั้ง `SPLICE_LOAD_ALLOW_REMOTE=true` อย่างตั้งใจเท่านั้น.

budget ปัจจุบัน: health p95 ≤ 200 ms, wallet p95 ≤ 300 ms, claim p95 ≤ 600 ms, failures = 0 และ throughput ≥ 20 req/s. นี่เป็น local regression baseline ไม่ใช่ production capacity guarantee.

## C4C2G: Replay object storage

- replay ใหม่เก็บ command stream เป็น immutable gzip blob; PostgreSQL เก็บเฉพาะ provider, object key, blob SHA-256, size, encoding และ canonical command-stream hash
- local Development ใช้ `LocalFileRaidReplayBlobStore`; กำหนด root ผ่าน `ReplayStorage__LocalRoot` ได้ โดยค่าเริ่มต้นอยู่ใต้ temporary directory
- API ตรวจ blob length/SHA-256, gzip bound, JSON command count และ canonical command hash ก่อนส่งให้ Unity
- migration `012` เป็น dual-read: replay รุ่นเก่าที่อยู่ใน PostgreSQL JSONB ยังเปิดได้ ส่วน record ใหม่ห้ามเก็บ command stream ซ้ำในฐานข้อมูล
- object key สร้างจาก server UUID/hash เท่านั้นและถูกตรวจ path traversal; player client ไม่ได้รับ storage key หรือ direct object access
- production ให้ bind `IRaidReplayBlobStore` กับ private S3-compatible bucket, workload identity, encryption/retention policy และ CDN/API authorization ตาม environment

blob ถูก stage ก่อน financial transaction เพื่อไม่ถือ ledger locks ระหว่าง object I/O; หาก process ตายหลัง stage แต่ก่อน commit อาจมี orphan ที่ไม่มี metadata อ้างอิง จึงต้องให้ Ops lifecycle job ลบ object ที่เก่ากว่า grace period และไม่พบใน `raid_replays`. ห้ามลบทันทีระหว่าง concurrent retry เพราะ object เดียวกันอาจถูก transaction อื่น commit สำเร็จ.
