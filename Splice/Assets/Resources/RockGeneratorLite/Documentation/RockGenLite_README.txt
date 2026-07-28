ROCK GENERATOR LITE
================================================================================

Rock Generator Lite is the free Lite version of the Veridian Rock Generator
toolset for Unity.

It lets you test the core workflow for creating procedural rock assets inside
the Unity Editor: designing rock profiles, previewing generated meshes, baking
textures, generating clean prefabs, and scattering those prefabs on Unity
Terrain.

Lite is usable on its own, but it is intentionally limited. If the workflow fits
your project and you want to use the generator seriously in production, Rock
Generator Pro is the recommended upgrade. Pro expands the Lite workflow with
batch generation, higher texture resolutions, more generation and texturing
options, a more advanced placer, runtime-oriented tools, and material/texture
atlas combining.

================================================================================
UPGRADE TO ROCK GENERATOR PRO
================================================================================

Rock Generator Pro is the full version of this asset. It is intended for users
who want to move beyond single-rock generation and use the generator as part of
a larger environment art or procedural asset pipeline.

Rock Generator Pro can perform the Lite workflow and adds production-focused
tools that are not included in Lite.

Pro Features Include:
- Batch Rock Generation: Generate organized sets of rock variants instead of
  creating one rock at a time.
- Advanced Rock Placer: A more capable placement workflow than the basic Lite
  placer, including additional placement controls and local brush placement.
- Material and Texture Atlas Combiner: Combine materials and textures into
  atlases for rocks and other assets, helping reduce material fragmentation in
  larger scenes.
- Higher Texture Resolutions: Bake above the Lite 1024x1024 texture limit.
- More Noise Methods: Additional procedural generation options for creating a
  wider range of rock shapes.
- More Texturing Workflows: More ways to texture generated rocks and use
  additional texture inputs.
- Runtime MonoBehaviour Tools: A fuller runtime-oriented setup for projects
  that need procedural rock generation during gameplay.
- Better Large-Scale Workflow: Pro is intended for larger libraries, more
  advanced placement, and more complete production use.

Try Lite first to confirm that the generator workflow and output style fit your
project. If it does, Rock Generator Pro is the recommended version for long-term
use.

Get Rock Generator Pro on the Unity Asset Store:
https://assetstore.unity.com/publishers/120204

================================================================================
GENERATED ASSET OWNERSHIP
================================================================================

The rocks you generate with this tool are your generated output assets.

Generated baked rocks are saved as normal Unity project assets, such as:
- Prefabs
- Meshes
- Materials
- Texture maps
- Optional collider data

For baked-texture output, generated rocks use standard Unity material paths
where available, such as URP Lit, HDRP Lit, or the Built-in Standard shader
fallback. Once generated, the prefab does not need Rock Generator Lite
MonoBehaviours to function as a normal Unity asset.

You may use, modify, sell, and redistribute the generated rock assets in your
own games, demos, scenes, rendered media, asset packs, or commercial projects.

This permission applies to the generated rock output assets. It does not grant
permission to redistribute the Rock Generator Lite or Rock Generator Pro package,
source code, editor tools, compute shaders, documentation, demo scripts, or other
original package files.

Important Note:
If you use Vertex Color output instead of baked textures, the mesh stores color
data in vertex colors. To display those colors correctly, use a material or
shader setup that reads vertex colors. Baked-texture output is the easiest path
when you want clean prefabs with standard material behavior.

================================================================================
GETTING STARTED
================================================================================

Rock Generator Lite is mainly used through Editor windows.

Editor Window Menu Paths:
- Tools > Veridian > Rock Generator Lite > 1. Rock Window
- Tools > Veridian > Rock Generator Lite > 2. Demo Orchestrator
- Tools > Veridian > Rock Generator Lite > 3. Rock Placer

You can also open the Rock Window from:
- GameObject > 3D Object > Veridian Rock Lite

The GameObject menu item opens the generator window. It does not instantly place
a finished rock into the scene. Finished rocks are created when you generate and
save a prefab.

