# Veyra — Comet Launcher

- **ID:** `galax/veyra-comet-launcher`
- **Faction:** Galax
- **Role:** Ranged Artillery / Zone Control
- **Range:** Ranged
- **Damage:** Physical burst / Tech utility
- **Difficulty:** Medium
- **Status:** Draft v0.1
- **Name alias:** Veyra Commet Lancher (ชื่อจาก brief; standardized เป็น **Comet Launcher**)

## Core Fantasy

นักรบดาวหางผู้ใช้หอกเทคโนโลยีและคำนวณวิถีโจมตีล่วงหน้า เธอพุ่งหอกไปตามเส้นทางที่ผู้เล่นลาก ยิ่งหอกเดินทางไกลก่อนปะทะยิ่งสร้างความเสียหายรุนแรง

## Gameplay Loop

1. โจมตีเพื่อสร้าง **Orbital Calibration** บนเป้าหมาย
2. รักษาระยะและลากวิถี **Comet Lance** ให้โดนช่วงปลายทางเพื่อเร่งความเสียหาย
3. ใช้ **Vector Thrusters** ถอยหรือหามุมยิงหอกใหม่
4. ใช้ **Meteor Convergence** ปิดทางหนีหรือบังคับศัตรูออกจากพื้นที่สำคัญ

## Ability Summary

| Slot | Ability | Type | Purpose |
|---|---|---|---|
| Skill 1 | Comet Lance | Active / Directional | พุ่งหอกเป็นเส้นตรง ยิ่งโดนไกลยิ่งแรง |
| Skill 2 | Orbital Calibration | Passive | ทำเครื่องหมายเป้าหมายและเพิ่มโอกาส Critical ของหอก |
| Skill 3 | Vector Thrusters | Active / Mobility Buff | รักษาระยะและสร้างโล่ชั่วคราว |
| Skill 4 | Meteor Convergence | Ultimate / Ground Target | ควบคุมพื้นที่และ burst เป็นระลอก |

## Skill 1 — Comet Lance

Veyra พุ่งหอกดาวหางที่ถืออยู่ไปตามทิศทางที่ผู้เล่นลาก หอกเดินทางเป็นเส้นตรงด้วย **ระยะตายตัว** และสร้าง Physical Damage แก่ศัตรูตัวแรกที่ปะทะ ความเสียหายเพิ่มตามระยะทางที่หอกเดินทางก่อนโดนเป้าหมาย

การลากกำหนดเฉพาะ **ทิศทาง** ไม่ได้กำหนดระยะ จึงปล่อยสกิลบนมือถือได้รวดเร็วและคาดเดาระยะสูงสุดได้เสมอ

| Property | Initial Tuning |
|---|---|
| Fixed travel distance | 10 m |
| Minimum damage | 70 / 105 / 140 / 175 + 65% Bonus Attack |
| Distance scaling | เพิ่มสูงสุด +100% เมื่อโดนที่ระยะ 10 m |
| Minimum-scaling distance | 2 m แรกยังไม่เพิ่ม damage |
| Projectile behavior | หยุดเมื่อโดน Hero, creep หรือสิ่งกีดขวางชิ้นแรก |
| Cooldown | 8 / 7.5 / 7 / 6.5 s |
| Cost | 50 / 55 / 60 / 65 Energy |

**Damage formula:** `Minimum Damage × (1 + normalized distance)` โดย normalized distance เพิ่มจาก 0 ที่ระยะ 2 m ไปถึง 1 ที่ระยะ 10 m

**Intent:** เป็นท่าโจมตีจากอาวุธ ไม่ใช่เวทเรียกดาวตก ผู้เล่นต้องสร้างระยะและอ่านทิศทางศัตรูเพื่อให้หอกแรงที่สุด ฝ่ายตรงข้ามหลบด้านข้างหรือใช้ยูนิตอื่นบังวิถีได้

## Skill 2 — Orbital Calibration

**Passive.** Basic Attack ของ Veyra ทำให้ Hero หรือ creep ศัตรูติด **Calibration Mark** ชั่วคราว หาก Comet Lance โดนเป้าหมายที่มี Mark หอกจะมีโอกาสเกิด Critical โดยไม่ลบ Mark

| Property | Initial Tuning |
|---|---|
| Comet Lance critical chance | 20% / 30% / 40% / 50% |
| Critical damage | 175% ของ final damage |
| Mark duration | 5 s; Basic Attack ใส่เป้าหมายเดิม refresh ระยะเวลา |
| Valid targets | Enemy Hero และ creep |

**Intent:** เป็น buff ติดตัว ไม่มีปุ่มเพิ่ม และสร้างจังหวะ “ล็อกเป้า → ถอยสร้างระยะ → พุ่งหอก” Critical เป็นโบนัสที่มีโอกาสเกิด ไม่ใช่ damage ที่รับประกันทุกครั้ง

## Skill 3 — Vector Thrusters

Veyra พุ่งระยะสั้นไปทิศที่เลือก ได้รับ Shield ชั่วคราว และ Comet Lance ครั้งถัดไปภายในช่วงบัฟจะเคลื่อนที่เร็วขึ้น สกิลนี้ไม่ข้ามสิ่งกีดขวางหนา

