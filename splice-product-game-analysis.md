# Splice — Product, Game Design, Business & Engineering Analysis

**วันที่วิเคราะห์:** 20 กรกฎาคม 2026  
**สถานะที่ตรวจ:** เอกสาร Markdown ที่ repository root พร้อมโครงสร้าง Unity, scripts, scenes, prefabs และ packages ที่เกี่ยวข้อง  
**วัตถุประสงค์:** ประเมินว่าเกมควรปรับตรงไหน ทำอย่างไรให้สนุกและอยากเล่นซ้ำ แนวเกมควรเปลี่ยนหรือไม่ และจัดลำดับการพัฒนาในมุม Business Analyst, Project Analyst, Game Ideator และ Game Engineer

---

## Executive Summary

Splice มี “แกนไอเดียที่ขายได้” แต่ขณะนี้กำลังสร้างระบบรอบเกมเร็วกว่าการพิสูจน์ว่าช่วงเล่นจริงสนุกหรือไม่

ไม่แนะนำให้เปลี่ยนแนวเกมทั้งหมด แนะนำให้ลดและลับแนวเกมให้ชัดเป็น:

> **Tactical Reverse Tower Defense Raid**  
> ผู้เล่นอ่านฐาน วางแผนทัพ แล้วตัดสินใจปล่อยยูนิต ใช้เวท และจัดการเลนเพื่อเจาะฐานภายใน 2–4 นาที

Roguelite deckbuilding ควรสร้างความหลากหลาย ส่วน idle/base-building ควรเป็นเหตุผลให้กลับมาเล่น ทั้งสองส่วนไม่ควรแย่งความเด่นจากการ raid

สิ่งสำคัญที่สุดสามอย่างในตอนนี้:

1. ทำ raid loop ให้เล่นลื่นตั้งแต่เริ่มจนกดเล่นซ้ำ
2. เพิ่มการตัดสินใจสดระหว่าง raid ให้ผู้เล่นรู้สึกว่า “ชนะเพราะเล่นดี”
3. ทำ structured playtest แล้วใช้ผลจริงตัด scope

---

## 1. จุดแข็งที่ควรรักษา

- Reverse Tower Defense แตกต่างจาก TD มือถือทั่วไป
- Async base raid เหมาะกับทีมเล็กกว่าการเริ่มด้วย live PvP
- ฐานผู้เล่นเป็นทั้งบ้าน, idle economy และเป้าหมายให้คนอื่นบุก ทำให้ระบบเชื่อมกันได้
- Monster ใช้ได้ทั้งบุกและ garrison ทำให้ content หนึ่งชิ้นมีมูลค่าหลายทาง
- Faction มี gameplay identity ไม่ใช่เพียงเปลี่ยน skin
- Defense Capacity แยกออกจากความรวย เป็นแนวคิดป้องกัน pay-to-win ที่ดี
- Art direction แบบ cute chibi เข้าถึงตลาดกว้าง และตัดกับธีมฝูงมอนสเตอร์บุกเมืองได้อย่างน่าสนใจ
- Data-driven design และ composite ID เป็นรากฐานที่รองรับ content เพิ่มได้ดี

---

## 2. ความเสี่ยงหลัก: ผู้เล่นอาจ “ดูเกม” มากกว่า “เล่นเกม”

ระบบปัจจุบันเน้นการเลือกการ์ด เลือกเลน รอ build time แล้ว Monster เดิน waypoint และต่อสู้อัตโนมัติ ขณะที่ Miner ทำงานอัตโนมัติและผู้เล่นเลื่อนกล้องดูเหตุการณ์

หากการตัดสินใจสำคัญมีเพียง “ลงตัวไหน เลนไหน” เกมอาจกลายเป็น autobattler ที่มีขั้นตอนเพิ่ม แต่ไม่มีความลึกพอให้เล่นซ้ำ 20–30 รอบ

### สิ่งที่ควรเพิ่มเพื่อสร้าง Player Agency

