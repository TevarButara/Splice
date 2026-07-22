# Splice — Unity Local Backend Integration

อัปเดต: 2026-07-22
สถานะ: Implemented / local-only

## เป้าหมาย

เชื่อม Unity Meta flow กับ ASP.NET Core + PostgreSQL จริง โดยยังไม่สร้าง Cloud resource และไม่เสียค่า server:

`Checkout → Deploy Town → Load Target → Quote Stake → Fund Raid Escrow`

## Authority Boundary

- Unity ส่ง intent และ layout เท่านั้น
- Server ตรวจ content/version/capacity, คำนวณ Gold/War Gem, สร้าง immutable snapshot และ deployment
- Target สำหรับ remote raid ต้องมาจาก server deployment; client ห้ามสร้าง target ที่ stake ได้เอง
- Quote/stake/payout และ wallet balance เชื่อ server เท่านั้น
- Remote checkout ไม่หัก PlayerPrefs wallet เพื่อป้องกัน double debit และ client authority

## Identity Mapping

- `snapshotId` ระบุ immutable town state
- `deploymentId` ระบุเมืองที่เปิดให้ raid และเป็น `targetDeploymentId` ของ C2
- `attackerLoadoutId` เป็น UUID คงที่ต่อ account/faction ใน prototype
- UI แสดง snapshot data ได้ แต่ Quote ต้องใช้ deployment ID เท่านั้น

## เปิดใช้งานในเครื่อง

1. จาก repository root รัน:

   ```bash
   bash Tools/run-local-backend-dev.sh <unity-player-uuid>
   ```

2. ใน Unity เลือก `Splice > Backend > Enable Local Remote Meta`; ใช้ UUID เดียวกับ Player ID ที่แสดงใน Unity Console
3. เล่น Meta flow ตามปกติ; endpoint เริ่มต้นคือ `http://127.0.0.1:5080`
4. เมื่อเลิกทดสอบ เลือก `Splice > Backend > Disable Remote Meta` และกด `Ctrl+C` ที่ server

launcher สร้างและลบฐานข้อมูล `splice_unity_local_dev` อัตโนมัติ. Development bearer และ loopback HTTP ห้ามใช้ production.

## Automated Proof

- EditMode ใช้ HTTP listener จริงเพื่อทดสอบ UnityWebRequest, DTO mapping, auth และ idempotency ตลอด flow
- Backend C1–C3 tests ใช้ ASP.NET Core + PostgreSQL จริง ครอบคลุม ledger, escrow, rollback, immutability และ concurrency
- launcher smoke test ยืนยัน migration/seed, health, wallet, defender target และ cleanup

## Production Gates ที่ยังไม่ทำ

- production identity/token issuance และ HTTPS certificate
- authoritative Raid Server allocation/start/result/settlement (C4)
- secrets manager, monitoring, backup/restore และ load test
- CDN/object storage/signing สำหรับ Live Content production

ขั้นถัดไปคือ C4 โดย server ต้องออก raid ticket, ล็อก deployment/snapshot และเป็นผู้ยืนยันผลก่อน settlement.
