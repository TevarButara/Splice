# Splice C4D1 — Ops Foundation

วันที่: 2026-07-23
สถานะ: **C4D1A IMPLEMENTED / AUTOMATED ACCEPTANCE PASS**

## เป้าหมาย

ทำให้ backend local-first ตรวจสุขภาพเชิงระบบได้และจัดการ replay object ที่ไม่มี DB อ้างอิงอย่างปลอดภัย โดยยังไม่เสียค่า cloud service และไม่เปิดข้อมูลผู้เล่น

## สิ่งที่สร้างแล้ว

### 1. Protected operational status

`GET /internal/v1/ops/status` ต้องผ่าน trusted Raid Server identity และแสดง:

- FUNDED raids
- ALLOCATED / CLAIMED jobs
- expired worker leases
- ACTIVE raids ที่ค้างเกิน threshold
- unpublished outbox และอายุ event ที่เก่าที่สุด
- alerts: `EXPIRED_WORKER_LEASES`, `STUCK_ACTIVE_RAIDS`, `OUTBOX_BACKLOG`
- metric snapshot ของ process ปัจจุบัน

endpoint นี้ไม่รับ player bearer และไม่คืน player ID, raid ID, storage key หรือ secret.

### 2. Metrics + tracing boundary

- นับ request, HTTP 5xx และ duration
- แยก label เฉพาะ route group แบบ low-cardinality
- นับ replay blob write/read/failure
- นับ reconciled raids และ orphan blobs ที่ลบ
- ใส่ trace tag เฉพาะ route group/status
- ใช้ `System.Diagnostics.Metrics` จึงต่อ OpenTelemetry/Prometheus-compatible exporter ได้ภายหลังโดยไม่แก้ domain logic

ตัวเลขใน `/ops/status` เป็น snapshot ใน memory ของ instance เดียว ไม่ใช่ตัวแทน aggregate production cluster; production ต้องส่ง metrics ไป external collector.

### 3. Race-safe replay lifecycle

ขั้นตอนหนึ่งรอบ:

1. list เฉพาะ `.json.gz` ที่เก่ากว่า grace period
2. query `raid_replays` ใหม่สำหรับ object key แต่ละรายการ
3. เก็บไฟล์ไว้ทันทีหาก DB ยังอ้างอิง
4. ก่อนลบตรวจ length และ mtime ซ้ำ
5. หากไฟล์เปลี่ยนระหว่าง scan ให้ข้ามและรอรอบถัดไป
6. ลบ stale `.tmp` จาก write ที่ขาดตอน
7. จำกัด batch และ interval เพื่อไม่แย่ง I/O กับ raid traffic

grace period ขั้นต่ำในโค้ดคือ 60 วินาที; ค่าแนะนำ local/production เริ่มที่ 3,600 วินาที.

## Safety policy

- `ReplayStorage:MaintenanceEnabled` มีค่า default เป็น `false`
- เปิดได้เมื่อยืนยันว่า API database และ replay storage root/bucket เป็นคู่ environment เดียวกัน
- ห้ามแชร์ local root เดียวกันระหว่าง production, staging และ test database
- ห้ามตั้ง lifecycle bucket rule ให้ลบ object ก่อน application grace/reconciliation
- private object storage เท่านั้น; client ไม่มี direct key

## Configuration

| Key | Default | Guard |
|---|---:|---|
| `ReplayStorage:MaintenanceEnabled` | `false` | ต้อง opt-in |
| `ReplayStorage:OrphanGraceSeconds` | `3600` | ขั้นต่ำ 60 |
| `ReplayStorage:MaintenanceIntervalSeconds` | `900` | ขั้นต่ำ 60 |
| `ReplayStorage:MaintenanceBatchSize` | `250` | 1–1000 |
| `Ops:ActiveRaidWarningSeconds` | `1800` | ขั้นต่ำ 60 |
| `Ops:ExpiredLeaseWarningCount` | `0` | alert เมื่อมากกว่าค่า |
| `Ops:OutboxWarningCount` | `10000` | ขั้นต่ำ 1 |

ASP.NET Core environment variable ใช้ `__` แทน `:` เช่น `ReplayStorage__MaintenanceEnabled=true`.

## Automated acceptance

- .NET build: 0 errors, 0 warnings
- C2/C3/C4/C4C2F/C4C2G/C4D1A integration: PASS
- trusted ops auth / player denial: PASS
- stuck raid `DEGRADED` → reconciliation → `HEALTHY`: PASS
- referenced blob retained: PASS
- aged orphan and stale temp reclaimed: PASS
- blob changed after scan not deleted: PASS
- Unity process crash/reclaim/duplicate delivery/one settlement: PASS
- Unity EditMode 73/73, PlayMode 4/4
- Content Validator: 0 errors, 0 warnings
- local load: 240 requests, concurrency 16, failures 0, 981.13 req/s; p95 health 35.61 ms, wallet 69.87 ms, claim 86.98 ms

load นี้เป็น regression baseline บนเครื่อง local ไม่ใช่ production capacity guarantee.

## งานถัดไป

### C4D1B — Backup / restore drill

- script backup PostgreSQL + replay objects เป็นชุดเดียวที่ระบุ manifest/version
- restore เข้า database/root ใหม่
- verify ledger balance, immutable hashes, replay availability และ RPO/RTO
- corruption/missing-object drill ต้อง fail closed

### C4D1C — Container + external observability

- reproducible ASP.NET container, non-root user, health/readiness separation
- local compose สำหรับ API + PostgreSQL + private S3-compatible emulator โดยยังไม่เสียค่า cloud
- OpenTelemetry exporter, dashboard และ alert routing
- secret/workload identity boundary สำหรับ production

### ก่อน production

- private S3-compatible adapter + encryption/retention
- PostgreSQL PITR และ object versioning
- distributed load/soak/chaos test
- mTLS/workload identity แทน development key
