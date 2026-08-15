# MOBA Hero Ability Library

คลังออกแบบ Hero แบบ **1 Hero ต่อ 1 ไฟล์** เพื่อค้นหาและแก้ได้โดยไม่ต้องโหลดเอกสารทั้งหมด

## Index

| Faction | Hero | Role | Status | File |
|---|---|---|---|---|
| Galax | Veyra — Comet Launcher | Artillery / Zone Control | Draft v0.1 | [`Galax/veyra-comet-launcher.md`](Galax/veyra-comet-launcher.md) |

## File Rules

- Path: `<Faction>/<hero-id>.md`
- Hero ID: ตัวพิมพ์เล็ก ใช้ `-` คั่นคำ เช่น `galax/veyra-comet-launcher`
- หนึ่งไฟล์เก็บเฉพาะข้อมูลของ Hero นั้น; กติกาที่ใช้ร่วมกันให้เก็บในไฟล์นี้
- ไม่จำเป็นต้องมี Skill 1–3 ครบทุกตัว และแต่ละช่องอาจเป็น Active, Passive, Toggle หรือ Buff ได้
- Skill 4 สงวนเป็น Ultimate เว้นแต่คอนเซปต์ Hero ระบุเป็นอย่างอื่น
- ตัวเลขในสถานะ `Draft` เป็น initial tuning สำหรับ playtest ไม่ใช่ balance ขั้นสุดท้าย
- เมื่อเพิ่มหรือลบ Hero ให้อัปเดตตาราง Index เท่านั้น

## Shared Terms

- **AP:** Ability Power
- **Shield:** รับความเสียหายแทน HP; ไม่ถือเป็นการฟื้นฟู HP
- **Slow:** ลด Move Speed ตามเปอร์เซ็นต์ที่ระบุ
- **Displacement:** การผลัก ดึง หรือเคลื่อนตำแหน่งด้วยสกิล
- **CC:** Crowd Control

## Design Checklist

- ระบุจุดแข็ง จุดอ่อน และ counterplay ที่ฝ่ายตรงข้ามเข้าใจได้
- สกิลแต่ละอันต้องมีหน้าที่ต่างกันและสนับสนุน gameplay loop เดียวกัน
- จำกัดข้อความต่อสกิลให้สั้น; แยกค่าตัวเลขไว้ในตาราง
- หลีกเลี่ยง mechanic ใหม่หาก mechanic เดิมอธิบายคอนเซปต์ได้เพียงพอ