- Spell หรือ Commander Ability จำนวน 2–3 ปุ่มต่อแมตช์
- Rally: เร่งโจมตี เปลี่ยนเป้าหมาย หรือย้ายแรงกดดันไปอีกเลน
- Sacrifice: สังเวยยูนิตเพื่อระเบิด เปิดทาง หรือเพิ่มพลังชั่วคราว
- Retreat/Recast: ถอนคำสั่งที่ยังไม่เข้าปะทะและคืน resource บางส่วน
- Breach Choice: เมื่อเจาะแนวป้องกันสำเร็จ ให้เลือกโบนัสหนึ่งจากสาม
- Tactical pause ชั่วครู่ตอนเลือก reward ใน PvE
- Scout ก่อน raid แล้วเลือกจุดเริ่มหรือ modifier

เป้าหมายเชิงออกแบบคือ ทุกประมาณ 5–10 วินาที ผู้เล่นควรมีสิ่งที่ “อยากตัดสินใจ” ไม่ใช่เพียงสิ่งที่ “กดได้”

---

## 3. Core Loop ที่แนะนำ

หนึ่ง session ควรสั้นและชัด:

1. เห็นเป้าหมายสามฐาน พร้อมความเสี่ยงและรางวัล
2. Scout ฐานอย่างรวดเร็ว
3. เลือก squad 5 ใบ และ spell 2 ใบ
4. Raid 2–3 นาที
5. ตัดสินใจเรื่องจังหวะ เลน และความสามารถพิเศษระหว่าง raid
6. ได้ผลลัพธ์ที่ทำให้รู้สึกว่า “เกือบชนะ” หรือ “build นี้ใช้ได้ผล”
7. เลือก upgrade/draft หนึ่งอย่าง
8. เริ่ม raid รอบถัดไปได้ทันที

หลังจบ raid ปุ่ม “เล่นต่อ” ควรพาผู้เล่นเข้าสู่การตัดสินใจสนุกครั้งถัดไปภายในไม่กี่วินาที ไม่ควรบังคับกลับเมือง ผ่านหลายหน้าจอ หรือเก็บ resource หลายชนิดทุกครั้ง

---

## 4. Mining Economy: มีเอกลักษณ์ แต่เสี่ยงทำเกมช้า

Miner แบบขุด–แบก–กลับฐานมีจุดแข็ง:

- สร้าง economic warfare
- ระยะทางมีผลต่อรายได้
- เปิดเป้าหมายรองนอกเหนือจาก Fort

แต่สำหรับ async raid ที่ฝ่ายรับไม่ได้ควบคุมสด มีความเสี่ยงดังนี้:

- ช่วงต้นเกมต้องรอรายได้
- ผู้เล่นเสียโดยไม่เข้าใจว่าเศรษฐกิจพังตรงไหน
- Miner กลายเป็นภาระที่ต้องเฝ้ามากกว่าความสนุก
- แมตช์ 2–4 นาทีอาจสั้นเกินไปสำหรับ economy arc เต็มรูปแบบ

### แนวทางทดลอง

- ให้ทองตั้งต้นพอ deploy ชุดแรกได้ทันที
- ให้ Miner เพิ่มรายได้รอบถัดไป แทนที่จะเป็นคอขวดของ action แรก
- แสดง income pulse ที่อ่านง่าย เช่นทุก 10 วินาที
- แสดงเส้นทาง Miner และทองที่กำลังแบกให้ชัด
- ทดลองสอง variant:
  - A: carry-and-return แบบปัจจุบัน
  - B: passive income และยึดเหมืองกลางเพื่อเพิ่มรายได้
- หากแบบ B สนุกเร็วกว่าอย่างชัดเจน ให้ลด Miner เหลือ objective รอง

ไม่ควรรักษาระบบ Miner เพียงเพราะมีโค้ดแล้ว หากข้อมูล playtest แสดงว่ามันทำให้ time-to-fun ยาวขึ้น

---

## 5. Roguelite กับ Async Raid ยังขัดกันบางส่วน

