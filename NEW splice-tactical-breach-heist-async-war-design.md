# Splice — Tactical Breach, Heist Raid & Async Base War

**สถานะ:** Game Direction Proposal v0.1  
**วันที่:** 20 กรกฎาคม 2026  
**แนวเกม:** Tactical Reverse Tower Defense + Heist Raid + Async Base War + Hero Action Control  
**แพลตฟอร์ม:** Mobile Portrait · Session 2–4 นาที

---

## 1. High Concept

> ผู้เล่นสร้างเมืองของตนไว้บนโลกจำลอง จัดป้อม Garrison เศรษฐกิจ และทรัพย์ค้ำเมืองเป็น snapshot จากนั้นนำ Hero และกองทัพมอนสเตอร์ไปเลือกเจาะเมืองของผู้เล่นอื่น ขโมยทรัพย์ แล้วตัดสินใจว่าจะถอนตัวพร้อมของที่ได้ หรือเสี่ยงบุกลึกถึง Core เพื่อรับรางวัลทั้งหมด

ผู้เล่นทุกคนมีสองบทบาท:

- **ผู้สร้างและผู้ตั้งรับ:** ออกแบบเมือง วาง Tower, Core, Garrison, Economy และกับดัก
- **ผู้บัญชาการฝ่ายบุก:** จัด Army Preset เลือก Hero และบุก snapshot ของเมืองอื่นแบบ Tactical Breach

ฝ่ายตั้งรับไม่จำเป็นต้องออนไลน์ ระบบโหลด snapshot ล่าสุดมาจำลองการต่อสู้แทนเจ้าของเมือง

## 2. Product Fantasy

เกมต้องขายความรู้สึกสามอย่าง:

1. **นี่คือเมืองของฉัน** — ภูมิใจในผัง การตกแต่ง และแนวป้องกัน
2. **นี่คือกองทัพของฉัน** — collection และ preset สะท้อนวิธีเล่นส่วนตัว
3. **ฉันเป็นผู้นำการบุก** — Hero ทำให้ผู้เล่นอยู่ในสนาม ไม่ได้เพียงดู AI

> **Build your monster city. Lead the breach. Steal the treasure. Escape before everything collapses.**

## 3. Core Design Pillars

### Build a Readable Fortress

เมืองเป็นทั้งพื้นที่สร้างสรรค์และสนามรบ ผู้บุกต้องอ่านจุดแข็ง จุดอ่อน และมูลค่าของเป้าหมายได้ก่อนเดิมพัน

### Command the Breach

ผู้เล่นเลือกทัพ จังหวะ เลน Spell และควบคุม Hero ได้ ฝีมือต้องสำคัญกว่า stat ล้วน

### Steal, Push or Escape

ทุกช่วงของ raid ผู้เล่นเลือกระหว่างถอนพร้อม loot หรือเสี่ยงบุกต่อเพื่อรางวัลสูงขึ้น

### Attack and Defend

ความก้าวหน้าเกิดจากทั้งการสร้างฐานที่ฉลาดและการบุกที่มีฝีมือ

### Fair Stakes

เดิมพันต้องตื่นเต้น แต่ไม่เปิดให้ผู้จ่ายเงินรังแกผู้เล่นอ่อน หรือทำให้ทรัพย์ที่ซื้อด้วยเงินจริงหายโดยไม่สมัครใจ

---

## 4. Player-Owned City

หนึ่งเมืองต่อ faction ที่ปลดล็อกได้ แต่ควรจำกัดเมือง active เพื่อคุม economy และภาระดูแล

### องค์ประกอบเมือง

- **Core:** เป้าหมายสุดท้ายของ raid
- **Tower:** โจมตี สนับสนุน ควบคุมพื้นที่ และปกป้องเศรษฐกิจ
- **Garrison:** Monster ตั้งรับที่โจมตีผู้บุกรุก
- **Economy Buildings:** เหมือง คลัง จุดผลิตทอง และสิ่งสนับสนุน
- **Vault:** เก็บ Raid Stake และ loot ที่ปล้นได้
- **Traps/Utility:** ประตู กับดัก จุดรักษา โล่ และสัญญาณเตือน
- **Army Showcase:** แสดงกองทัพบุกของเจ้าของเมือง
- **Hero Hall:** แสดง Hero ชุด และความสำเร็จ
- **Decoration:** ไม่มีผล combat

