# Changelog

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
