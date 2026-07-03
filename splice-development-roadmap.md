# Splice — แผนพัฒนา (Development Roadmap)

**หลักการจัดลำดับ:** พิสูจน์ว่า "core loop สนุกไหม" ให้เร็วที่สุด ก่อนลงทุนกับอาร์ต/คอนเทนต์/multiplayer ที่ใช้เวลาเยอะและแก้ทีหลังยาก

---

## ขั้นที่ 0: เตรียมเครื่องมือ (สัปดาห์นี้)

- [ ] ติดตั้ง Unity 6 LTS + สร้างโปรเจกต์ด้วย URP (Mobile) template
- [ ] ติดตั้ง package: Netcode for GameObjects, Unity Transport, Input System
- [ ] ตั้ง Git repository + `.gitignore` สำหรับ Unity (กัน Library/, Temp/ ไม่ให้ push ขึ้น repo)
- [ ] เปิด Claude Code ในโปรเจกต์ ตั้งค่า context ให้รู้จักโครงสร้างโปรเจกต์
- [ ] วางโครงสร้างโฟลเดอร์เบื้องต้น: `Scripts/Core`, `Scripts/Network`, `Data/ScriptableObjects`, `Art/Prototype`

**ตัวอย่างงานแรกที่มอบให้ AI agent:** "ช่วย scaffold โครงสร้างโปรเจกต์ Unity สำหรับเกม reverse tower defense พร้อม folder convention และ base class สำหรับระบบ deploy unit แบบ server-authoritative ด้วย Netcode for GameObjects"

---

## ขั้นที่ 1: Greybox Core Loop Prototype (สัปดาห์ 2-4) ⭐ สำคัญที่สุด

**เป้าหมาย:** ตอบคำถาม "เกมนี้สนุกจริงไหม" ให้เร็วที่สุด — **ห้ามลงอาร์ตจริงตอนนี้** ใช้ cube/capsule/primitive แทนทั้งหมด

งานที่ต้องทำ:
- [ ] Deploy system (แตะ/ลากวางมอนสเตอร์ลง lane)
- [ ] Mana/resource regen (server-authoritative timer)
- [ ] Tower targeting + attack AI พื้นฐาน
- [ ] Win/Lose condition (ทำลายหัวใจป้อม / ฝูง HP หมด)
- [ ] 1 lane, 1 level เดียวพอ — ไม่ต้องหลากหลาย

**Deliverable:** build ที่เล่นจบ 1 รอบได้ตั้งแต่ต้นจนจบ แม้จะหน้าตาเป็นกล่องสี่เหลี่ยมล้วนก็ตาม

**Playtest:** เล่นเองซ้ำๆ อย่างน้อย 20-30 รอบ ถามตัวเองว่า "อยากเล่นรอบต่อไปไหม" ถ้าคำตอบคือไม่ ต้องหยุดแล้วปรับ design ก่อนเดินหน้าต่อ

### ชุดคำสั่งสำหรับ Claude Code (สั่งทีละ prompt เรียงตามลำดับ)

**หลักการ:** อย่ายัดทุกอย่างเป็น prompt เดียว — สั่งทีละก้อน แล้ว **build/เทสใน Editor ก่อนไปก้อนถัดไปเสมอ** ถ้า build ผ่านและทำงานตามที่คาด ค่อยสั่งก้อนต่อไป วิธีนี้ debug ง่ายกว่ามาก เพราะรู้ทันทีว่าจุดที่พังคือโค้ดชุดล่าสุดที่เพิ่งสั่งไป

---

**Prompt 1 — Project Scaffold + Data Foundation**
```
สร้างโครงสร้างโปรเจกต์ Unity สำหรับเกม reverse tower defense ชื่อ "Splice" ดังนี้:
1. โครงสร้างโฟลเดอร์: Scripts/Core, Scripts/Network, Scripts/Data, Data/ScriptableObjects, Art/Prototype
2. ติดตั้งและตั้งค่า Netcode for GameObjects + Unity Transport ให้พร้อมใช้งาน
3. สร้าง NetworkManager ที่ config ให้รันแบบ "Host" mode (client+server โปรเซสเดียว สำหรับโหมด PvE offline)
4. สร้าง ScriptableObject 2 คลาส: MonsterDefinitionSO (id, displayName, maxHP, damage, attackIntervalSeconds, moveSpeed, manaCost) และ TowerDefinitionSO (id, displayName, maxHP, damage, attackIntervalSeconds, attackRange)
5. สร้างตัวอย่างข้อมูล 1 monster กับ 1 tower เป็น SO asset สำหรับเทส
```

