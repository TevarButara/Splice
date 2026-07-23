# Splice — Project Status

อัปเดตล่าสุด: 2026-07-23

## ทิศทางเกม

- Tactical Breach ผสม Heist Raid และ Async Base War
- ผู้เล่นสร้างเมืองแบบ immutable snapshot, จัดกองป้องกัน และส่ง Hero + กองทัพไป raid เมืองอื่น
- War Gem เป็น stake/loot currency; Premium Diamond แยกจากของที่ขโมยได้

## สถานะปัจจุบัน

- Scene Architecture Refactor เสร็จแล้ว
- `Bootstrap` โหลด `RaidArena` และ additive role presentation
- แยก `RaidAttackerPresentation` / `RaidDefenderPresentation`
- DefenderCamera ใช้มุม MonCamera แบบกลับฝั่ง 180°
- Visible Incoming Raid ทำงานจริง: Hero, 3 waves, 11 raiders และ Defense Report
- C0.1–C0.3 Backend Boundary เสร็จแล้ว: Unity-facing contracts, runtime consumers และ remote-ready transport boundary
- Raid Offer UI ใช้ quote/confirm contract; Town Deployment UI ใช้ snapshot service แทน PlayerPrefs stores โดยตรง
- Target Pool, immutable Snapshot Loader, raid settlement/reward, revenge gate และ Defense Report ใช้ service boundary แล้ว
- มี public Meta API client, serializer, standard error mapping และ local loopback transport; ยังไม่เปิด network/Cloud จริง
- Remote player composition ถูก guard ไม่ให้เขียน raid report, payout หรือ settlement แทน trusted Raid Server
- C1 PostgreSQL ledger แบบ local-only เสร็จแล้ว: migrations, currencies/system accounts, double-entry posting, idempotency และ outbox
- C2 ASP.NET Core Wallet/Escrow API แบบ local-only เสร็จแล้ว: auth boundary, wallet, quote, confirm/fund, startup refund และ reconciliation
- C3 immutable Town backend เสร็จแล้ว: draft, server checkout, layout commit, snapshot, deployment, vault/escrow และ global snapshot query
- U1 Live Content Update เสร็จแล้วแบบ local-first: Addressables groups/profile, deterministic Content Catalog + PostgreSQL seed, runtime updater, retry/rollback, automated tests และ content-only build proof
- U2 Unity ↔ Local Backend Integration เสร็จแล้ว: HTTP transport, development bootstrap, server-authoritative Checkout/Deploy/Target/Quote/Fund และ local launcher
- C4A Authoritative Raid Lifecycle เสร็จแล้วแบบ local-only: allocation ticket, trusted start/result, zero-sum settlement, immutable result และ active-session recovery
- C4B Authoritative Loadout + Headless Worker เสร็จแล้วแบบ local-only: validated army, immutable loadout snapshot, worker queue/lease และ deterministic Unity batchmode proxy
- C4C1 Authoritative Hero + Gear เสร็จแล้วแบบ local-only: ownership inventory, Gear instance UUID, server combat stats/power และ immutable raid payload
- C4C2A Fixed-Tick Combat Kernel เสร็จแล้ว: worker ใช้ immutable town positions/loadout/Hero/Gear จำลอง 3 breach rings และสร้าง deterministic command-stream hash
- C4C2B Command-Stream Presentation เสร็จแล้ว: `RaidArena` แสดง Hero + Army, ring breach, ability pulse และผล authoritative จาก stream แบบ read-only ในมุมผู้ป้องกัน
- C4C2C Per-Unit Combat Authority เสร็จแล้ว: backend ตรึง combat payload ของ Monster/Tower/Core รายตัวใน immutable snapshot และ kernel คำนวณ HP/armor/damage/cooldown/target/defeat ต่อ actor
- Quote gate ปฏิเสธ town snapshot schema เก่าก่อน reserve stake; ผู้เล่นเจ้าของเมืองต้อง redeploy เพื่อสร้าง snapshot schema 2
- C4C2D Lifecycle + Verified Replay เสร็จแล้ว: Unity poll เฉพาะ public read routes, backend เก็บ immutable command stream, ตรวจ command hash/version/count ก่อน settlement และ participant เรียก replay ได้
- C4C2E Worker Reliability + Load Proof เสร็จแล้ว: build Unity executable จริง, crash/reclaim/duplicate-delivery แบบข้าม process, stable result identity, bounded retry/heartbeat และ local load budget
- C4C2F Defense History + Verified Revenge เสร็จแล้ว: history เฉพาะเจ้าของเมือง, stable pagination, verified replay launch และ revenge request ที่ backend ผูก source raid/เป้าหมาย/snapshot/cooldown
- C4C2G Replay Object Storage เสร็จแล้วแบบ local-first: replay ใหม่เก็บ immutable gzip blob นอก PostgreSQL, DB เก็บ metadata/hash pointer, ตรวจ blob/hash/count ก่อนส่ง และ dual-read replay JSONB รุ่นเก่า
- C4D1A Ops Telemetry + Replay Lifecycle เสร็จแล้วแบบ local-first: protected status endpoint, request/replay/reconciliation metrics, queue/lease/stuck/outbox alerts และ race-safe orphan/temp cleanup แบบ opt-in
- C4D1B Backup/Restore Drill เสร็จแล้วแบบ local-first: exported-snapshot bundle ครอบ DB + referenced blobs, private/no-overwrite restore, ledger/hash/fingerprint/replay verification และ corrupt/missing fail-closed
- worker queue ใช้ `READ COMMITTED + FOR UPDATE SKIP LOCKED`; เส้นทางเงินยังคง `SERIALIZABLE` พร้อม bounded retry เพื่อให้ scale โดยไม่ลดความถูกต้องของ ledger