### Ready to Defend / Deploy Town

เมื่อกดยืนยัน ระบบต้อง:

1. ตรวจผังและ Defense Capacity
2. ตรวจ Core ทางเข้า และเส้นทางบุก
3. คำนวณ Base Power Rating
4. ให้เลือกทรัพย์ค้ำภายในช่วงที่ระบบกำหนด
5. Preview รางวัลและความเสี่ยง
6. ล็อกทรัพย์ค้ำใน Vault Escrow
7. Commit snapshot เวอร์ชันใหม่
8. นำเมืองเข้าสู่ target pool

ผู้เล่นแก้ฐานเป็น draft ได้ แต่ raid ใช้ committed snapshot เท่านั้น Snapshot ที่ถูกจับคู่แล้วห้ามเปลี่ยนกลาง raid

---

## 5. Garrison และ Army Preset

### Garrison

- ป้องกันเมืองและต่อสู้จริง
- กิน Defense Capacity
- มี guard/patrol position
- ใช้ defensive behavior และ defensive upgrades

### Army Preset

- ใช้สำหรับออก raid
- ใช้ Army Capacity แยกจาก Defense Capacity
- แยก data/loadout จาก Garrison
- จัดได้หลาย preset ตาม slot ที่ปลดล็อก

### Army Showcase

กองทัพบุกยืนแสดงในเมืองได้ แต่เป็น display-only:

- ไม่โจมตี ไม่รับ damage และไม่ block path
- ไม่ spawn เป็นฝ่ายตั้งรับเมื่อถูก raid
- ไม่กิน Defense Capacity
- แสดงเฉพาะ preset ที่เจ้าของตั้ง Public

เพื่อให้ scout มีประโยชน์แต่ไม่เปิดข้อมูลหมด ควรแสดง Hero, unit family/silhouette, Army Power โดยประมาณ และ playstyle เช่น Swarm/Sustain/Siege แต่ซ่อน Spell, ลำดับ deck, relic และ preset อื่น

---

## 6. Hero System — จุดขายหลัก

ผู้เล่นมี Hero Collection หลายตัว แต่เลือกหนึ่งตัวต่อ raid

### หน้าที่

- เป็น avatar ของผู้เล่น
- สั่งฝูงด้วย Rally/Command Ability
- เปิดหีบ เก็บ loot และ activate objective
- ต่อสู้กับ Tower, Garrison และ neutral monster
- เป็นแกน skill expression, collection และ cosmetic

### Auto Mode

- AI เคลื่อนและโจมตีให้
- ผู้เล่นยังใช้ ability/Rally ได้
- เหมาะกับ casual และการเล่นซ้ำ

### Manual Mode

- ลากนิ้วหรือ joystick เคลื่อน Hero
- Basic attack อัตโนมัติในระยะ
- Ability 2–3 ปุ่ม
- หลบ AoE เปิดทาง เก็บ loot และช่วยแนวรบ

### Seamless Takeover

- เริ่ม Auto ได้
- แตะ Hero เพื่อ Manual ทันที
- กด Auto เพื่อคืนให้ AI
- Disconnect แล้ว Hero กลับ Auto เพื่อไม่ให้ raid ค้าง

### Hero ไม่ควร Solo เมือง

- Hero เปิดโอกาสให้กองทัพ ไม่แทนที่กองทัพ
- มี Tower anti-hero และ Garrison ลงโทษการแยกตัว
- Hero ล้มแล้วมี Downed state และ revive ได้จำกัด
- หากล้มถาวร กองทัพเล่นต่อได้ แต่เปิด Vault/ถอน loot ได้ด้อยลง

### Progression

เน้น Outfit, weapon skin, animation, emote, voice และ skill sidegrade หลีกเลี่ยง stat ไร้เพดาน

Archetype ตัวอย่าง: Warlord, Shadow Raider, Beast Rider, Arcanist และ Guardian

---

## 7. Tactical Breach Structure

เมืองแบ่งเป็น 3 Defense Rings

### Ring 1 — Outskirts

Tower เบา, Economy node, neutral monster และ loot เล็ก ใช้อ่าน strategy ของฐาน

### Ring 2 — City Defense

