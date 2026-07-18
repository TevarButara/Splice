# CODEX Coach Notes

วันที่เริ่มบทบาท: 2026-07-14

## บทบาทของ Codex ในโปรเจกต์นี้

- ทำหน้าที่เป็น executive coach / production advisor สำหรับเกม Splice
- ไม่แก้โค้ดเกมหรือ asset gameplay โดยตรง เว้นแต่ผู้ใช้สั่งชัดเจน
- ช่วยคิดลำดับงานที่ทำให้เกมสมบูรณ์ที่สุดโดยใช้เวลาน้อยที่สุด
- ช่วยตัด scope, ลดความเสี่ยง, และหาวิธีแก้ปัญหาเมื่อเจอ blocker
- หากมี note สำคัญระหว่างทาง ให้เพิ่มลงไฟล์นี้ต่อเนื่อง

## North Star ตอนนี้

พิสูจน์ให้เร็วที่สุดว่า core loop สนุกจริง:

1. ผู้เล่นเลือก faction / เข้าเมือง / จัดฐานได้
2. ผู้เล่นเลือกเป้า raid และเริ่ม raid ได้
3. raid เล่นจบได้ตั้งแต่ต้นจนจบ
4. ได้ reward กลับเข้า meta wallet / base loop
5. ผู้เล่นอยากเล่นซ้ำอีก 20-30 รอบ แม้ยังเป็น greybox

ถ้าข้อ 1-5 ยังไม่ลื่น ห้ามรีบสเกล art, content, PvP, live ops, หรือ monetization หนัก

## Priority ปัจจุบัน

- Focus: Phase 1 playable greybox + Player Base / Async Raid loop
- สถานะตาม roadmap: 5.1-5.4 โค้ดหลักเสร็จ, 5.5 เหลือ idle economy / base level-up / loot scaling / matchmaking-by-strength
- งานที่น่าจะคุ้มที่สุดตอนนี้คือ Editor assembly + playtest checklist + ตัดสินใจ balance แบบหยาบ

## กติกาการตัดสินใจ

- ถ้างานไม่ได้ช่วยให้เล่นจบรอบได้เร็วขึ้น ให้เลื่อนไป backlog
- ถ้างานเป็น art/content scale แต่ยังไม่พิสูจน์ loop ให้ทำแค่ 1 ตัวอย่างเพื่อ validate pipeline
- ถ้าระบบหนึ่งต้องใช้เวลานาน ให้ทำ version ที่วัด fun ได้ก่อน แล้วค่อย harden
- Server-authoritative และ data-driven เป็น architectural guardrail ห้ามทำทางลัดที่ต้องรื้อทีหลัง
- Defender เป็น async base-building, Invader เป็น live play core; อย่ากลับไปทำ live PvP 1:1 ก่อนเวลา

## Risk ที่ต้องจับตา

- Scope creep จาก faction/tier/content เยอะเกินก่อน core loop สนุก
- Editor setup/prefab/scene debt ทำให้โค้ดที่เขียนแล้วไม่เคยถูกทดสอบ end-to-end
- Economy defense snowball: เงินต้องไม่ซื้อฐานกันแตก 100%; ใช้ DefenseCapacity/base level เป็นเพดาน
- Async raid ถ้า reward ไม่รู้สึกคุ้ม หรือแพ้แล้วไม่อยากลองอีก จะทำให้ meta loop แห้ง
- UX 30 วินาทีแรกสำคัญมาก เพราะ genre ผสมหลายชั้น

## Next Coaching Agenda

- สร้าง short milestone แบบ "เล่นได้ใน 1 session" สำหรับ Editor assembly
- นิยาม playtest script 20-30 รอบ และ metric ที่ต้องจด
- แยก backlog เป็น Must / Should / Later เพื่อกันงานบาน
- ตัดสินใจว่า 5.5 economy minimum viable version ต้องมีอะไรบ้างก่อนสเกล content
