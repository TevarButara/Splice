# Splice C4C2E — Worker Reliability และ Load Proof

วันที่: 2026-07-23
สถานะ: เสร็จและผ่าน automated verification แบบ local-only

## เป้าหมาย

พิสูจน์ว่า authoritative raid worker ทำงานเป็น process จริง, ฟื้นจากการล่มได้, รับ duplicate delivery ได้โดยไม่จ่ายซ้ำ และมี performance baseline ที่รันซ้ำได้ก่อนนำระบบขึ้น server จริง

## Runtime flow

1. Player fund raid และ backend สร้าง immutable allocation
2. Unity worker claim งานด้วย lease และ heartbeat
3. worker จำลอง fixed-tick จาก immutable town/loadout/Hero/Gear เท่านั้น
4. result ID และ idempotency key derive แบบ deterministic จาก `raidId`
5. backend ตรวจ command stream/hash แล้วจึง settle double-entry ledger
6. หาก worker ล่ม งาน `CLAIMED` ที่ lease หมดถูก worker อื่น reclaim ได้
7. retry หรือ duplicate submit ของผลเดียวกันคืนผลเดิมและไม่สร้าง payout ซ้ำ

## Reliability contract

- HTTP retry สูงสุด 4 attempts พร้อม backoff 250/500/1,000/2,000 ms
- retry เฉพาะ timeout, 408, 425, 429 และ 5xx
- ไม่ retry authority/schema/conflict ที่ต้องแก้ input หรือ ownership
- heartbeat ต่อ lease ระหว่าง simulation
- queue claim และ heartbeat ใช้ `READ COMMITTED + FOR UPDATE SKIP LOCKED`
- wallet/escrow/settlement ยังคง `SERIALIZABLE` พร้อม bounded jittered retry
- safe rollback ไม่ซ่อน PostgreSQL error เดิมเมื่อ transaction จบไปแล้ว
- log ใช้ request/path/SQLSTATE แบบ structured; ไม่บันทึก key, bearer หรือ request body

## Process E2E ที่พิสูจน์แล้ว

- build macOS Development Player จาก scene `Bootstrap`
- รัน executable ด้วย `-batchmode -nographics`
- worker A claim แล้ว crash injection ด้วย exit code 77
- raid คง `ACTIVE`, ไม่มี partial result และ claim attempt = 1
- test ทำให้ lease หมด แล้ว worker B reclaim เป็น attempt = 2
- worker B submit result ซ้ำสองครั้งด้วย identity เดิม
- ผลสุดท้ายเป็น `SETTLED` และมี result, replay, funding, settlement อย่างละหนึ่งชุด
- ledger ยัง balanced และไม่มี open lifecycle state

Development crash/duplicate switches ถูก compile guard ด้วย `UNITY_EDITOR || DEVELOPMENT_BUILD`; production build ใช้ injection นี้ไม่ได้

## Load harness

คำสั่ง:

```bash
bash Backend/database/scripts/test-c4c2e-load.sh
```

default workload: 240 requests, concurrency 16, ผสม `/health`, `/v1/wallet` และ empty `/internal/v1/raid-jobs/claim`.

ผล baseline ล่าสุด:

| Metric | ผล |
|---|---:|
| Failures | 0 |
| Throughput | 2,975.43 req/s |
| Health p95 | 8.84 ms |
| Wallet p95 | 14.47 ms |
| Empty claim p95 | 35.79 ms |

budget: health ≤ 200 ms, wallet ≤ 300 ms, claim ≤ 600 ms, throughput ≥ 20 req/s และ failures = 0.

Harness ปฏิเสธ remote URL โดย default เพื่อลดความเสี่ยงยิง production โดยไม่ตั้งใจ; remote ต้อง opt-in ด้วย `SPLICE_LOAD_ALLOW_REMOTE=true`.

## Bug ที่กลายเป็น regression

- backend test fixture ใช้ defense contract รุ่นเก่า `contentKind/raidPower`
- executable จริง reject ด้วย `Per-unit combat payload is invalid`
- แก้เป็น `unitKind/basePower/scaledPower`
- เพิ่ม assertion ที่ worker claim boundary และ Unity simulator regression
- Nanolod Editor DLL ถูก import เข้า Player build; แก้ importer เป็น Editor-only และเพิ่ม Content Validator test ป้องกัน
- queue claim เคยเกิด serialization failure ภายใต้ concurrency; แยก isolation ให้เหมาะกับ queue โดยไม่ลด isolation ของ financial routes

## Verification

- C2/C3/C4/C4C2E process integration: PASS
- .NET build: 0 warnings, 0 errors
- Unity EditMode: 69/69 passed
- Unity PlayMode: 4/4 passed
- Content Validator: Errors 0, Warnings 0
- local load budget: PASS
- `git diff --check`: PASS

## ข้อจำกัดก่อน production

- proof ปัจจุบันใช้ macOS Development Player; ติดตั้ง optional Dedicated Server module ก่อนสร้าง production worker image ที่เล็กลง
- local load test เป็น regression baseline ไม่ใช่จำนวนผู้เล่นสูงสุดที่รับประกัน
- ก่อนเปิดเงินจริงยังต้องมี workload identity/mTLS, secrets manager, metrics/tracing/alerting, distributed soak test, backup/restore drill และ deployment rollback

## งานถัดไป

C4C2F: raid history/incoming-defense + revenge query จาก authoritative lifecycle แล้วแยก replay blob adapter เพื่อย้าย command stream ไป object storage เมื่อเริ่ม production scale