Tower synergy, Garrison หลัก, Trap, ทางแยก และ Vault ย่อย

### Ring 3 — Inner Keep

แนวรับแข็งที่สุด มี Main Vault, Elite Garrison และ Core

### Pre-Raid Flow

1. เลือกเมืองจาก world map/target list
2. ดู Base Rating, Defense Style, Stake, Potential Loot และ Loss on Failure
3. Scout snapshot แบบจำกัดเวลา
4. เลือก Army Preset และ Hero
5. เลือก entry point
6. ยืนยันเดิมพัน
7. เริ่ม raid

### ระหว่าง Raid

- Deploy unit จาก squad/deck
- ควบคุมหรือปล่อย Auto Hero
- ใช้ Hero ability และ tactical spell
- Rally ฝูงไป objective
- ทำลาย defensive ring
- เก็บและ secure loot
- เลือกถอนหรือบุกต่อ

### Breach Reward Choice

เมื่อผ่านแต่ละ ring ให้เลือกหนึ่งอย่าง:

- ฟื้น HP กองทัพ
- ลด cost การ์ด
- Mutation ชั่วคราว
- คืน Hero ability charge
- Secure loot เพิ่ม
- เปิดเผยกับดัก ring ถัดไป

### End Conditions

- **Full Victory:** ทำลาย Core รับ loot สูงสุดและ rating
- **Successful Extraction:** Hero ถอนสำเร็จ รับเฉพาะ loot ที่ secure
- **Defeat:** Hero/army หมดสภาพ เวลาหมด หรือไม่มีทางทำ objective ต่อ

---

## 8. Heist Layer

### Loot States

- **Available:** ยังอยู่ในเมือง
- **Carried:** Hero/porter กำลังแบก ตายแล้วตก
- **Secured:** ส่งถึง extraction cache แล้ว
- **Banked:** raid จบและลง wallet แล้ว

### Extraction

ช่วยให้ผู้เล่นอ่อนยังชนะบางส่วน ลด all-or-nothing สร้าง playstyle ลอบปล้น และทำให้ฐานแข็งยังเป็นเป้าที่สนุก

### Security Escalation

ยิ่งอยู่นาน Alert Level ยิ่งสูง:

- reinforcement ตื่น
- Tower overcharge
- extraction ช้าลง
- Core ส่ง pulse ตรวจจับ Hero

จึงเกิดคำถามหลักทุกตา:

> **“ของเริ่มคุ้มแล้ว—จะหนีตอนนี้ หรือเสี่ยงตี Core เพื่อเอาทั้งหมด?”**

---

## 9. Economy Model

ช่วงแรกควรมีสกุลหลักไม่เกินสามประเภท

### Gold — Soft Currency

ได้จาก economy, world gathering, raid, quest และ PvE ใช้สร้าง/อัปเกรด Tower, Monster, เมือง และของทั่วไป แต่ไม่ซื้อ Defense Capacity โดยตรง

### Diamond — Premium Currency

ได้จาก IAP, achievement/event และ world exploration แบบจำกัด ใช้ cosmetic, convenience ที่มีเพดาน และ collection ที่ไม่ทำให้ combat pay-to-win

### War Gem / Raid Diamond — Stake Currency

**คำแนะนำสำคัญ:** ไม่ควรให้ Premium Diamond ที่ซื้อด้วยเงินจริงถูกผู้เล่นอื่นขโมยจาก wallet โดยตรง ควรแยก Stake Currency ซึ่งหาได้จาก gameplay และ escrow

เหตุผล:

- ลด pay-to-win และ pay-to-recover
- ลดความโกรธจากการเสียของที่ซื้อขณะ offline
- ป้องกันการโอนมูลค่าระหว่างบัญชีและตลาดมืด
- แยก balance raid ออกจากราคา IAP
- ลดความเสี่ยงด้าน store policy/ข้อกำกับ

Premium Diamond อาจใช้ซื้อ Raid Pass ที่ให้สิทธิเข้าร่วม แต่ผลแพ้ชนะไม่ควรโอน Premium Diamond ระหว่างผู้เล่นโดยตรง

---

## 10. Stake & Escrow

### Attacker Stake

ก่อนบุกแสดง Entry Stake, Maximum Loot, Partial Extraction Reward, Loss on Defeat, Power Difference และ difficulty band