**Prompt 2 — Mana / Resource System**
```
สร้างระบบ mana แบบ server-authoritative สำหรับผู้เล่นในแมตช์:
1. ใช้ NetworkVariable<float> เก็บค่า mana ปัจจุบัน (คำนวณ/แก้ไขได้ที่ server เท่านั้น)
2. Mana รีเจนอัตโนมัติตามเวลา (server-side timer) จนถึงค่าสูงสุดที่กำหนดได้
3. เพิ่ม UI text แบบง่าย (ไม่ต้องมีอาร์ต) แสดงค่า mana ปัจจุบันบนหน้าจอ อัพเดตแบบเรียลไทม์จาก NetworkVariable
```

**Prompt 3 — Deploy System**
```
สร้างระบบวางมอนสเตอร์ (deploy) แบบ server-authoritative:
1. รับ input การแตะ/คลิกบนหน้าจอเพื่อเลือกตำแหน่งวางบน lane (ใช้ lane เดียวก่อน)
2. Client ส่ง ServerRpc พร้อมข้อมูล monsterId และตำแหน่งที่จะวาง
3. Server validate: เช็คว่า mana พอจ่าย manaCost ของมอนสเตอร์ตัวนั้นไหม
4. ถ้าผ่าน: server หัก mana แล้ว spawn NetworkObject (ใช้ capsule primitive แทนโมเดลจริง) โดยดึงค่าพื้นฐานจาก MonsterDefinitionSO
5. ถ้าไม่ผ่าน: reject เงียบๆ ไม่ spawn อะไร
```

**Prompt 4 — Combat & Basic AI**
```
สร้างระบบต่อสู้พื้นฐานระหว่างมอนสเตอร์กับ tower:
1. มอนสเตอร์: เดินตรงไปตาม lane จนกว่าจะเจอ tower ในระยะ attackRange แล้วหยุดโจมตีเป็นช่วงตามค่า attackIntervalSeconds
2. Tower: ตรวจจับมอนสเตอร์ที่เข้ามาในระยะ attackRange แล้วโจมตีตามช่วงเวลาเดียวกัน
3. ทั้งสองฝั่งใช้ state machine ง่ายๆ: Move -> Attack -> Dead
4. HP และผลลัพธ์ดาเมจคำนวณที่ server เท่านั้น เมื่อ HP เหลือ 0 ให้ despawn NetworkObject
```

**Prompt 5 — Win/Lose Condition + Test Level**
```
ประกอบ level ทดสอบ 1 ด่านให้เล่นจบครบ loop:
1. วาง tower ตาม lane 2-3 ตัว จบด้วย "หัวใจป้อม" ที่มี HP pool ของตัวเอง
2. เงื่อนไขชนะ: ทำลายหัวใจป้อมให้ HP เหลือ 0
3. เงื่อนไขแพ้: ฝูงมอนสเตอร์ผู้เล่น (ใช้ HP pool รวมของทีม) เหลือ 0 ก่อนถึงหัวใจป้อม
4. เมื่อจบเกม (ชนะหรือแพ้) แสดง UI ข้อความง่ายๆ พร้อมปุ่ม "เล่นใหม่" ที่รีเซ็ตด่านกลับสถานะเริ่มต้น
```

หลังจาก Prompt 5 ผ่านแล้ว คุณจะได้ build ที่เล่นจบ 1 รอบได้ตามเป้าหมายของขั้นที่ 1 พอดี — ตรงนี้แหละคือจุดที่ต้องหยุด playtest ก่อนเดินหน้าไปขั้นถัดไป

---

## ขั้นที่ 2: Data Architecture Foundation (ขนานกับขั้นที่ 1)

- [ ] สร้าง `MonsterDefinitionSO`, `TowerDefinitionSO`, `CardDefinitionSO`
- [ ] ให้ AI agent ช่วยร่าง stat formula (damage/HP/cost scaling ตาม tier) ออกมาเป็น JSON/CSV แล้วเขียน import script แปลงเป็น SO asset

---

## ขั้นที่ 3: Draft / Roguelite Structure (เดือนที่ 2)

- [ ] ระบบ draft มือการ์ดต่อรอบ (server กำหนด seed)
- [ ] Run structure (3-5 ด่านต่อรอบ raid)
- [ ] Persistent unlock เชื่อมกับ Lair (ยังไม่ต้องมี UI สวย แค่ logic ทำงานถูก)

---

## ขั้นที่ 4: Art Pipeline Validation — ตัวละครตัวแรก (เดือนที่ 2, ขนานกับขั้นที่ 3)