Core Workflow:
1. Open the Rock Window.
2. Choose or create a rock profile.
3. Set an Output Directory and Export Name.
4. Choose a base shape, rock type, preset, or custom settings.
5. Adjust the shape, noise, texture, LOD, and collider options.
6. Preview the rock in the Rock Window viewport.
7. Use "Generate Prefab (Save to Project)" to save the generated rock.
8. Drag the generated prefab into your scene or use the Rock Placer to scatter
   it on a Unity Terrain.

Creating a Saved Profile:
In the Project window, create a new rock profile from:
Create > Procedural Generation > New Rock Settings

A RockSettings asset stores the procedural settings for a rock. You can open a
saved profile in the Rock Window, edit a temporary copy, and then apply the
changes back to the saved profile or save the edited settings as a new profile.

================================================================================
DEMO SCENE AND DEMO WORKFLOW
================================================================================

This package includes demo content to help you test the Lite workflow quickly.

The demo scene is safe to delete after you understand the tool. It is only a
launcher-style scene/readme for opening the Demo Orchestrator and Rock Window.
It is not required by the core generator.

Recommended Demo Flow:
1. Open the included demo scene, if you want a guided starting point.
2. Open the Demo Orchestrator from:
   Tools > Veridian > Rock Generator Lite > 2. Demo Orchestrator
3. Generate a demo terrain canvas.
4. Select a demo preset or assign a custom RockSettings profile.
5. Generate a demo rock.
6. Send the generated rock to the Rock Placer.
7. Populate the demo terrain with the generated rock prefab.

The Demo Orchestrator saves temporary demo output to a demo folder by default:
Assets/VeridianData/RockGenLite/Demo_Assets

Before removing demo content, you can use "Purge All Demo Assets" in the Demo
Orchestrator to delete generated demo terrain data, textures, layers, and demo
rocks. The purge tool includes safety checks and is intended only for generated
demo content.

Safe Deletion:
- The demo scene can be deleted.
- The demo readme/launcher object can be deleted.
- The demo scripts can be deleted if you do not need the demo workflow.
- The core generator does not depend on the demo scene.

If you generated demo assets, purge them first or delete the demo output folder
manually if you are certain it only contains temporary demo content.

================================================================================
EDITOR TOOLS INCLUDED
================================================================================

Rock Window:
The main editor window for designing procedural rocks. It lets you edit
RockSettings profiles, preview generated geometry, inspect LODs, preview baked
textures, and generate saved prefabs.

Demo Orchestrator:
A guided Lite demo workflow. It can create a temporary terrain, generate a demo
rock, and send that rock to the Rock Placer.

Rock Placer:
A basic terrain scattering tool. It places generated rock prefabs on Unity
Terrain using simple rules such as spawn count, scale range, slope limits, height
limits, surface alignment, vertical offset, and clumping.

The Lite Rock Placer is useful for tests and simple scenes. Rock Generator Pro
includes a more advanced and more production-ready placement workflow, including
local brush placement and additional placement controls.

================================================================================
TECHNICAL FOOTPRINT
================================================================================

Rock Generator Lite is separated into four assemblies.

Assembly Architecture:
1. Main Runtime Assembly
   Veridian.RockGenLite.Runtime

2. Main Editor Assembly
   Veridian.RockGenLite.Editor

3. Demo Runtime Assembly
   Veridian.RockGenLite.Demo.Runtime

4. Demo Editor Assembly
   Veridian.RockGenLite.Demo.Editor

The Editor assemblies are stored in Editor folders so Editor-only UI and asset
generation code are excluded from player builds.

Dependency Structure:
- The Main Editor assembly references the Main Runtime assembly.
- The Demo Runtime assembly is isolated from the main generator and is used for
  demo-related runtime components.
- The Demo Editor assembly references the required main and demo assemblies.
- The Main Runtime assembly does not depend on the Editor assemblies.
- The core generator does not depend on the demo scene.

Generated Prefab Independence:
Generated baked rock prefabs are normal Unity assets. They can be used in your
scenes without generator MonoBehaviours attached to the prefab. As long as the
generated prefab, meshes, materials, and textures remain in the project, the rock
can be used like any other static environment prop.

