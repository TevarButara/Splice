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
