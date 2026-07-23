# Splice Prototype 100% Acceptance — 23-07-26

## นิยาม 100%

100% ในเอกสารนี้หมายถึง Prototype สำหรับ internal demo และพิสูจน์ core loop ตาม scope ปัจจุบัน ไม่ใช่ production-ready หรือพร้อมวางขาย

## Loop ที่ปิดครบ

1. เปิดเกมผ่าน `Bootstrap` แล้วเข้าเมือง `BuildZone`
2. first-run onboarding อธิบาย Town, Raid และ Defense
3. ผู้เล่นดูเมืองและ wallet แล้วเปิดหน้า Raid
4. เลือกเป้าหมายที่แสดง reward, stake และ payout หน่วยหลักร้อย
5. ยืนยัน Raid Contract แล้วเข้า `RaidArena`
6. เล่น/ดู authoritative raid presentation และรับผล
7. เลือก Raid Again หรือ Return to Town
8. เจ้าของเมืองเปิด Defense History, Verified Replay หรือ Revenge ได้

## Acceptance ที่ผ่าน

- Build Settings มี 5 scene ที่จำเป็น: Bootstrap, BuildZone, RaidArena, RaidAttackerPresentation และ RaidDefenderPresentation
- Prototype Hub ใช้ root screen-space Canvas และ responsive scaler 1920×1080
- loading, error, empty และ retry states มีใน meta flow
- dev auto-demo ไม่เริ่มเมื่อมี target, session หรือ replay จริง
- local raid startup retry แบบ bounded ทำงานเฉพาะ readiness race ที่รู้จัก
- local HTTP อนุญาตเฉพาะ Development build; non-development ยังคง HTTPS-only
- Unity EditMode 88/88 และ PlayMode 7/7 ผ่าน
- Content Validator ผ่าน 0 errors / 0 warnings
- clean macOS Development Player build ผ่าน 0 errors
- executable smoke เปิด Bootstrap → BuildZone → Prototype Hub ได้โดยไม่ crash
- Backend Release build ผ่าน 0 errors / 0 warnings
- PostgreSQL ledger และ API integration C1–C4D1 ผ่าน

## Regression สำคัญที่ถูกปิด

- กล้อง Hero follow/center และ Auto follow
- ability radius ตรง pointer
- knock-down Hero ไม่เดินต่อ
- reward/stake off-by-one และใช้หน่วยหลักร้อย
- Raid Contract ไม่ถูก dev auto-demo ปิดทับ
- Raid Contract ไม่หลุด/ถูก clip จาก resolution
- local server startup race มี retry และไม่ retry business/security error
- Unity build target/group ไม่ตรงกันถูกตรวจพบระหว่าง acceptance; clean build ใช้ StandaloneOSX ตรงกัน
- glyph ที่ฟอนต์ runtime ไม่มีถูกแทนด้วยข้อความ ASCII

## นอก scope ของ Prototype 100%

- final UI illustration, icon, animation และ art polish จาก asset ชุดจริง
- production identity/mTLS, secret manager, image signing/scanning และ cloud deployment
- private S3-compatible replay storage และ production CDN
- distributed load/soak บน production-like environment
- device certification, store compliance, analytics cohort, crash reporting และ live-ops runbook
- monetization launch หรือเงินจริง

งานนอก scope เหล่านี้เป็นเหตุผลที่ Production-ready ยังอยู่ที่ 46–49% แม้ Prototype จะครบ 100%.