เมื่อยืนยัน หัก Stake เข้า escrow ทันที:

- Full Victory: คืน Stake + loot เต็ม
- Extraction: คืนบางส่วน + secured loot
- Defeat: เสีย Stake

### Defender Stake

เมื่อ Ready to Defend:

- ระบบกำหนดช่วง Stake ตาม Base Rating
- เจ้าของเลือกภายในช่วงนั้น
- ล็อกใน City Vault Escrow
- เมืองเข้าสู่ target pool

ผลลัพธ์:

- Defender ชนะ: ได้ Defense Reward/ส่วนหนึ่งของ Attacker Stake
- Attacker ถอน: เสียเฉพาะ loot ที่ถูก secure
- Core แตก: เสีย Stake ตามเพดาน ไม่เสียเมืองหรือ collection

### Guardrails

- ไม่ใช้ winner-takes-all ทั้งหมด
- มี raid tax/sink
- มี insurance และ daily loss cap
- ผู้เล่นใหม่มี shield
- stake จำกัดตาม power band
- reward ไม่ต้องโอนหนึ่งต่อหนึ่งจากผู้แพ้ทั้งหมด

### ตัวเลข Prototype เท่านั้น

| รายการ | ค่าเริ่มทดลอง |
|---|---:|
| Attacker Stake | 10 War Gems |
| Full Victory gross reward | 18 |
| Extraction | 6–12 ตาม secured loot |
| Defeat | เสีย 10 |
| Defender maximum loss | 8 |
| System sink/adjustment | 2 |

ต้อง simulation และ playtest ก่อนใช้จริง

---

## 11. Async Base War

### Defense Report

- ใครมาและใช้ Hero/army style ใด
- Heatmap เส้นทาง
- จุดที่แนวรับแตก
- Tower/Garrison ที่ผลงานสูงสุด
- Loot ที่เสีย/ป้องกันได้
- Edit Base และ Revenge

### Revenge

- ได้ข้อมูลฐานคู่แข่งเพิ่มเล็กน้อย ไม่ใช่ damage boost สูง
- มี cooldown และวันหมดอายุ
- คู่เดิม raid กันซ้ำแล้ว reward ลดลง
- ห้ามวนฟาร์มหรือ harassment

### Protection

- Shield หลัง Core แตก
- Daily loss cap
- Pair cooldown
- Diminishing loot
- Target pool ไม่ให้ค้นบัญชีเจาะจงได้เสรี

### Matchmaking

ใช้ Defense Capacity, normalized level, layout history, attacker army/hero power, win rate และ stake band แสดงเป้าเป็น Safe, Fair และ Risky พร้อม reward ตามความเสี่ยง

---

## 12. Simulated World Map

เป็นโลกจำลอง ไม่ใช้ GPS จริง Server จัด virtual coordinates และเติม bot city เพื่อรักษาความหนาแน่น

### หน้าที่

- แสดงเมืองผู้เล่นและเมือง bot
- ค้นหา raid target
- มีป่า เหมือง dungeon neutral camp และ anomaly
- รองรับ biome/sector/region event

### MVP ที่แนะนำ

ใช้ **sector/node map** ไม่ใช่ shared realtime open world:

- เมืองและกิจกรรมเป็น node
- แตะเพื่อเข้า instance แยก
- ตำแหน่งอัปเดตเป็นรอบ
- ไม่มี avatar จำนวนมากเดินพร้อมกัน

รักษา fantasy โลกเดียวกันแต่ลด server, sync, cheating และ performance cost

---

## 13. Diamond Hunt

ผู้เล่นควบคุม Hero ไปตี neutral monster ตามป่าเพื่อหา rare resource

### รูปแบบแนะนำ

- Diamond Anomaly จำกัดต่อวัน/สัปดาห์
- Encounter PvE 30–90 วินาที
- Manual Control เน้นหลบ AoE, interrupt และ timing
- ได้ Diamond Fragment แน่นอนหรือ progress meter
- Rare full drop เป็นโบนัส
- Fragment ครบจึงแปลงเป็น Diamond/War Gem
- มี weekly cap และแสดงความคืบหน้าชัด

