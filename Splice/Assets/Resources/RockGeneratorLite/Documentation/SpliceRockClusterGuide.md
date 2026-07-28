# Splice Rock Cluster Extension

This project extends **Tools > Veridian > Rock Generator Lite > 1. Rock Window**
with a deterministic multi-rock workflow. The original single-rock workflow is
unchanged while **Enable Cluster** is off.

## Quick workflow

1. Configure the normal rock shape, material, LOD, and collider settings.
2. Open **Rock Cluster (Multi-Rock Export)** and enable the cluster.
3. Set **Rock Count** and **Cluster Seed**.
4. Choose a distribution shape and tune spread, center bias, position/height
   variance, minimum spacing, scale range, and tilt.
5. Inspect the live preview. Changing the same seed always reconstructs the
   same rock shapes and transforms.
6. Press **Generate Cluster Prefab (Save Exact Preview)**.

The exported root contains normal editable rock children, persistent mesh
assets, LODs/colliders from the rock profile, and one shared material/texture
set. Sharing the material keeps the cluster lighter than exporting an unrelated
material set for every rock.

## Distribution shapes

- **Disk** — filled circular area.
- **Ring** — area between inner and outer radii.
- **Rectangle** — rectangular area.
- **Line** — path-like strip.
- **Mound** — circular pile with a center-height falloff.
- **Sphere Volume** — points inside a 3D volume.
- **Mesh Surface** — triangle-area-weighted sampling over MeshFilters in a
  scene object or prefab.

Mesh Surface does not need a Collider. Its mesh data must be readable by Unity.
Use **Minimum Upward Normal** to exclude undersides or steep walls. Set it to
`-1` to allow every face. Use **Invert Surface Normals** when the source mesh
winding faces inward/downward.

**Show Surface In Preview** is visual-only. **Include Surface In Export**
controls whether the selected surface is also copied into the resulting prefab.

## Placement notes

- **Ground Offset** controls embedding: positive lifts rocks and negative values
  bury them.
- **Minimum Spacing** is best-effort. The generator preserves the requested
  rock count after its rejection-attempt budget is exhausted.
- Preview/export count is capped at 64 to protect Editor memory because every
  rock has unique generated LOD meshes.
- The cluster generator uses its own deterministic random stream and does not
  alter `UnityEngine.Random`, so it cannot change gameplay randomness.

## Output and runtime

The prefab includes a `RockClusterGroup` marker containing seed, count, and
shape metadata. Generated meshes and materials are persistent assets; the
prefab does not depend on the preview window or temporary preview textures.

For very large environments, export a small set of clusters and place those
prefabs with the Rock Placer or Addressables. Avoid one 64-rock cluster when
individual rock culling or destruction is required at runtime.