เอกสารกล่าวถึงทั้ง draft แบบสุ่ม, ArmyPreset, faction loadout, ทีมผสม faction และกติกาหนึ่ง faction ต่อหนึ่ง raid จึงยังไม่ชัดว่าความสนุกหลักมาจากการเตรียม deck ก่อนรบ หรือแก้สถานการณ์จากของสุ่มระหว่าง run

### โมเดลที่แนะนำ

- ก่อน raid: เลือก squad 5 ใบจาก collection ของ faction เดียว
- ระหว่าง raid: จั่วจาก squad นี้ ไม่สุ่มจาก collection ทั้งหมด
- หลังชนะ: เลือก mutation/relic หนึ่งจากสามสำหรับ run ปัจจุบัน
- เมื่อจบ run: mutation หาย แต่ mastery/unlock บางส่วนอยู่ต่อ

โมเดลนี้ทำให้ผู้เล่นรู้สึกว่าแพ้เพราะการตัดสินใจ ไม่ใช่เพราะได้ deck ที่ใช้ไม่ได้ และยังรักษารสชาติ roguelite

---

## 6. Faction และ Content กว้างเกินไปสำหรับ MVP

แผนปัจจุบันมี 8 faction × 5 tier รวม tower, monster, weapon, shield และ upgrade หลายระดับ ขณะที่ gameplay จริงยังไม่ผ่าน playtest end-to-end อย่างเป็นระบบ นี่คือความเสี่ยงสูงสำหรับ solo developer

### MVP ที่เหมาะสมกว่า

- 2 faction
- Faction ละ 5–6 monster
- Tower รวม 5–6 แบบ
- Spell รวม 4 แบบ
- 1 environment
- 3 รูปแบบฐาน
- แต่ละ unit มี upgrade เชิงกลไกสองทาง แทนห้าระดับที่เพิ่มเพียง stat และความอลัง

### Faction คู่แรกที่แนะนำ

- Beast: เข้าใจง่าย เน้น brute force
- Undead หรือ Galax: เล่นตรงข้ามชัด เช่น sustain หรือ utility

Human แบบเก่งรอบด้านผลิตง่าย แต่ไม่ใช่ faction ที่ดีที่สุดในการพิสูจน์ความสนุก เพราะ hook ไม่แรง หากใช้ทดสอบ art pipeline ควรทำเพียงหนึ่งตัวก่อน

---

## 7. Progression ควรให้ “ของเล่นใหม่” มากกว่า “เลขสูงขึ้น”

Tier T1–T5 มีแนวโน้มกลายเป็น vertical power: HP, damage, cost และความอลังเพิ่มขึ้น ผลคือ content เก่าหมดคุณค่าและ matchmaking ยากขึ้น

ควรใช้ sidegrade และ mechanic upgrade เช่น:

- Raptor: เลือกระหว่างโจมตีเร็วเมื่ออยู่เป็นฝูง หรือกระโดดข้ามป้อมแนวแรก
- Golem: รับกระสุนแทนเพื่อน หรือตายแล้วแตกเป็นสองตัวเล็ก
- Healer: ฮีลเป้าหมายเดียวแรง หรือฮีลเป็นพื้นที่
- Tower: เลือก anti-swarm หรือ anti-elite

สิ่งนี้ทำให้ผู้เล่นอยากทดลอง build มากกว่าการไล่ตัวเลขสูงสุดเพียงอย่างเดียว

---

## 8. Retention: ทำอย่างไรให้ผู้เล่นอยากเล่นอีกรอบ

เกมควรสร้างแรงดึงกลับสี่ชั้น:

### Moment-to-Moment

- จังหวะปล่อยฝูง
- Skill timing
- ภาพและเสียงตอนฐานแตก
- Hit feedback และ unit reaction ที่อ่านง่าย

### Match-to-Match

- ความรู้สึก “เกือบชนะ”
- ฐานรูปแบบใหม่
- ทดลอง counter หรือ build ใหม่
- เลือกความเสี่ยงกับรางวัล

### Session-to-Session

