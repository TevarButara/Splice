# Changelog

## 0.8.0

- Added first-class Sprite (2D and UI) input for the main SubFX source and every Trail/Particle visual layer.
- Sprite sub-rects are preserved, so a selected sprite inside a sprite sheet or atlas no longer renders the complete backing texture.
- Preview, exported prefabs, Gradient, Stroke and UV Scroll now share the same sprite texture scale/offset.
- Alpha Processor crops the selected sprite region before generating its non-destructive processed texture.
- Texture size and memory validation use the selected sprite region rather than charging the complete atlas.
- Main SubFX motion no longer overwrites visual-layer texture coordinates or colors.
- Added regression coverage for main Sprite sources, sprite-based visual layers, atlas UV rects and motion evaluation.

## 0.7.1

- Fixed spatial gradients being tinted or hidden by Main Color. Solid mode now uses Main Color, while every spatial Gradient mode uses the authored Gradient as the exclusive color source.
- Gradient recoloring preserves source alpha and subtle luminance detail without retaining the source image hue.
- Fixed Stroke sampling when the displayed texture is assigned through a MaterialPropertyBlock by explicitly supplying its texel size.
- Clamped outline sampling at texture edges to prevent texture wrapping from suppressing the stroke.
- SubFX Lab now presents Solid/Gradient as mutually exclusive controls and only displays settings relevant to the active mode.
- Added an in-editor compatibility warning when the selected template material cannot render spatial Gradient or Stroke.

## 0.7.0

- Added Additional Visual Layers inside SubFX Lab.
- A SubFX can contain multiple Trail and Particle layers without creating separate prefabs manually.
- Every layer supports its own image/texture, HDR-style gradient, emission, transform, quality mask and Instance Layout.
- Layer Instance Layouts support multiple copies, stagger, manual placement and independent self-spin like the main sprite-card workflow.
- Added an independent Layer Motion Stack with the same duration-based Spin, Pulse, Float, Orbit, Shake and other motion controls.
- Trail layers expose lifetime, start/end width, vertex distance, texture tiling mode and alignment.
- Particle layers expose continuous/burst emission, shape, loop, mobile maximum count, rate/burst count, lifetime, speed, size, radius, force and local/world simulation.
- Main visual color now supports Solid, Vertical, Horizontal, Radial Inside-Out and Radial Outside-In gradients with optional reversal.
- Added None, Solid, Soft Glow and Dashed stroke styles with editable color, width and dash frequency.
- Added a dedicated URP transparent Gradient/Stroke Card shader for Static Sprite / Instance Card.
- Added native Trail and Particle gradient application plus quality gates and mobile validation for auxiliary layers.
- Exported pooled prefabs include all configured visual layers; runtime spawning does not rebuild their layer layout.
- Added regression coverage for layer construction, gradient/stroke shader properties and independent layer motion.

## 0.6.0

- Added a draggable splitter between the authoring controls and Live Preview; its width persists across Editor sessions.
- Double-clicking the splitter restores a balanced layout.
- Motion timing is now duration-based and visible for every Motion Stack type.
- Spin uses Angle / Duration (for example 360 degrees in 2 seconds) instead of requiring mental conversion to degrees per second.
- Pulse, Float, Orbit, Flicker, UV Scroll and Shake express cycles or movement units within a configurable duration.
- Expand, Contract, Fade In and Fade Out continue to use their explicit completion duration.
- Added an inline timing summary and Loop control to every motion layer.
- Live Preview automatically expands its timeline to include the longest motion duration and start delay.
- Existing assets retain their previous effective speed when their serialized duration is the default one second.
- Added regression coverage for duration-based Spin/Pulse and splitter constraints.

## 0.5.0

- Static Sprite / Instance Card now uses its own two-sided transparent material, so swords and other flat images remain visible from either camera side.
- Live Preview Edit mode now supports Move, Rotate and Scale tools for each individual item.
- Added compact Position, Rotation and Scale fields for precise per-item editing, including non-uniform scale and full XYZ rotation.
- Added W/E/R shortcuts and direct marker drag gestures for moving, rotating and uniformly resizing the selected item.
- Added regression coverage for two-sided card materials and non-destructive manual transform editing.

## 0.4.0

- Fixed unstable Live Preview playback by giving Editor preview exclusive timeline control and deterministic Particle/VFX seeds.
- Fixed flat visuals intersecting the preview ground by separating environment depth.
- Visual Effect Graph preview now simulates with fixed substeps instead of one large unstable step.
- Added Static Sprite / Instance Card starter preset for stable one-image objects.
- Added in-preview instance markers; convert a procedural layout to Manual and drag items directly on its authoring plane.
- Added per-item activation delay, optional visible duration and reverse order.
- Added Motion Stack scope selection: Whole Formation or Each Instance.
- Per-instance motion, self-spin, particles and VFX use local staggered time in preview and runtime.
- Added timing, conversion and stagger regression coverage.

## 0.3.0

- Added deterministic Instance Layouts so one SubFX source can produce multiple independently placed visuals.
- Added Single, Radial, Arc, Line, Grid, Random Ring and Manual layouts.
- Added instance count, radius, spacing, start angle, facing, rotation/scale steps and deterministic jitter controls.
- Added separate High/Medium/Low instance counts for mobile scalability.
- Added individual self-spin with optional alternating direction; group Motion Stack remains available for whole-formation movement.
- Layouts are baked into exported pooled prefabs and do not instantiate copies during skill playback.
- Added mobile count/renderer validation and layout/runtime regression tests.
- New SubFX, Blend and Skill FX assets now receive collision-free IDs.

## 0.2.0

- Added a composable SubFX Motion Stack with Spin, Pulse, Expand, Contract, Float, Orbit, Flicker, Fade In/Out, UV Scroll and Shake.
- Added editable speed, amount, delay, duration, phase, loop, axis, UV direction and animation curve controls.
- Added Magic Circle, Impact Pop, Energy Flow and Floating Aura quick recipes.
- Motion now runs identically in Live Preview and exported pooled prefabs.
- Existing legacy Spin/Pulse/Expand/Contract metadata remains runtime-compatible.
- Added mobile validation for motion count and invalid motion parameters.
- Added exact-time Spin/Pulse and motion-validator regression tests.

## 0.1.1

- Added an embedded live preview viewport to every authoring step.
- Added isolated preview ground grid, orbit/zoom camera and optional 2m hero scale reference.
- Added play, pause, replay, timeline scrub and High/Medium/Low preview controls.
- Added exact-time Blend Sequence evaluation for editor previews.
- Fixed Unity 6 constructor-side-effect exception in `SpliceFxPropertyDriver`.
- Added preview timing/quality and property-driver regression coverage.

## 0.1.0

- Added preset registry and starter preset installer.
- Added non-destructive alpha processing.
- Added SubFX and Blend Sequence data contracts.
- Added execution-stage skill package binding.
- Added pooled-prefab export metadata and timeline runtime.
- Added mobile quality tiers, budgets and validation.