ไม่ควรใช้ drop chance ต่ำอย่างเดียว เพราะจะดูเหมือนบีบให้เติมเงิน World Hunt ควรเป็นพื้นที่ฝึก Hero โดยไม่เสีย Stake ด้วย

---

## 14. Complete Loops

### Minute-to-Minute

Scout → Deploy → Control Hero → Breach → Loot → Decide → Extract/Push

### Session

1. เก็บผลผลิตเมือง
2. ดู Defense Report
3. ปรับฐานหรือ Revenge
4. เลือกเป้าจาก world map
5. Raid 2–4 นาที
6. ใช้ loot ปรับทัพ/เมือง
7. Raid ต่อหรือ World Hunt

### Long-Term

- สะสม Hero/Monster
- ปลด sidegrade
- พัฒนาเมืองและ cosmetic identity
- ไต่ sector/rating
- Seasonal war
- สร้างฐานที่มีชื่อเสียง

---

## 15. แหล่งกำเนิดความสนุก

### Tactical

อ่านฐาน เลือก entry จัด Tank/DPS/Support ควบคุม Hero และใช้ Spell พลิกสถานการณ์

### Emotional

ขโมยแล้วหนีทันเวลา เสี่ยงบุกอีก ring เห็นฐานตัวเองหยุดผู้บุก และแก้แค้นสำเร็จ

### Collection

Hero appearance, Monster composition, Tower style, Base decoration และ build sidegrade

### Social

Visit city, Defense Report, Revenge, regional leaderboard และ base challenge ในอนาคต

---

## 16. UX สำคัญ

### Target Card

แสดงชื่อ/faction, Base Rating, defense archetype, Public Army/Hero preview, Entry Stake, Maximum Loot, Loss on Defeat และ shield state

### Confirm Raid

ต้องตรงไปตรงมา:

> จ่าย 10 War Gems เพื่อเริ่ม Raid  
> ทำลาย Core: รับสูงสุด 18  
> ถอนสำเร็จ: รับตาม loot ที่ Secure  
> แพ้: เสีย Stake 10

### Raid HUD

Alert/Timer, match Gold, Carried/Secured Loot, Hero HP/ability, card hand, Extract button และ minimap สำหรับกล้อง portrait

---

## 17. Monetization

### เหมาะสม

- Hero outfit และ weapon skin
- Base theme/decoration
- Tower projectile/VFX skin
- Emote/victory pose
- Cosmetic battle pass
- Preset convenience
- Rewarded ad สำหรับ PvE boost/target reroll แบบจำกัด

### หลีกเลี่ยง

- ขาย Defense Capacity หรือ stat
- ขาย Hero ที่เหนือกว่าโดยตรง
- ซื้อ Stake เพื่อรังแกผู้เล่นอ่อน
- ขโมย Premium Diamond ที่ซื้อด้วยเงินจริงจากผู้เล่น offline
- ซื้อ shield จนไม่มีวันถูกบุก

> เงินซื้อ identity, variety และ convenience ได้ แต่ไม่ควรซื้อสิทธิ์เอาทรัพย์จากผู้เล่นอื่นง่ายขึ้น

---

## 18. Abuse & Economy Risks

### Self-Farming/Collusion

ห้ามบุกบัญชีเดียวกัน ใช้ pair cooldown, diminishing reward, target pool control, daily transfer cap และตรวจรูปแบบแพ้ให้กันซ้ำ

### Smurfing

Match จาก effective army/base strength ไม่ใช่ level อย่างเดียว และจำกัด stake band

### Offline Loss

ใช้ daily loss cap, shield, insurance, defense performance reward และไม่ทำลาย Tower/Monster ถาวร

### Inflation

ใช้ raid tax, upgrade/cosmetic sink, reward budget และ seasonal reset เฉพาะ rating/token ชั่วคราว

---

## 19. Technical Extension

### Snapshot ควรเพิ่ม

- Snapshot version และ commit timestamp
- Owner/faction ID และ Base Rating
- Tower/Garrison/Economy/Trap
- Vault escrow reference
- Public Army Showcase แบบ display-only
- Public Hero appearance
- Entry points และ navigation validation

### Runtime Entities

- `GarrisonCombatEntity`
- `ArmyShowcaseEntity` แบบ presentation-only
- `RaidHeroEntity` พร้อม Auto/Manual state
- `LootCarrierState` แบบ available/carried/secured/banked
- `RaidEscrowTransaction`