- ปลด mutation, unit หรือ strategy ใหม่
- มีเป้าหมายระยะสั้นที่จบได้ในหนึ่ง session
- มี mastery ที่สะท้อนความเชี่ยวชาญ ไม่ใช่เพียงเวลาหรือเงิน

### Social

- Revenge
- Defense report/replay
- Copy หรือ share base
- Faction challenge

### ระบบที่ควรทำก่อน Idle Economy แบบลึก

Defense Replay Summary ควรบอกว่า:

- ฐานถูกตีจากด้านใด
- ป้อมใดสร้าง damage มากที่สุด
- จุดใดแตกก่อน
- ผู้บุกใช้ unit ใด
- มีปุ่มแก้ฐานและปุ่มแก้แค้น

แม้ยังไม่มีวิดีโอ replay จริง ก็ใช้ heatmap เส้นทาง และ event summary ได้ ซึ่งสร้างความผูกพันกับฐานมากกว่าข้อความ “ถูกปล้น 500 ทอง”

### Streak Loop ที่น่าทดลอง

- ชนะ raid แล้วเลือก mutation
- ชนะต่อแล้ว reward multiplier เพิ่ม หรือพบฐาน elite
- แพ้แล้วยังเก็บบางส่วน พร้อม counter suggestion
- ผู้เล่นเลือกเองว่าจะ cash out หรือเสี่ยงต่อ

ระบบนี้สร้างความรู้สึก “อีกตาเดียว” ได้ดีกว่า idle timer เพียงอย่างเดียว

---

## 9. Business Analysis

### Target Audience ที่ควรโฟกัส

- ผู้เล่นมือถือที่ชอบ strategy แบบ session สั้น
- ชอบ Clash-style base raid แต่ไม่ต้องการการควบคุมซับซ้อน
- ชอบทดลอง deck/build
- ไม่ควรพยายามจับสาย idle, hardcore deckbuilder, TD และ competitive PvP พร้อมกันใน MVP

### Product Promise

> “สร้างฝูงมอนสเตอร์ แล้วหาทางเจาะฐานที่ผู้เล่นคนอื่นออกแบบไว้”

ประโยคนี้ชัดและขายง่ายกว่า “Reverse TD + Roguelite Deckbuilder + Idle Meta + PvP” เพราะผู้เล่นเห็นภาพการเล่นทันที

### Monetization ที่เข้ากับเกม

- Cosmetic monster evolution
- Base theme และ decoration
- Spell/tower VFX
- Battle pass ที่เน้น cosmetics และ challenge
- Extra preset slot หรือ convenience ที่ไม่เพิ่ม combat power
- Rewarded ad หลังแพ้เพื่อเก็บ reward บางส่วน หรือ reroll เป้าหมาย โดยจำกัดความถี่

### สิ่งที่ควรหลีกเลี่ยง

- ขาย Defense Capacity
- ขาย stat ตรง
- ขาย faction ที่แข็งกว่าอย่างชัดเจน
- บังคับรอ build time แล้วขาย skip ตั้งแต่ช่วงพิสูจน์เกม
- ทำ warp/world-map monetization ก่อนรู้ว่าการเลือกเป้าหมายสนุกจริง

### สมมติฐานทางธุรกิจที่ต้องพิสูจน์

1. ผู้เล่นเข้าใจ fantasy ของการเป็นฝ่ายบุกได้ทันที
2. การบุกฐานที่คนอื่นสร้างมีความหลากหลายจริง
3. ผู้เล่นผูกพันกับ collection และฐานของตนเอง
4. การแพ้สร้างแรงอยากแก้มือ ไม่ใช่ความรู้สึกว่าโดน stat หรือ RNG เอาเปรียบ
5. Content cadence ที่ทีมทำไหวเพียงพอต่อการรักษาความสดใหม่

---

## 10. Project Analysis

Repository มีระบบและ asset จำนวนมากแล้ว เช่น scripts ประมาณ 83 ไฟล์, network architecture, async base builder, multi-faction profile, wallet/checkout, spell/projectile/supporter และ art/VFX หลายส่วน ขณะที่ checklist หลายรายการยังต้องประกอบใน Unity Editor และยังไม่เห็นบันทึก structured playtest 20–30 รอบ

