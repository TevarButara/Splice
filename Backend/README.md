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
