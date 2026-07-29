# Splice FX Studio

Preset-driven skill VFX authoring for Splice.

Open **Splice > FX Studio > Open Studio**.

Workflow:

1. Install or extend the Preset Library.
2. Create a SubFX asset, choose a preset, import/process a texture, then tune its exposed values.
3. Add SubFX assets to a Blend Sequence and author timing, transform and quality availability.
4. Bind one or more Blend Sequences to skill execution stages.
5. Export. Generated textures, materials and prefabs are written only below the configured `Generated` folder.
6. Validate mobile budgets before building.

The Studio never edits `.vfx` graph YAML. Preset authors create a tested Visual Effect Graph or prefab once and expose a stable property contract; skill authors then work only through Studio data.