นี่เป็นลักษณะ **production inversion**: foundation กว้าง แต่ playable proof ยังบาง

### งานที่ควร Freeze ชั่วคราว

- Faction เพิ่ม
- World map
- Live PvP และ N:1
- Crafting/hatching แบบลึก
- Monetization integration
- Art variant จำนวนมาก
- Tier 5 แบบเต็ม
- การตัดสินใจเรื่อง server hosting

### Milestone ใหม่: Fun Proof Build

ควรผ่านเงื่อนไขต่อไปนี้:

- เปิดเกมจนเริ่ม raid ได้โดยไม่มี dev setup
- เล่นจบและเริ่มรอบใหม่ได้
- มี unit ที่มีหน้าที่ต่างกันจริงอย่างน้อยห้าตัว
- มี tactical action ระหว่างเกมอย่างน้อยสองแบบ
- มีฐานอย่างน้อยสาม layout ที่บังคับให้เปลี่ยนแผน
- Feedback หลักครบ: hit, death, gold, cooldown, win/lose
- ผู้เล่นใหม่เข้าใจเป้าหมายโดยไม่ต้องอธิบาย
- ผู้ทดสอบบางส่วนเลือกเล่นต่อเองเมื่อไม่มีรางวัลบังคับ

### Definition of Done ที่ควรใช้

งาน gameplay ยังไม่ควรถือว่าเสร็จเพียงเพราะ code complete แต่ต้องมี:

1. ประกอบใน scene/prefab แล้ว
2. เล่น end-to-end ได้
3. มี feedback ที่ผู้เล่นอ่านได้
4. ผ่าน edge cases ขั้นพื้นฐาน
5. มี playtest note หรือ metric รองรับ

---

## 11. Game Engineering Analysis

### สิ่งที่วางรากฐานไว้ดี

- แยก data ด้วย ScriptableObject
- แยก RaidSide ออกจาก faction ถูกแนวคิด
- Snapshot raid ลด infrastructure cost
- BuildGrid ใช้ร่วมระหว่าง build mode กับ runtime
- Server authority ถูกเตรียมไว้สำหรับอนาคต
- Save แยก faction และ composite ID ลด migration pain

### จุดที่ควรปรับ

#### 11.1 แยก Gameplay Simulation ออกจาก Networking

NGO ทุกระบบตั้งแต่ PvE prototype เพิ่มความซับซ้อนในการ debug และ Editor assembly ควรรักษา server-authoritative architecture แต่แยก combat/economy rules เป็น plain C# service หรือ simulation layer แล้วใช้ NGO เป็น adapter

ประโยชน์:

- เขียน automated test ได้ง่าย
- รัน balance simulation โดยไม่เปิด scene
- เปลี่ยน network mode โดยไม่แตะกติกาเกม
- ลดปัญหา presentation หรือ prefab ทำให้ gameplay test ไม่ได้

#### 11.2 เพิ่ม Automated Tests

อย่างน้อยควรมี EditMode tests สำหรับ:

- Damage และ armor
- Gold transaction
- Defense Capacity
- Grid occupancy
- Reward/loot calculation
- Snapshot serialize/deserialize
- Faction ID resolution
- Win/lose edge cases

#### 11.3 สร้าง Save Repository Abstraction

PlayerPrefs เหมาะกับ prototype แต่หลายระบบเริ่มพึ่งมัน ควรมี interface กลางสำหรับ profile, wallet, base layout และ progression เพื่อให้เปลี่ยนไป local JSON/cloud backend ได้โดยไม่รื้อ gameplay

#### 11.4 ลด Scene/Prefab Assembly Debt

ควรทำ validator/editor tool เพื่อตรวจ:

- Reference หาย
- Prefab ที่ควรมีแต่ไม่มี NetworkObject
- ID ซ้ำหรือว่าง
- Faction registry ไม่ครบ
- Scene ไม่มี manager ที่จำเป็น
- ScriptableObject อ้าง prefab ผิดชนิด
- Layer, collider หรือ NavMesh setup ผิด

