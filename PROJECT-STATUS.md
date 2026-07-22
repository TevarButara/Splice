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

## Verification ล่าสุด

- Unity compile: Error 0
- EditMode: 33/33 passed
- PlayMode: 1/1 passed
- Content Validator: Errors 0, Warnings 0
- Target Pool diagnostic: PASS; immutable V1 คงเดิมหลัง commit V2
- Pre-debit snapshot gate ปฏิเสธ snapshot ที่หายหรือ identity/revision ไม่ตรงก่อนหัก stake
- BuildZone และ Bootstrap Incoming Raid runtime smoke test ผ่าน, Console Error 0
- PostgreSQL C1 regression ผ่าน; invariant/idempotency/concurrent double-spend ผ่านซ้ำ 10/10 รอบ
- .NET build: 0 errors, 0 warnings; C2 HTTP/race/recovery regression ผ่านซ้ำ 10/10 รอบ; NuGet vulnerability scan ไม่พบช่องโหว่
- C3 pre-debit/hash/immutability/concurrency/rollback regression ผ่านซ้ำ 10/10 รอบ; formatting ผ่าน

## Backend

- Architecture contract: `splice-server-wallet-escrow-snapshot-contract-db.md`
- สถานะ: C0 Boundary, C1 PostgreSQL, C2 Wallet/Escrow และ C3 immutable Town API local-only เสร็จแล้ว
- Backend package: `Backend`; ใช้ HTTP เฉพาะ 127.0.0.1 ตอนทดสอบ ยังไม่เปิด Cloud/production
- Stack ที่เสนอ: ASP.NET Core modular monolith + PostgreSQL; deploy แบบ stateless containers และแยก authoritative Unity Raid Server ในระยะ C4

## งานถัดไป

1. สร้าง Unity Content Catalog Exporter/Validator และ seed content จริงเข้า PostgreSQL
2. เชื่อม Unity remote adapters กับ local C2/C3 API พร้อม EditMode/PlayMode tests
3. ทดสอบ local end-to-end: Checkout → Deploy → Target → Quote → Fund
4. C4: Trusted authoritative Raid Result/Settlement

## สิ่งที่ยังห้ามใน production

- ห้ามเชื่อ balance, stake, payout หรือ raid result จาก Unity client
- ห้ามใช้ local-host result กับ shared economy
- ห้ามเปิดเงินจริงก่อน idempotency, crash recovery, reconciliation, load test และ backup/restore ผ่าน

## วิธีใช้เอกสาร

- อ่านไฟล์นี้ก่อนเริ่มงานทุกครั้ง
- อ่านไฟล์ความคืบหน้าของวันนี้เพิ่มเติม
- เปิดประวัติวันเก่าเฉพาะเมื่อต้องตรวจเหตุผลหรือ regression
