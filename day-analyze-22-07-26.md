# Splice — Daily Analyze 22-07-26

## สถานะรวม

**ความพร้อมนำออกขาย: ประมาณ 30%** (ช่วงประเมิน 25–35%)

Core gameplay อยู่ประมาณ **75%**, Prototype Async อยู่ประมาณ **70%** แต่ backend จริง, content, UI final, tutorial, analytics, monetization, QA และ store launch ยังอยู่ช่วงต้น จึงไม่ควรใช้เปอร์เซ็นต์ของ Prototype แทนความพร้อมขาย

เมื่อเทียบ roadmap เดิม งานระบบ Prototype **เร็วกว่าแผนประมาณ 4–6 สัปดาห์** เพราะ Tactical Raid, Hero, Breach/Extraction/Stake, Town Snapshot, Target Pool และ Raid Session ใช้งานได้แล้ว อย่างไรก็ตามกำหนด Soft Launch ยังไม่ถือว่าปลอดภัยจนกว่าจะผ่าน server/economy และ production vertical slice

## จุดที่ทำให้ช้า

- UI ทำซ้ำหลายรอบก่อนล็อก visual language และ asset จริง
- การประกอบ/ทดสอบ Unity แบบ manual ทำให้รอบแก้ยาว แม้ Unity MCP ช่วยลดลงมากแล้ว
- Content Registry ยังไม่สมบูรณ์ เช่น TowerDefinition/prefab/faction list
- ระบบ economy/async ปัจจุบันเป็น local proof; ก่อนขายต้องย้าย authority ไป server
- Scene และ worktree มีการเปลี่ยนจำนวนมาก ทำให้ตรวจ regression และ commit ยากขึ้น

## วิธีเร่งที่แนะนำ

1. **ล็อก Vertical Slice เดียวก่อน:** 1 faction, 1 Hero, 1 เมือง, 3 layouts, Raid จบครบ แล้วหยุดเพิ่ม feature ชั่วคราว
2. ใช้ **Unity MCP + EditMode/PlayMode automated tests**; bug ที่พบทุกตัวควรกลายเป็น regression test
3. ทำ **Content Validator** ตรวจ SO, prefab, NetworkObject, faction registry และ missing reference อัตโนมัติก่อน Play
4. พัก UI final จนได้ภาพ/กรอบจริง แล้วกำหนด design system หนึ่งชุดเพื่อลดการรื้อซ้ำ
5. แยก commit เล็กตาม Step และเพิ่ม CI build/test เพื่อจับ compile/scene break เร็วขึ้น
6. ออกแบบ server contract สำหรับ wallet, escrow, snapshot และ result ตั้งแต่ Prototype C เพื่อลดการเขียน local logic ใหม่

## งานใหญ่ที่ยังเหลือก่อนขาย

- Defense Report/Revenge, protection และ matchmaking
- Server wallet/escrow, anti-fraud และ authoritative settlement
- World map/World Hunt, Hero collection และ progression
- Content จริงหลายด่าน/ศัตรู/ป้อม พร้อม balance
- UI final, VFX, audio, tutorial และ accessibility
- Analytics, IAP/receipt validation, device performance, QA และ store assets

**ข้อสรุป:** วันนี้ Splice เป็น Prototype ที่พิสูจน์ทิศทางเกมได้ดีและเดินเร็วกว่าแผน แต่ยังไม่ใช่เกมพร้อมขาย เป้าหมายที่เหมาะสมถัดไปคือปิด Prototype B → Prototype C → Production Vertical Slice ก่อนเพิ่มโลกหรือคอนเทนต์จำนวนมาก
