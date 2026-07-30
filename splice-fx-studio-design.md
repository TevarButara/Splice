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

## Live Preview

ทุกแท็บมี Preview Stage อยู่ทางขวาของหน้าต่าง จึงเห็นหน้าตาและสเกลของ Effect ระหว่างปรับค่าโดยไม่ต้อง Export หรือเข้า Play Mode

ความสามารถ:

- Preset tab แสดง template ตัวอย่าง
- SubFX Lab อัปเดตสี, texture, emission, size, radius, speed และ lifetime จาก asset
- Blend Timeline แสดงทุก layer ตาม start/duration/loop
- Bind & Export เลือกดู Cast, Launch, Travel, Impact, Persistent และ End แยก stage
- Play/Pause/Replay และเลื่อน Time slider เพื่อดูเฟรมที่ต้องการ
- สลับ High/Medium/Low เพื่อดู layer ที่จะทำงานจริงในแต่ละระดับเครื่อง
- หมุนกล้องด้วยการลากเมาส์และซูมด้วย scroll wheel
- เปลี่ยนสีพื้นหลังและเปิด Hero Scale wireframe สูง 2 เมตรเพื่อเทียบขนาด
- พื้นกริดหนึ่งช่องเท่ากับหนึ่ง Unity unit

Preview ใช้ `PreviewRenderUtility` และ object แบบ `HideAndDontSave` จึงไม่เพิ่ม GameObject ลง Scene, ไม่ทำให้ Scene dirty และไม่แก้ source prefab

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

## SubFX Motion Stack

ภาพที่นำเข้าเป็น visual source เท่านั้น จึงต้องเพิ่ม Motion หากต้องการให้ขยับ ผู้ใช้สามารถเลือก Motion หลายตัวและผสมกันตามลำดับได้:

- `Spin` หมุนตามแกนและองศาต่อวินาที
- `Pulse` ขยาย/ย่อเป็นจังหวะ
- `Expand` ขยายตามเวลาและ Animation Curve
- `Contract` ย่อลงตามเวลาและ Animation Curve
- `Float` ลอยตามแกน
- `Orbit` เคลื่อนเป็นวงรอบจุดกำเนิด
- `Flicker` กระพริบความสว่าง
- `Fade In` / `Fade Out`
- `UV Scroll` เลื่อน texture โดยไม่ขยับ GameObject
- `Shake` สั่นแบบ deterministic Perlin motion

ค่าที่ปรับได้ขึ้นอยู่กับชนิด Motion ได้แก่ Speed, Amount, Start Delay, Duration, Phase, Loop, Axis, UV Direction และ Motion Curve

Quick FX:

- `Magic Circle` = Spin + Pulse + Fade In
- `Impact Pop` = Expand + Flicker + Fade Out
- `Energy Flow` = UV Scroll + Pulse
- `Floating Aura` = Float + Pulse + Flicker

Motion Stack ทำงานทั้งใน Live Preview และ `SpliceFxMotionPlayer` บน exported pooled prefab จึงไม่ใช่ animation เฉพาะ Editor

## SubFX Instance Layout

SubFX หนึ่งตัวสามารถใช้ภาพหรือ prefab ต้นฉบับเพียงชิ้นเดียวแล้วสร้างเป็นหลาย instance ได้ โดยมีรูปแบบ:

- `Single` หนึ่งชิ้น
- `Radial` วางเต็มวง เช่น ดาบ 5 เล่มรอบวงเวท
- `Arc` วางเป็นแนวโค้ง
- `Line` วางเรียงตามทิศทาง
- `Grid` วางเป็นแถวและคอลัมน์
- `Random Ring` กระจายระหว่างรัศมีในและนอกด้วย seed ที่ให้ผลซ้ำได้
- `Manual` กำหนดตำแหน่ง มุม ขนาด และเปิด/ปิดของแต่ละชิ้นเอง

ค่าหลักประกอบด้วย Count, Center Offset, Base Rotation/Scale, Plane Axis, First Direction, Facing, Radius, Inner Radius, Arc, Start Angle, Spacing, Rotation Step, Scale Step และตำแหน่ง/มุม/ขนาดแบบ jitter

การเคลื่อนไหวแยกเป็นสองระดับ:

- `Motion Stack > Spin/Orbit` ขยับ formation ทั้งชุดรอบจุดกลาง
- `Each Item Spin` หมุนแต่ละชิ้นรอบแกนของตัวเอง และเลือกสลับทิศชิ้นคู่/คี่ได้

`Motion Stack Applies To` เลือกได้ว่าจะให้ Motion Stack ทำงานกับทั้ง formation หรือให้แต่ละ instance เล่น Motion Stack แยกกัน เมื่อเลือก `Each Instance` ระบบจะใช้ `Delay Per Item` เป็น local time ของแต่ละชิ้น จึงสร้างการปรากฏ หมุน ลอย หรือพุ่งเรียงลำดับได้โดยไม่ต้องเขียน execution code ใหม่

Live Preview มีโหมด `Edit` สำหรับแปลง procedural layout ปัจจุบันเป็น Manual โดยรักษาตำแหน่งเดิมทั้งหมด จากนั้นเลือก instance แต่ละชิ้นและใช้ `Move / Rotate / Scale` ได้โดยตรง กด `W / E / R` เพื่อสลับเครื่องมือ, ลาก marker เพื่อปรับแบบเร็ว หรือกรอกค่า Position/Rotation/Scale แบบ XYZ เพื่อความแม่นยำและทำ non-uniform scale ได้ ใช้ Alt/Right-drag เพื่อหมุนกล้อง

ภาพเดี่ยวที่ต้องคงอยู่ เช่น ดาบหรือสัญลักษณ์ ใช้ preset `Static Sprite / Instance Card` ซึ่งใช้ material โปร่งใสแบบ two-sided แยกจาก material กลาง จึงมองเห็นได้จากกล้องทั้งสองด้านโดยไม่เปลี่ยนพฤติกรรม preset อื่น ส่วน Particle preset ใช้เมื่อต้องการ emitter ที่สร้างอนุภาคอายุสั้นหลายชิ้นจริง ๆ

จำนวน instance แยก High/Medium/Low ได้ เช่น 5/4/3 สำหรับมือถือแต่ละระดับ การ Export จะ bake instance ทั้งหมดลง pooled prefab ล่วงหน้า จึงไม่มีการ `Instantiate` ระหว่างกด skill และ Validator จำกัดสูงสุด 64 ชิ้นพร้อมเตือน renderer budget

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