#### 11.5 Mobile Performance

ถ้าจะรองรับ Swarm ควรวัดบนเครื่องระดับกลางตั้งแต่ prototype:

- ใช้ object pooling
- จำกัด projectile และ VFX budget
- หลีกเลี่ยงการ scan object ทั้ง scene ทุก tick
- กระจาย AI update ไม่ให้ทุก unit คิดพร้อมกัน
- ใช้ profiler กับจำนวนยูนิตสูงสุดที่ตั้งใจให้เกิดจริง
- ทดสอบ thermal throttling และ memory ไม่ใช่ดูเพียง FPS ช่วงสั้น

#### 11.6 แยก Gameplay กับ Presentation

Animation หรือ VFX ที่หายต้องไม่ทำให้ combat state พัง และ simulation ควรเร่งเวลาได้สำหรับ balance test

#### 11.7 ทำเอกสาร Design Authority เพียงฉบับเดียว

เอกสารปัจจุบันมีข้อมูลต่างเวอร์ชันปะปน เช่น:

- Roadmap ช่วงต้นยังกล่าวถึง mana และ team HP pool
- Architecture ใหม่ใช้ gold/miner และ async 1:1
- เอกสาร faction บอกว่าผสม faction ได้ แต่อีกส่วนกำหนดหนึ่ง faction ต่อ raid
- PvP จำนวนผู้เล่นและบทบาทมีทั้งแนวคิดเก่าและใหม่

ควรมี `game-design-current.md` หรือ equivalent เป็น source of truth และ mark ส่วนเก่าว่า superseded เพื่อป้องกัน implementation ผิดรุ่น

---

## 12. แนวเกมควรเปลี่ยนไหม

ไม่ควรเปลี่ยนจาก Reverse Tower Defense เพราะเป็นจุดแตกต่างที่แข็งแรงที่สุด

แต่ควรลดคำจำกัดความ MVP จาก:

> Reverse TD + Roguelite Deckbuilder + Idle Meta + Base Builder + Async PvP + Live PvP

เหลือ:

> **Reverse TD tactical raider ที่มี light deckbuilding และ async player bases**

สิ่งที่ควรเพิ่มคือ tactical agency ส่วนสิ่งที่ควรลดคือ idle complexity, จำนวน faction และ infrastructure ที่ทำล่วงหน้า

---

## 13. แผน 4–6 สัปดาห์ที่แนะนำ

### สัปดาห์ 1: End-to-End Loop

- Freeze feature ใหม่
- ทำเส้นทาง Main Menu → Target → Raid → Result → Raid Again
- ใช้ 1 faction, 5 unit, 3 base layouts
- วัด loading time และเวลาจนถึง action แรก

### สัปดาห์ 2: Tactical Agency และ Game Feel

- เพิ่ม spell/rally tactical actions สองแบบ
- ทำ combat feedback ให้ตีแล้วสะใจ
- ทำ economy variant ที่เร็วขึ้น
- ทดลองความยาวแมตช์ 90, 150 และ 240 วินาที

### สัปดาห์ 3: Internal Structured Playtest

- เล่นอย่างน้อย 30 รอบ
- บันทึก unit ที่ใช้ จุดแพ้ เวลาของการตัดสินใจ และช่วงที่ต้องรอ
- ตัดหรือปรับระบบที่ไม่สร้าง decision หรือ spectacle

### สัปดาห์ 4: New-Player Test

- ทดสอบกับผู้เล่นใหม่ 5–10 คน
- ไม่อธิบายก่อนเล่น
- สังเกตว่าเข้าใจเป้าหมายหรือไม่ อ่าน resource ได้ไหม และเลือกเล่นต่อเองหรือไม่

### สัปดาห์ 5–6: Iterate จากข้อมูล

- ปรับเกมตามข้อมูล ไม่ใช่ตามจำนวน feature ใน roadmap
- เพิ่ม faction ที่สองเมื่อ faction แรกสนุกแล้ว
- ทำ meta progression เวอร์ชันบาง
- จากนั้นจึงตัดสินใจว่าจะลงทุนต่อด้าน content, retention หรือปรับ economy

