# Splice FX Studio

Preset-driven skill VFX authoring for Splice.

Open **Splice > FX Studio > Open Studio**.

Workflow:

1. Install or extend the Preset Library.
2. Create a SubFX asset, choose a preset, import/process a texture, tune its exposed values, then add an Instance Layout, Motion Stack layers or a Quick FX recipe.
3. Add SubFX assets to a Blend Sequence and author timing, transform and quality availability.
4. Bind one or more Blend Sequences to skill execution stages.
5. Export. Generated textures, materials and prefabs are written only below the configured `Generated` folder.
6. Validate mobile budgets before building.

The Studio never edits `.vfx` graph YAML. Preset authors create a tested Visual Effect Graph or prefab once and expose a stable property contract; skill authors then work only through Studio data.

Instance Layout turns one source visual into a formation. Use **5 Around**
for a five-sword circle, then tune radius, start angle, facing and per-item
rotation. Available layouts are Single, Radial, Arc, Line, Grid,
deterministic Random Ring and fully Manual positioning. The exported pooled
prefab contains the final copies; playback does not instantiate them.

For a single stable image such as a sword, use the **Static Sprite / Instance
Card** preset. It uses a dedicated two-sided material, so the card remains
visible from either camera side. Particle presets intentionally emit
short-lived copies and are not a replacement for an instance card. In Live
Preview, press **Edit** to convert the current formation to Manual, then select
each item and use Move/Rotate/Scale (W/E/R). Drag its marker for fast editing,
or enter exact XYZ Position/Rotation/Scale values for full non-uniform control.
`Delay Per Item`, `Visible Duration`, `Reverse Order`, and `Motion Stack
Applies To` create sequential reveal or per-item motion without custom code.

The authoring and Live Preview panes are separated by a draggable splitter.
Drag it to allocate space and double-click it to restore the balanced width;
the selected width is saved across Unity sessions.

Every Motion Stack layer uses an explicit duration. Spin is authored as
**Angle / Complete Angle In**, so `360` degrees over `2` seconds produces
`180°/s`. Pulse, Float, Orbit, Flicker, UV Scroll and Shake specify how many
cycles or movement units happen inside their duration. Each layer displays a
plain-language timing summary and can either loop or hold its final state.

SubFX Lab also contains **Additional Visual Layers**. Add as many Trail or
Particle layers as the mobile budget allows. Each layer owns a texture,
gradient, emission, transform, quality mask, Instance Layout and an independent
Layer Motion Stack. Trail layers configure width, lifetime, tiling and
alignment. Particle layers configure continuous/burst emission, shape, count,
rate, lifetime, speed, size, force and simulation space. These layers are
built into the exported pooled prefab.

The main visual supports Solid, Vertical, Horizontal, Radial Inside-Out and
Radial Outside-In gradient modes. Static Sprite / Instance Card uses the
FX Studio Gradient Stroke Card shader and supports Solid, Soft Glow and Dashed
outlines. Other custom templates must expose the same shader properties or the
validator reports a compatibility warning.

Color authoring is exclusive: `Solid` uses Main Color, while Vertical,
Horizontal and Radial modes use Main Gradient and ignore Main Color. Stroke is
derived from the source image alpha; transparent padding around the image gives
the outer outline room to render clearly.
