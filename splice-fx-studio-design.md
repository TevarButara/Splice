# Splice FX Studio — Architecture & Workflow

วันที่: 29-07-26
เวอร์ชัน: 0.1.0

## เป้าหมาย

`Splice FX Studio` เป็น Unity Editor package สำหรับสร้าง Skill VFX แบบ data-driven โดยผู้ทำคอนเทนต์ไม่ต้องลาก Node ใน Visual Effect Graph ใหม่ทุก Skill

เครื่องมือไม่แก้ไฟล์ `.vfx` หรือ YAML ของ Graph โดยตรง แต่ใช้:

1. Graph/Prefab Template ที่ผ่านการทดสอบ
2. Exposed Property contract
3. SubFX assets
4. Blend Sequence
5. Execution Stage Binding
6. Generated pooled prefabs

Gameplay, Damage, Target และ Hit Timing ยังคงตัดสินโดย `HeroAbilityExecutionSO` ฝั่ง authoritative ส่วน Studio รับเฉพาะ presentation event

## ตำแหน่งไฟล์

```text
Packages/com.splice.fxstudio/
  Runtime/       data contracts + runtime sequence/property driver
  Editor/        Studio window + alpha processor + exporter + validator
  Tests/Editor/  EditMode regression

Assets/SpliceFXStudio/
  Presets/       preset assets และ registry
  Templates/     prefab template ที่แก้ไข/เปลี่ยนเป็น VFX Graph ได้
  Materials/     shared starter material
  Authoring/     SubFX, Blend และ Skill FX Package ที่ผู้ใช้สร้าง
  Generated/     texture/prefab ที่ Studio สร้างใหม่ได้
```

กฎสำคัญ: Exporter เขียนได้เฉพาะ path ที่มี `/Generated` และไม่เขียนทับ source image, source Graph, authored prefab หรือ authoring asset

## เปิดใช้งาน

Unity menu:

```text
Splice > FX Studio > Open Studio
```

หน้าต่างมีห้าแท็บ:

1. `Create`
2. `SubFX Lab`
3. `Blend Timeline`
4. `Bind & Export`
5. `Validate`

## Starter Presets

ชุดเริ่มต้นติดตั้งแล้ว:

- Ground Ring / Magic Circle
- Impact / Explosion
- Dash Trail
- Projectile
- Beam / Lightning
- Orbiting Objects

Starter ใช้ URP fallback prefab ที่เล่นได้จริง ผู้ใช้สามารถเปลี่ยน `templatePrefab` ของ Preset เป็น prefab ที่มี Visual Effect Graph ได้โดยไม่แก้ระบบ Studio

การเพิ่ม Preset ใหม่:

1. สร้าง Graph/Prefab Template
2. Expose property ที่ต้องการ
3. Create `Splice/FX Studio/Preset`
4. ตั้ง stable `presetId`
5. กำหนดชื่อ exposed properties และ mobile budget
6. เพิ่ม asset เข้า `SpliceFxPresetRegistry`

## Stable Property Contract

ชื่อมาตรฐาน:

```text
MainTexture
MainColor
Emission
Lifetime
SpawnRate
Speed
Size
Radius
RotationSpeed
NoiseStrength
```

Custom property รองรับ:

- Float
- Int
- Bool
- Vector2/3/4
- Color
- Texture
- Gradient
- AnimationCurve

ถ้า Graph ไม่ expose property ใด Property Driver จะข้าม property นั้นอย่างปลอดภัย

## Alpha Processor

รองรับ:

- Source Alpha
- Luminance to Alpha
- Chroma Key
- R/G/B/A Channel Mask
- Invert
- Threshold/Feather
- Chroma tolerance/softness/despill
- Resize สูงสุดก่อน export

ผลลัพธ์:

- สร้าง PNG RGBA ใหม่
- Source ไม่ถูกแก้
- Clamp
- No Mipmap
- ASTC 6x6 สำหรับ Android/iOS
- Default maximum 1024

## Blend Sequence

แต่ละ Clip เก็บ:

- SubFX reference
- Start time
- Duration
- Position
- Rotation
- Scale
- Loop
- Low/Medium/High quality mask