---

## 14. Playtest Framework ที่ควรใช้

### ต่อหนึ่ง Raid ให้บันทึก

- ระยะเวลารวม
- เวลาจนถึงคำสั่งแรก
- จำนวนการตัดสินใจที่มีความหมาย
- เวลาที่ผู้เล่นต้องรอโดยไม่มีสิ่งอยากทำ
- Card/unit ที่ใช้และไม่ได้ใช้
- สาเหตุที่คิดว่าชนะหรือแพ้
- ช่วงเวลาที่สนุกที่สุด
- ช่วงเวลาที่น่าเบื่อหรือสับสนที่สุด
- หลังจบเลือก Raid Again หรือหยุด

### คำถามหลัง Session

- อธิบายเกมนี้ด้วยประโยคเดียวว่าอย่างไร
- ตอนแพ้ รู้หรือไม่ว่าควรทำอะไรต่างออกไป
- Unit ใดจำได้และเพราะอะไร
- มีช่วงใดที่รู้สึกว่ากำลังดูมากกว่ากำลังเล่น
- ถ้าเล่นต่อ อยากได้ของใหม่หรืออยากทดลองแผนใหม่อะไร
- หากไม่มี daily reward ยังอยากกลับมาเล่นหรือไม่ เพราะอะไร

### สัญญาณว่าควร Iterate ก่อนเพิ่ม Content

- ผู้เล่นไม่เข้าใจว่าทำไมแพ้
- ใช้ unit เดิมซ้ำโดยไม่ต้องอ่านฐาน
- มีช่วงรอนานในแมตช์สั้น
- กล้องทำให้พลาดข้อมูลสำคัญ
- ความต่างของ unit คือ stat มากกว่าวิธีใช้
- ผู้เล่นเล่นต่อเพราะ reward เท่านั้น ไม่ใช่เพราะอยากทดลองกลยุทธ์

---

## 15. ลำดับความสำคัญแบบ Must / Should / Later

### Must — ก่อนเพิ่ม Content

- End-to-end raid loop
- Tactical agency ระหว่าง raid
- Game feel และ readability
- Three meaningful base layouts
- Economy pacing test
- Structured playtest
- Basic automated rule tests
- Scene/prefab validation

### Should — หลัง Core Fun ผ่าน

- Faction ที่สอง
- Light roguelite mutation
- Defense report และ revenge flow
- Meta progression แบบ sidegrade
- Analytics funnel
- Save abstraction

### Later

- Faction 3–8
- World map
- Live PvP
- N:1 mode
- Ranked/MMR
- Deep crafting/hatching
- Heavy monetization
- Full tier content scale-out

---

## Final Recommendation

Splice ไม่ได้ขาดไอเดีย แต่มีไอเดียและระบบมากเกินหลักฐานความสนุก ณ จุดปัจจุบัน

ทิศทางที่เหมาะสมที่สุดคือรักษา Reverse Tower Defense และ async base raid ไว้ แล้วทำให้การ raid เป็นประสบการณ์ที่:

- เข้าใจเร็ว
- เริ่ม action เร็ว
- มีการตัดสินใจต่อเนื่อง
- แพ้แล้วรู้ว่าจะแก้มืออย่างไร
- ชนะแล้วอยากลอง build หรือฐานที่ยากกว่า

เมื่อ greybox ทำให้คนอยากกด Raid Again ได้โดยยังไม่ต้องพึ่ง daily reward, art จำนวนมาก หรือ progression ที่บังคับ เกมจึงพร้อมสำหรับการขยาย faction, meta, live operations และ monetization ต่อไป

---

## เอกสารที่ใช้ประกอบการวิเคราะห์

- `CODEX.MD`
- `README.md`
- `technical-architecture.md`
- `splice-development-roadmap.md`
- `splice-faction-design.md`
- `conceptArt-Natural.md`
- `conceptArt-Map.md`
- `AGENTS.md`
- `CLAUDE.md`

