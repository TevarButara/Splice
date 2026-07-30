# Changelog

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