### Hero Authority

Server คุม movement/combat/result Client ส่ง intent การสลับ Auto/Manual ต้อง validate มี rate limit และ disconnect fallback

### Escrow Requirements

- Idempotency key
- Atomic reserve/commit/refund
- Result settle ได้ครั้งเดียว
- Audit log
- Reconnect/resume policy
- ป้องกัน replay reward

### Analytics

Track target impression/selection, raid confirmation, stake reserve, manual control, ring breach, loot carried/secured, extraction, completion, settlement, defense report, revenge และ world hunt

---

## 20. Production Scope

### Prototype A — Fun Proof

- 1 faction, 1 Hero
- 5 Monster, 4 Tower, 2 Garrison
- 3 fixed layouts
- Breach 3 rings
- Auto/Manual Hero
- Carried/Secured loot
- Extract/Core victory
- Fake local War Gem

ไม่ทำ IAP, shared world backend, player matchmaking, multi-city หรือ revenge จริง

### Prototype B — Async Proof

- Player-created snapshot
- Commit/validation
- Bot/player target list
- Defense report
- Mock revenge
- Stake escrow simulation

### Prototype C — Economy Proof

- Server wallet
- War Gem source/sink
- Daily loss cap และ shield
- Strength/stake matchmaking
- Fraud test cases

ผ่านแล้วจึงทำ simulated world sector, Hero collection, faction ที่สอง, cosmetics และ World Hunt

---

## 21. Decisions ที่ต้องล็อก

1. Premium Diamond จะไม่ถูกขโมยโดยตรง — **แนะนำ: ใช่**
2. แยก War Gem จาก Premium Diamond — **แนะนำ: แยก**
3. Hero ล้มแล้วทัพเล่นต่อ — **แนะนำ: เล่นต่อ แต่ถอน loot ยากขึ้น**
4. Extraction — **แนะนำ: ทำได้เฉพาะ checkpoint**
5. ทางเข้าเมือง MVP — **แนะนำ: 2 ทาง**
6. Squad — **แนะนำ: 5 ใบแบบหมุนเวียนคาดเดาได้**
7. Public Army — **แนะนำ: เปิด archetype ไม่เปิด loadout ทั้งหมด**
8. จำนวนครั้งถูกบุก — **ต้องมีเพดานและ shield**
9. Stake — **ผู้เล่นเลือกภายในช่วงที่ระบบกำหนด**
10. World Hunt — **Fragment + progress meter + weekly cap**

---

## 22. Recommended Direction

> **Async Monster-City War ที่การบุกเป็น Tactical Breach Heist ผู้เล่นนำ Hero และกองทัพเจาะเมืองเป็นชั้น ขโมยทรัพย์ แล้วเลือกถอนตัวหรือเสี่ยงตี Core ขณะที่เมืองของทุกคนเป็น snapshot ที่สร้าง ปรับ และนำมาต่อสู้แทนเจ้าของเมื่อ offline**

องค์ประกอบหลัก:

- เมืองผู้เล่นพร้อม Tower, Core, Garrison และ Economy
- Army Preset แยกจาก Garrison แต่แสดงในเมืองแบบ non-combat
- Hero Collection เลือกหนึ่งตัวต่อ raid
- Hero สลับ Auto/Manual ได้
- Tactical Breach สามชั้น
- Loot แบบ carried/secured/banked
- Extraction และ Full Core Victory
- Stake escrow ที่โปร่งใส
- Premium Diamond แยกจาก War Gem
- World map จำลองแบบ sector/node ไม่ใช้ GPS
- World Hunt สำหรับ Hero และ rare fragment
- Defense Report, Revenge, Shield และ anti-harassment

เอกลักษณ์หลักของ Splice ควรตกผลึกอยู่ในคำถามนี้:

> **“ของที่ขโมยมาเริ่มคุ้มแล้ว—จะหนีกลับตอนนี้ หรือเสี่ยงพัง Core เพื่อเอาทั้งหมด?”**

หากทุก raid ทำให้ผู้เล่นรู้สึกกับคำถามนี้ได้ เกมจะมีทั้งความสนุกระยะสั้น ความผูกพันกับเมือง และเหตุผลให้กลับมาเล่นระยะยาว