Exporter ประกอบทุก Clip เป็น prefab เดียวที่มี `SpliceFxSequenceRuntime` แล้ว Timeline จะเปิด/ปิด Layer ตามเวลาเมื่อ prefab ถูกเรียกจาก `VfxPoolService`

เวอร์ชันนี้ไม่ merge Graph หลายไฟล์เข้าด้วยกัน เพื่อลดความเสี่ยงจาก Unity/VFX Graph update และทำให้แก้ไข/rebuild ได้ deterministic

เมื่อ Export สำเร็จ ระบบจะลงทะเบียน final sequence และ skill package เข้า Addressables ด้วย address คงที่:

```text
splice-fx/sequence/{sequenceId}
splice-fx/package/{packageId}
```

จึงนำ VFX ชุดใหม่เข้า remote content catalog ภายหลังได้โดยไม่ต้องติดตั้งตัวเกมใหม่ หากโปรเจกต์ยังไม่มี Addressables settings ระบบจะแจ้งเตือนและไม่ทำให้การ Export ล้มเหลว

## Skill Execution Binding

Stage มาตรฐาน:

```text
Cast
Launch
Travel
Impact
Persistent
End
Custom
```

แต่ละ Stage กำหนด:

- Blend Sequence
- Delay
- Placement: World, Ground, Hero Root, Hero Effect Anchor
- Scale: Authored, Hero-relative, Ability Cast Range, Effect Radius
- Local offset
- Ground offset
- Orient to direction

`HeroAbilityDefinitionSO.fxStudioPackage` เชื่อม Studio เข้าระบบเดิม โดย exported Studio stage มีสิทธิ์เหนือ legacy cue เฉพาะ stage เดียวกัน Stage อื่นยังใช้ legacy VFX ต่อได้

## Runtime และ Network Safety

- Studio ไม่คำนวณ damage
- Studio ไม่เลือก target
- Studio ไม่แก้ cooldown/mana
- Execution Module/Server ส่ง stage, position, direction และ timing
- Client เล่น VFX ผ่าน Pool เท่านั้น
- Gameplay ยังทำงานแม้ VFX หายหรือถูกปิดใน Low quality

## Validator

ตรวจ:

- ID ซ้ำ/ว่าง
- Preset/Template หาย
- Template ไม่มี visual component
- VisualEffect ไม่มี Graph
- Missing MonoBehaviour script
- Particle capacity
- Renderer count
- VisualEffect component count
- Lifetime budget
- Texture resolution
- Estimated ASTC 6x6 memory
- Empty quality mask
- Custom property ซ้ำ/ไม่มีชื่อ
- Blend clip ไม่มี SubFX
- Skill stage ซ้ำ/ไม่มี sequence/ยังไม่ export

FX Studio Validator ถูกเชื่อมเข้า `Splice Content Validator` และ build validation แล้ว

## Automated Tests

เมนู:

```text
Splice > FX Studio > Run EditMode Tests
Splice > FX Studio > Run PlayMode Regression
```

ครอบคลุม:

- Luminance alpha
- Chroma key/despill boundary
- Blend duration
- Invalid clip regression code
- Deterministic stage lookup
- Exporter ปฏิเสธ path นอก Generated
- Hero Ability รับ exported Studio package และเข้า staged VFX path

## ข้อจำกัด v0.1

- Starter template เป็น URP fallback; production Graph ต้องนำมาผูกกับ Preset
- Timeline เป็น clip inspector + visual bar ยังไม่ใช่ drag-resize timeline เต็มรูปแบบ
- ยังไม่ bake หลาย Layer ให้เป็น Graph เดียว เพราะต้อง profile ก่อนว่าจำเป็นจริง
- Audio/Camera shake ยังไม่รวมใน FX package เพื่อไม่ให้ presentation ระบบแรกกว้างเกินไป

## ลำดับการใช้งานกับ Rowan

1. Create Skill FX Package `Rowan Ultimate`
2. สร้าง SubFX: Rune Circle, Sword, Dash Trail, Impact X, End Collapse
3. Process Alpha ของภาพแต่ละชิ้น
4. สร้าง Blend แยกตาม Stage
5. Bind Blend เข้า Cast/Travel/Impact/End
6. เลือก `Skill3-Wildblade Frenzy.asset` ใน Bind tab
7. กด Bind
8. Validate + Export
9. ทดสอบใน RaidArena
