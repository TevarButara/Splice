# Splice — Project Status

อัปเดตล่าสุด: 2026-07-22

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

## Verification ล่าสุด

- Unity compile: Error 0
- EditMode: 54/54 passed
- PlayMode: 2/2 passed
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
- C4C1 regression ผ่าน: Hero/Gear ที่ไม่ได้เป็นเจ้าของถูก reject, power breakdown 130+2,830+200=3,160 และ worker ได้ immutable Hero combat/Gear payload แม้แก้ loadout ภายหลัง
- C4C2A regression ผ่าน: command stream replay ตรงกัน, collection order ไม่เปลี่ยน hash, forged power ถูก reject และตำแหน่งเมืองเปลี่ยนทำให้ simulation hash เปลี่ยน

## Backend

- Architecture contract: `splice-server-wallet-escrow-snapshot-contract-db.md`
- สถานะ: C0 Boundary, C1 PostgreSQL, C2 Wallet/Escrow, C3 immutable Town API, Unity local integration, C4A lifecycle, C4B worker, C4C1 Hero/Gear authority และ C4C2A fixed-tick kernel เสร็จแล้ว
- Backend package: `Backend`; ใช้ HTTP เฉพาะ 127.0.0.1 ตอนทดสอบ ยังไม่เปิด Cloud/production
- Stack ที่เสนอ: ASP.NET Core modular monolith + PostgreSQL; deploy แบบ stateless containers และแยก authoritative Unity Raid Server ในระยะ C4

## งานถัดไป

1. C4C2B: ทำ RaidArena command-stream presentation adapter ให้เห็น Hero/Army เคลื่อนที่ โจมตี และ breach ตามผล authoritative
2. เพิ่ม authoritative combat payload รายตัวสำหรับ Monster/Tower เพื่อแทน aggregate power ใน kernel
3. ให้ player Raid Arena subscribe/poll authoritative lifecycle แล้วแสดง Pending/Completed/Replay
4. เพิ่ม headless executable end-to-end smoke; ก่อน production ค่อยเพิ่ม autoscaling/metrics/secrets

## สิ่งที่ยังห้ามใน production

- ห้ามเชื่อ balance, stake, payout หรือ raid result จาก Unity client
- ห้ามใช้ local-host result กับ shared economy
- ห้ามเปิดเงินจริงก่อน idempotency, crash recovery, reconciliation, load test และ backup/restore ผ่าน

## วิธีใช้เอกสาร

- อ่านไฟล์นี้ก่อนเริ่มงานทุกครั้ง
- อ่านไฟล์ความคืบหน้าของวันนี้เพิ่มเติม
- เปิดประวัติวันเก่าเฉพาะเมื่อต้องตรวจเหตุผลหรือ regression