**ทำแค่ 1 ตัวให้จบ pipeline ก่อนสเกล อย่าเพิ่งทำหลายตัวพร้อมกัน**

- [ ] เลือก faction เริ่มต้น: **Human** (rig แบบ humanoid มาตรฐาน ทดสอบง่ายสุด)
- [ ] Generate mesh ด้วย Meshy หรือ Tripo
- [ ] Remesh ลด poly ให้เหมาะมือถือ
- [ ] Rig ผ่าน Meshy auto-rig หรือส่งเข้า Mixamo
- [ ] Import เข้า Unity, ผูก Animator, ทดสอบ animation (idle/walk/attack/death)
- [ ] เอาไปแทนที่ placeholder ใน greybox prototype ดูว่า workflow ทั้งเส้นลื่นไหม

เมื่อ pipeline นี้ทำซ้ำได้เร็วและไม่ติดขัดแล้ว ค่อยไปขั้นที่ 6 (สเกลคอนเทนต์)

---

## ขั้นที่ 5: Lair Meta System (เดือนที่ 3)

- [ ] Idle resource generation
- [ ] ระบบฟักไข่/คราฟต์มอนสเตอร์ (โปร่งใส ไม่ปิดบัง odds)
- [ ] อัพเกรดถาวร + cosmetic slot (เผื่อ skin ระบบทีหลัง)
- [ ] UI เบื้องต้น (ไม่ต้องสวยที่สุด แค่ใช้งานได้)

---

## ขั้นที่ 6: Content Scale-Out (เดือนที่ 3-4)

- [ ] ผลิตตาม tier framework ที่ตกลงกันไว้ (T1-T5) สำหรับ **4 faction เริ่มต้น: Human, Undead, Beast, Galax**
- [ ] Tower faction คู่ขนาน (mirror ธีมเดียวกับฝั่งมอนสเตอร์)
- [ ] Elf, Thorn, Demon เก็บไว้เป็น content update หลัง launch

---

## ขั้นที่ 7: Onboarding / Tutorial (เดือนที่ 4)

- [ ] ออกแบบ tutorial ที่ชัดเจนใน 30 วินาทีแรก (บทเรียนจากความล้มเหลวของ Countless Army เรื่อง UX/สอนเล่น)
- [ ] ทดสอบกับคนที่ไม่เคยเห็นเกมมาก่อน (เพื่อน/ชุมชน) ดูว่าเข้าใจภายใน 1 นาทีแรกไหม

---

## ขั้นที่ 8: Analytics (เริ่มติดตั้งได้ตั้งแต่ขั้นที่ 1 จริงๆ)

- [ ] ติดตั้ง Firebase Analytics หรือเทียบเท่า
- [ ] Track retention funnel (D1/D7/D30), จุดที่ผู้เล่นออกจากเกมกลางคัน, conversion rate ของแต่ละ monetization hook

---

## ขั้นที่ 9: Soft Launch Prep (เดือนที่ 5)

- [ ] เตรียม store listing (icon, screenshot, คำโปรย)
- [ ] เลือกตลาดทดสอบ (ประเทศเล็กที่พฤติกรรมใกล้เคียงตลาดหลัก)
- [ ] เชื่อม rewarded ads + IAP + receipt validation (server-side)

---

## ขั้นที่ 10: Soft Launch & Iterate (เดือนที่ 5-6)

- [ ] ปล่อยตลาดทดสอบ ดู retention/monetization data จริง
- [ ] ปรับจูนตาม data ก่อนตัดสินใจ launch ตลาดหลัก

---

## หลังจากนี้: Phase 2 (PvBot) และ Phase 3 (PvP)

รายละเอียดสถาปัตยกรรมอยู่ในเอกสาร `technical-architecture.md` ที่ทำไว้ก่อนหน้า — เริ่มลงมือได้ก็ต่อเมื่อ PvE core loop พิสูจน์แล้วว่า retention ดีและมี player base รองรับปัญหา cold start

---

## สิ่งที่ควรทำ "วันนี้" เพื่อเริ่มจริงๆ

1. ติดตั้ง Unity 6 LTS + สร้างโปรเจกต์ใหม่
2. Push ขึ้น Git repo ว่างๆ ก่อนเขียนโค้ดบรรทัดแรก
3. เปิด Claude Code แล้วเริ่มงานแรก: scaffold deploy system + mana system แบบ greybox (ขั้นที่ 1)

อย่าข้ามไปทำ art หรือ faction design ก่อนพิสูจน์ core loop — นี่คือความเสี่ยงเดียวที่ถ้าผิดพลาดจะเสียเวลามากที่สุด