| Property | Initial Tuning |
|---|---|
| Dash distance | 3.5 m |
| Shield | 60 / 100 / 140 / 180 + 40% AP |
| Shield duration | 2.5 s |
| Next Comet Lance projectile speed | +25% |
| Buff window | 4 s |
| Cooldown | 16 / 15 / 14 / 13 s |
| Cost | 65 Energy |

**Intent:** ช่วย reposition แต่ไม่ล้าง CC และไม่ทำให้ Veyra ปลอดภัยจากนักล้วงตลอดเวลา

## Skill 4 (Ultimate) — Meteor Convergence

Veyra เปิดพิกัดวงโคจรขนาดใหญ่ หลังเตือนพื้นที่ 1 วินาที ดาวหาง 3 ลูกตกเป็นระลอก ทุกลูกสร้าง Magic Damage และ Slow ลูกสุดท้ายตกกลางวง สร้างความเสียหายสูงขึ้นและดึงศัตรูเล็กน้อยเข้าหาศูนย์กลางก่อนระเบิด

| Property | Initial Tuning |
|---|---|
| Cast range | 11 m |
| Zone radius | 4.5 m |
| Warning time | 1 s |
| Wave interval | 0.75 s |
| Damage per wave | 100 / 160 / 220 + 35% AP |
| Final-wave bonus | +50% damage |
| Slow | 30% เป็นเวลา 1 s; refresh ต่อ wave |
| Final pull | 1.2 m |
| Cooldown | 90 / 75 / 60 s |
| Cost | 100 Energy |

ศัตรูแต่ละตัวได้รับ Calibration Mark ได้ตามกติกาปกติ แต่ Ultimate ใช้ Mark ได้สูงสุด **หนึ่งครั้งต่อการร่าย** เพื่อกัน burst ที่เกินควบคุม

**Intent:** Ultimate ไม่ได้ล็อกศัตรูให้อยู่ครบทุกระลอกด้วยตัวเอง ต้องอาศัยการวางตำแหน่งหรือ CC จากเพื่อนร่วมทีม

## Strengths

- Poke จากระยะไกลและใช้หอกขวางเส้นทางแคบได้ดี
- ทำ burst ได้ดีเมื่อรักษาระยะและคาดเดาทิศทางศัตรูแม่น
- Ultimate มีผลสูงในการแย่ง objective หรือแบ่งพื้นที่ต่อสู้
- Skill 2 เป็น passive และรองรับทั้ง Hero กับ creep

## Weaknesses & Counterplay

- Comet Lance เป็น projectile เส้นตรง; การหลบด้านข้างหรือให้ creep บังช่วยหยุดหอกได้
- แพ้ Hero ที่ประชิดเร็วหลัง Vector Thrusters ถูกใช้ไปแล้ว
- ไม่มี hard CC ที่เชื่อถือได้เมื่อต่อสู้ลำพัง
- กระจายตัวออกจาก Ultimate และออกจากศูนย์กลางเพื่อลด final-wave damage
- ต้องบริหาร Energy หากยิง Comet Lance ทิ้งบ่อยเกินไป

## Combos

- **Marked thrust:** Basic Attack สร้าง Mark → Vector Thrusters ถอย → Comet Lance จากระยะไกล
- **Lane shot:** ทำ Mark ใส่ creep เป้าหมาย → หามุมไม่ให้ creep ตัวหน้าบัง → Comet Lance ปิดเป้าหมาย
- **Team fight:** Meteor Convergence บังคับทิศทาง → Comet Lance ยิงตามทางออก → เพื่อนใช้ CC ยื้อศัตรูในวง

## Visual & Audio Direction

- อาวุธเป็นหอกกลไกที่กางวงแหวน launcher รอบด้ามก่อนพุ่ง มีแผนที่วิถี hologram สีฟ้า–ม่วง
- เส้นเล็งแสดงระยะตายตัว 10 m และแบ่งสีช่วงใกล้–ไกลเพื่อสื่อ damage scaling
- Calibration Mark เป็น reticle สามเหลี่ยมโคจรรอบเป้าหมาย ไม่บดบัง silhouette
- เสียงดาวหางมี pitch ไล่สูงก่อนตก เพื่อให้ศัตรูตอบสนองได้แม้ไม่ได้มองวงเตือน

## Open Questions for Playtest

- ระยะตายตัว 10 m และความเร็ว projectile ให้โอกาสหลบเหมาะสมบนหน้าจอมือถือหรือไม่
- การที่ creep บัง Comet Lance ทำให้ใช้งานในเลนยากเกินไปหรือไม่
- Critical chance 20–50% สร้างจังหวะน่าตื่นเต้นหรือทำให้ผลลัพธ์สุ่มเกินไป
- Final pull ของ Ultimate รบกวนการควบคุมมากเกินไปหรือไม่
- Energy economy รองรับการ poke โดยไม่ทำให้ spam ได้ตลอดหรือไม่

## Change Log

- 2026-08-15: Reworked Skill 1 เป็นหอกเส้นตรง damage ตามระยะ และให้ Orbital Calibration เพิ่ม critical chance กับ Hero/creep.
- 2026-08-15: Initial ability kit draft v0.1.