Lite and Pro Together:
Rock Generator Lite and Rock Generator Pro use different namespaces and isolated
Assembly Definitions, so they can safely exist in the same Unity project.

However, if you own Rock Generator Pro, you should strongly consider removing
the Lite version unless you have a specific reason to keep it. Pro covers the
Lite use cases and provides the fuller workflow. Keeping both installed is
supported, but it can add menu clutter and make project organization less clear.

================================================================================
PROJECT FOOTPRINT
================================================================================

Rock Generator Lite is a small package because it does not ship with pre-baked
rock meshes, large texture libraries, or finished rock prefabs. The package is
made primarily from C# scripts, compute shaders, text files, assembly
definitions, and demo helper files.

The package itself is intended to stay under 1 MB before you generate your own
assets.

Generated output will use normal project storage. If you bake many rocks, the
generated meshes, textures, materials, and prefabs will take up disk space like
any other Unity assets.

Use the Demo Orchestrator for temporary tests and use dedicated production
folders for rocks you plan to keep.

================================================================================
EDITOR SCRIPTING NOTES
================================================================================

Rock Generator Lite is primarily an Editor generation tool.

The main profile asset is RockSettings. A RockSettings asset stores the
procedural shape, texture, LOD, collider, and output options for a rock.

The Editor prefab generation pipeline can be used from custom Editor tooling
through the Lite editor code. This is useful for small custom tools that generate
individual rocks from RockSettings profiles.

For large batch generation, higher-resolution baking, runtime-oriented systems,
or more complete automation, use Rock Generator Pro.

================================================================================
LIMITED RUNTIME NOTES
================================================================================

The Lite package includes runtime-side generation infrastructure because the
Editor tools use the same procedural mesh generation pipeline internally.

That said, Lite should be treated mainly as an Editor prefab generator. The
recommended Lite workflow is to generate rocks in the Editor, save them as
prefabs, and use those prefabs in your scenes.

Runtime texture baking is not part of the Lite workflow. If you generate meshes
at runtime with Lite, use an appropriate shared material or vertex-color shader
setup.

Rock Generator Pro is more suitable for runtime rock generation. It includes a
broader runtime-oriented setup, including MonoBehaviour runtime tools and a more
complete workflow for projects that need runtime procedural behavior.

================================================================================
FULL DOCUMENTATION
================================================================================

This README is only a quick orientation file. The full documentation explains
the Rock Window, Demo Orchestrator, shape controls, texture baking, LODs,
colliders, Rock Placer, Editor scripting notes, runtime limitations, generated
asset output, and Pro upgrade path in more detail.

Full documentation:
https://docs.google.com/document/d/1u9eo1GKukanI6D_TkyPhdGOeH5q_eOdomr3Z-sEfgsQ/edit?usp=sharing

================================================================================
SUPPORT AND CONTACT
================================================================================

For setup questions, bug reports, or unexpected issues, you can contact me by
email:

trevor.keiber@gmail.com

Please include:
- Unity version
- Render pipeline
- Target platform
- A short description of the issue
- Steps to reproduce the issue
- Console errors, if any

Support Note:
I review Lite bug reports as time permits and use them to improve future
updates. Priority support, faster responses, and more detailed technical
assistance are reserved for Rock Generator Pro users.

================================================================================
SUPPORT THE TOOL
================================================================================

If Rock Generator Lite helps you test the workflow or generate useful rocks for
your project, consider leaving a rating or review on the Unity Asset Store.

Reviews help free tools remain visible and make continued maintenance easier to
justify.

================================================================================
THE VERIDIAN ECOSYSTEM
================================================================================

You can view my other Unity tools and utilities on my Asset Store publisher
page:

https://assetstore.unity.com/publishers/120204

Recommended Upgrade:
Rock Generator Pro
The full version of this asset. It includes batch generation, higher-resolution
texture baking, more noise and texturing methods, a more advanced rock placer,
local brush placement, runtime MonoBehaviour tools, and a material/texture atlas
combiner for rocks and other assets.

Other Utilities:
If you are building dense environments, also consider tools that support LOD
generation, procedural material creation, mesh optimization, channel packing,
and environment production workflows.