## ความพร้อมโดยประมาณ

- Prototype ที่เล่นและสาธิต loop หลักได้: 74–76%
- MVP สำหรับ closed playtest ที่มี backend จริง: 61–64%
- Production-ready สำหรับขายและรองรับผู้เล่นจำนวนมาก: 42–45%
- เปอร์เซ็นต์ production นับรวม security hardening, observability, load/soak test, backup/restore, deployment automation, live operations, content/polish และ store compliance—not แค่ feature ที่มองเห็นใน Unity

## Verification ล่าสุด

- Unity compile: Error 0
- EditMode: 73/73 passed
- PlayMode: 4/4 passed
- Content Validator: Errors 0, Warnings 0
- Target Pool diagnostic: PASS; immutable V1 คงเดิมหลัง commit V2
- Pre-debit snapshot gate ปฏิเสธ snapshot ที่หายหรือ identity/revision ไม่ตรงก่อนหัก stake
- BuildZone และ Bootstrap Incoming Raid runtime smoke test ผ่าน, Console Error 0
- PostgreSQL C1 regression ผ่าน; invariant/idempotency/concurrent double-spend ผ่านซ้ำ 10/10 รอบ
- .NET build: 0 errors, 0 warnings; C2 HTTP/race/recovery regression ผ่านซ้ำ 10/10 รอบ; NuGet vulnerability scan ไม่พบช่องโหว่
- C3 pre-debit/hash/immutability/concurrency/rollback regression ผ่านซ้ำ 10/10 รอบ; formatting ผ่าน
- Addressables content-only proof ผ่าน: baseline `1.0.0` → update `1.0.1`, catalog hash เปลี่ยน, `IsUpdateContentBuild=true` และไม่มี Player rebuild
- Unity Content Catalog export 7 definitions แบบ deterministic; backend รองรับ composite identity `(content_id, content_kind)` และ C1–C3 regression ยังผ่าน
- Configurator รันซ้ำได้แบบ idempotent และล้าง stale Addressables groups ที่ไม่มี content definition; มี regression test ป้องกัน group ชื่อซ้ำ/เลขต่อท้าย
- Unity HTTP contract regression ผ่าน flow Checkout → Deploy → Target → Quote → Fund พร้อมตรวจ Bearer auth และ idempotency headers
- local ASP.NET/PostgreSQL launcher smoke test ผ่าน; API health, wallet และ defender deployment อ่านได้จริง และฐานข้อมูลชั่วคราวถูกลบหลังหยุด
- C4 trusted-route regression ผ่าน: player ปลอม start/result ไม่ได้, result replay ไม่จ่ายซ้ำ, conflicting result ถูกปฏิเสธ และ deployment ถูก Pause เมื่อ backing ต่ำกว่าเกณฑ์
- C4B regression ผ่าน: loadout ปลอมถูก reject, quote ตรึง immutable army, worker อื่นขโมย lease/result ไม่ได้ และ empty queue ตอบแบบ explicit
- C4C1 regression ผ่าน: Hero/Gear ที่ไม่ได้เป็นเจ้าของถูก reject และ worker ได้ immutable Hero combat/Gear payload แม้แก้ loadout ภายหลัง
- C4C2A regression ผ่าน: command stream replay ตรงกัน, collection order ไม่เปลี่ยน hash, forged power ถูก reject และตำแหน่งเมืองเปลี่ยนทำให้ simulation hash เปลี่ยน
- C4C2B visual smoke ผ่านด้วย Unity MCP: role picker ไม่บัง, Hero + Army 8 ตัวอยู่ใน DefenderCamera, actor proxy ไม่หายตาม network prefab lifecycle และ replay จบที่ `COMPLETE`
- C4C2C regression ผ่าน: per-actor target/defeat deterministic, forged aggregate power ถูก reject, Monster/Tower/Core payload มาจาก server catalog/snapshot และ legacy combat snapshot ถูกปฏิเสธก่อน reserve stake
- C4C2C visual smoke ผ่านด้วย Unity MCP: replay สร้างยูนิตจริง, breach ครบ 3 rings และจบ `FULL VICTORY` ที่ tick 82
- C4C2D regression ผ่าน: tampered command hash ถูก reject ก่อน settlement, wallet ไม่เปลี่ยน, replay row แก้/ลบไม่ได้, lifecycle/result/snapshot identity ตรงกัน และ simulated incoming defense ไม่ poll attacker endpoint
- C4C2D visual smoke ผ่านด้วย Unity MCP: HUD แสดง `RAID SERVER ACTIVE` และ authoritative lifecycle progress ใน RaidArena
- C4C2E process E2E ผ่าน: Unity executable ล่มหลัง claim ด้วย exit 77, lease ถูก reclaim ใน attempt 2, duplicate result ไม่จ่ายซ้ำ และมี result/replay/funding/settlement อย่างละชุด
- C4C2E load budget ผ่านที่ 240 requests / concurrency 16: failures 0, 2,975.43 req/s; p95 health 8.84 ms, wallet 14.47 ms, empty worker claim 35.79 ms
- C4C2F integration ผ่าน: attacker อ่าน incoming history ไม่ได้, defender เปิด verified replay ได้, target/snapshot ถูกผูกจาก server, request หมดอายุสร้างใหม่ได้ และ cooldown เริ่มเมื่อ trusted worker start เท่านั้น
- C4C2F load recheck ผ่านที่ 240 requests / concurrency 16: failures 0, 2,416.51 req/s; p95 health 33.13 ms, wallet 26.08 ms, empty worker claim 58.11 ms (ทุกค่าต่ำกว่า budget)
- C4C2G integration ผ่าน: DB pointer-only, immutable gzip blob, byte SHA-256/size/gzip/JSON/canonical command validation, non-participant denial, missing/corrupt fail-closed และ legacy dual-read
- C4C2G Unity executable process E2E ผ่าน crash/reclaim/duplicate settlement; load proof 240 requests / concurrency 16, failures 0, 2,700.96 req/s; p95 health 12.79 ms, wallet 30.18 ms, claim 48.74 ms
- C4D1A integration ผ่าน: ops endpoint ป้องกันด้วย trusted identity, stuck raid ทำให้สถานะ `DEGRADED`, reconciliation คืนเป็น `HEALTHY`, referenced blob ไม่ถูกลบ, orphan/temp ถูกเก็บกวาด และ blob ที่เปลี่ยนระหว่าง scan รอดจากการลบ
- C4D1A load proof 240 requests / concurrency 16: failures 0, 981.13 req/s; p95 health 35.61 ms, wallet 69.87 ms, claim 86.98 ms (ผ่าน budget ทั้งหมด)
- C4D1B drill ผ่าน: consistent snapshot/fingerprint, ledger/account/snapshot/escrow integrity, restored API replay, private bundle, no leaked snapshot connection และ corrupt/missing object ถูกปฏิเสธก่อนสร้าง target
- C4D1B local fixture timing: backup 0–1 วินาที, restore+verifyต่ำกว่า 1 วินาที; ไม่ใช่ production RTO
- bug จาก fixture ที่ใช้ defense field รุ่นเก่าถูกจับโดย executable จริงและกลายเป็น regression ที่ตรวจ `unitKind/scaledPower`
- Unity headless proof build ผ่านด้วย macOS Development Player + batchmode/nographics; Dedicated Server subtarget จะเปิดเมื่อ install optional macOS Dedicated Server module
- Nanolod Editor-only DLL ถูกจำกัด importer ไม่ให้เข้า Player build และมี Content Validator regression ป้องกัน

