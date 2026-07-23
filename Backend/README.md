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

## C4D1A: Operations telemetry + replay lifecycle

- `GET /internal/v1/ops/status` ใช้ trusted Raid Server headers เท่านั้น และคืน queue depth, expired lease, stuck active raid, unpublished outbox, alerts และ in-process metric snapshot
- request telemetry ใช้ route group แบบ low-cardinality (`health`, `player`, `internal`, `other`) ไม่ใส่ player/raid ID ใน metric labels
- replay write/read/failure, reconciliation และ orphan deletion ถูกนับไว้ พร้อม `System.Diagnostics.Metrics` boundary สำหรับต่อ OpenTelemetry exporter ภายหลัง
- orphan cleanup ตรวจว่า object เก่ากว่า grace period, query DB pointer ใหม่ก่อนลบ และตรวจ size/mtime ซ้ำเพื่อไม่ลบไฟล์ที่เปลี่ยนระหว่าง scan
- maintenance ปิดโดยค่าเริ่มต้นเพื่อไม่ให้ process ที่ชี้ test DB กวาด storage ของ DB อื่น ต้องเปิดอย่างตั้งใจหลังยืนยันว่า DB กับ storage root เป็นคู่เดียวกัน

ตัวอย่าง config แบบ environment variables:

```bash
ReplayStorage__LocalRoot=/absolute/private/replay-root
ReplayStorage__MaintenanceEnabled=true
ReplayStorage__OrphanGraceSeconds=3600
ReplayStorage__MaintenanceIntervalSeconds=900
ReplayStorage__MaintenanceBatchSize=250
Ops__ActiveRaidWarningSeconds=1800
Ops__ExpiredLeaseWarningCount=0
Ops__OutboxWarningCount=10000
```

ก่อนเปิด maintenance ใน production ต้องมี backup/restore drill และเปลี่ยน local adapter เป็น private S3-compatible implementation ที่ใช้ workload identity. รายละเอียดอยู่ที่ `splice-c4d1-ops-foundation.md`.

## C4D1B: Consistent backup / restore drill

backup ใช้ PostgreSQL exported snapshot เดียวกันสำหรับ database dump, table fingerprints และ replay pointer inventory แล้วคัดลอกเฉพาะ immutable blob ที่ snapshot อ้างอิง:

```bash
bash Backend/database/scripts/backup-c4d1.sh \
  splice_local /absolute/private/replay-root /absolute/new/backup-bundle

bash Backend/database/scripts/restore-c4d1.sh \
  /absolute/backup-bundle splice_restored /absolute/new/restored-replays

bash Backend/database/scripts/verify-c4d1-restore.sh \
  splice_restored /absolute/new/restored-replays /absolute/backup-bundle
```

ข้อกำหนดด้านความปลอดภัย:

- bundle, target database และ target replay root ต้องยังไม่มีอยู่; script ไม่ overwrite
- bundle สร้างด้วย permission `0700` และ `umask 077` เพราะ dump อาจมีข้อมูลบัญชี
- object key, symlink, byte length, SHA-256, gzip, DB pointer inventory และ table fingerprints ถูกตรวจ
- restore ใช้ `pg_restore --single-transaction`; corruption/missing object ถูกปฏิเสธก่อนสร้าง target
- local-first script ปฏิเสธ `s3-compatible` rows จนกว่าจะมี adapter ที่ดึง object/version จาก private bucket ได้

รัน automated drill:

```bash
bash Backend/database/scripts/test-c4d1-backup-restore.sh
```

นี่เป็น logical full-backup proof ไม่ใช่ production PITR. RPO จริงขึ้นกับรอบ backup; production ยังต้องมี encrypted/signed off-site backup, PostgreSQL WAL/PITR, object versioning และ scheduled restore drill.

## C4D1C: Container + external observability

API แยก probe ตามหน้าที่แล้ว:

- `GET /health/live` ตรวจว่า process ตอบสนอง โดยไม่แตะ dependency
- `GET /health/ready` ตรวจ PostgreSQL และทดลองเขียน replay storage จริง
- `GET /health` เป็น readiness alias เพื่อไม่ทำให้ client/load test เดิมพัง
- `GET /metrics` เป็น Prometheus text format และต้องใช้ bearer token แยกที่ `Ops:MetricsBearerToken` (ขั้นต่ำ 24 ตัวอักษร)

local stack ไม่มีค่า cloud และ bind เฉพาะ loopback:

```bash
cd Backend
docker compose -f compose.local-observability.yml up --build -d
```

- API: `http://127.0.0.1:5080`
- Prometheus: `http://127.0.0.1:9090`
- Alertmanager: `http://127.0.0.1:9093`
- PostgreSQL อยู่ใน internal Docker network และไม่ publish port

Compose นี้เป็น Development fixture เท่านั้น ใช้ credential ที่ระบุชัดว่า local-only, ปิด replay cleanup และไม่ส่ง alert ออกภายนอก. ห้ามนำ credential/config นี้ไป production.

รัน config/security regression:

```bash
bash Backend/database/scripts/test-c4d1c-container.sh
```

เมื่อ Docker Desktop ทำงานและต้องการ build/start/scrape proof เต็มชุด:

```bash
SPLICE_CONTAINER_E2E=1 bash Backend/database/scripts/test-c4d1c-container.sh
```

ทุก base/service image pin ทั้ง version และ manifest digest. API image เป็น multi-stage .NET 10, runtime ทำงานด้วย non-root UID, filesystem เป็น read-only ใน Compose, drop Linux capabilities และเปิด writable path เฉพาะ `/tmp` กับ replay volume. Prometheus/Alertmanager เก็บข้อมูลใน local volumes; ลบ stack และ volumes ด้วย:

```bash
docker compose -f Backend/compose.local-observability.yml down -v
```

ก่อน production ต้องเปลี่ยน development bearer/key เป็น workload identity หรือ mTLS, inject secret จาก secret manager, ใช้ private object storage, TLS ingress, image digest/signing/scanning และ alert receiver จริง.