## Backend

- Architecture contract: `splice-server-wallet-escrow-snapshot-contract-db.md`
- สถานะ: C0 Boundary, C1 PostgreSQL, C2 Wallet/Escrow, C3 immutable Town API, Unity local integration, C4A lifecycle, C4B worker, C4C1 Hero/Gear authority, C4C2A–G และ C4D1A–B ops/backup เสร็จแล้ว
- Backend package: `Backend`; ใช้ HTTP เฉพาะ 127.0.0.1 ตอนทดสอบ ยังไม่เปิด Cloud/production
- Stack ที่เสนอ: ASP.NET Core modular monolith + PostgreSQL; deploy แบบ stateless containers และแยก authoritative Unity Raid Server ในระยะ C4

## งานถัดไป

1. ปิด Prototype visual loop: หน้า Defense History/Revenge UI จริง และเชื่อม Town → Target → Raid → Result → Replay/Revenge ครบวงจร
2. Prototype polish: onboarding, loading/error states, feedback, balance เบื้องต้น และ executable smoke ตั้งแต่ต้นจนจบ
3. C4D1C: container deployment, readiness, external metrics exporter และ production alert routing
4. เปลี่ยน `IRaidReplayBlobStore` เป็น private S3-compatible adapter เมื่อมี production environment; local filesystem ใช้เฉพาะ dev/test
5. ทำ distributed load/soak test เมื่อมี production-like environment; local harness ปัจจุบันเป็น baseline ไม่ใช่ capacity guarantee

## สิ่งที่ยังห้ามใน production

- ห้ามเชื่อ balance, stake, payout หรือ raid result จาก Unity client
- ห้ามใช้ local-host result กับ shared economy
- ห้ามเปิดเงินจริงก่อน production auth/mTLS, observability, distributed load/soak และ backup/restore drill ผ่าน

## วิธีใช้เอกสาร

- อ่านไฟล์นี้ก่อนเริ่มงานทุกครั้ง
- อ่านไฟล์ความคืบหน้าของวันนี้เพิ่มเติม
- เปิดประวัติวันเก่าเฉพาะเมื่อต้องตรวจเหตุผลหรือ regression
