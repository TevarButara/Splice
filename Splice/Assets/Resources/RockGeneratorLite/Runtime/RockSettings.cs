using System.Collections.Generic;
using UnityEngine;
using Veridian.RockGenLite.Noise;

namespace Veridian.RockGenLite
{
    public enum RockType { Igneous, Metamorphic, Sedimentary, Custom }
    public enum RockColorizationMethod { VertexColors, ProceduralTextureBake, TriplanarInputBake }
    public enum RockColorPattern { SlopeAndCavity, SedimentaryStrata, OrganicPatches }
    public enum RockSlopeMode { None, UpwardOnly, UpwardAndDownward }
    public enum RockBaseShape { Icosphere, CubeSphere }

    // NEW: Replaces RockNormalResolution to cover all maps
    public enum RockMapResolution { Full = 1, Half = 2, Quarter = 4, Eighth = 8 }
    // NEW: Defines how the metallic properties generate
    public enum RockMetallicStyle { None, Veins, CavityDeposits, CrystallineNodules }
    // NEW: Defines how the auxiliary textures are saved
    public enum RockTextureExportMode { PackedMaskMap, IndividualMaps }

    public enum RockColliderType { None, PrimitiveBox, PrimitiveSphere, ConvexMesh, ExactMesh }

    public enum RockPresetType
    {
        None,
        DesertSandstone,
        VolcanicObsidian,
        ColumnarBasaltFragment,
        GoldVeinedQuartz,
        AlienGeode,
        MossyRiverBoulder,
        TexturedGranite,
        FrostRimedAlpineSpire,
        BrittleShaleSlab,
        SunBleachedDesertPillar,
        FoldedMetamorphicSchist,
        DeepSeaHydrothermalVent,
        LichenCrustedLimestone,
        PorousVolcanicPumice,
        BandedIronFormation,
        ScorchedMeteorite,
        GeometricBismuthCluster,

        LayeredCanyonSandstone,
        ScouredGlacialErratic,
        HardpackedMudstone,
        WeatheredChalkCliffBlock,
        RiverWornFlatBoulder,
        OxidizedCopperMalachite,
        CoralLimestoneBlock,
        RawRoseQuartzMass,
        SpeckledWhiteGranite,
        SwirlingMarble,

        GreenSerpentiniteBoulder,
        RedJasperChertNodule,
        TravertineTerraceLimestone,
        PinkRhyoliteTuff,
        PyriteSlateSlab,
        HematiteIronOreNodule,
        LapisPyriteBoulder,
        SulfurStainedFumaroleRock
    }

    [System.Serializable]
    public struct LODLevel
    {
        [Tooltip("The number of subdivisions for the Icosphere base shape. (0-6)")]
        [Range(0, 6)] public int subdivisionLevel;

        [Tooltip("The grid resolution per face for the CubeSphere base shape.")]
        [Range(1, 50)] public int resolution;

        [Tooltip("The screen relative transition height at which this LOD becomes active.")]
        [Range(0.01f, 1.0f)] public float screenRelativeTransitionHeight;
    }

    [CreateAssetMenu(fileName = "NewRockSettings", menuName = "Procedural Generation/New Rock Settings")]
    public class RockSettings : ScriptableObject
    {
        [Header("General & Output")]
        [Tooltip("The folder path where the generated rock prefabs, meshes, and materials will be saved.")]
        public string saveFolderPath = "Assets/VeridianData/RockGenerator/Rocks";

        [Tooltip("The base name for the generated assets.")]
        public string exportName = "Rock";

        [Tooltip("The starting geometric shape for the rock.")]
        public RockBaseShape baseShape = RockBaseShape.CubeSphere;

        [Tooltip("A preset rock type that automatically sets up noise and pattern settings.")]
        public RockType rockType = RockType.Igneous;

        [Tooltip("The random seed used to generate the rock. Change this for a different variation.")]
        public int seed = 42;

        [Header("Core Shape & Sizing")]
        [Tooltip("Approximate generated mesh diameter in meters before Prefab Scale is applied. Lite presets are tuned around 2m. Changing this regenerates the mesh at a different physical size and can change how procedural texture, baked bump, and micro-detail scale feel on the surface.")]
        [Range(0.1f, 50f)] public float targetDiameter = 2.0f;

        [Tooltip("Final uniform scale applied after the rock has been generated. Use this when you want the same generated rock larger or smaller without recalculating procedural noise, baked bump scale limits, or texture relationships.")]
        [Range(0.1f, 10f)] public float prefabScale = 1.0f;

        [Tooltip("If enabled, the proportions of the rock are randomized per seed before procedural deformation is applied.")]
        public bool randomizeProportions = true;

        [Tooltip("Smallest allowed X/Y/Z proportions when proportions are randomized per seed.")]
        public Vector3 minRandomProportions = new Vector3(0.5f, 0.5f, 0.5f);

        [Tooltip("Largest allowed X/Y/Z proportions when proportions are randomized per seed.")]
        public Vector3 maxRandomProportions = new Vector3(1.7f, 1.7f, 1.7f);

        [Tooltip("Fixed X/Y/Z proportions used when Randomize Proportions is disabled. This flattens, stretches, or elongates the base shape before procedural displacement.")]
        public Vector3 baseProportions = Vector3.one;

        [Header("Procedural Texturing")]
        [Tooltip("The method used to apply color and texture. 'Vertex Colors' are highly optimized for Mobile/VR, providing extreme performance by requiring no textures at all.")]
        public RockColorizationMethod colorizationMethod = RockColorizationMethod.ProceduralTextureBake;

        [Tooltip("The procedural pattern style used to color the rock.")]
        public RockColorPattern colorPattern = RockColorPattern.SlopeAndCavity;

        [Tooltip("The resolution of the baked Albedo and primary maps.")]
        public int textureResolution = 1024;

        [Tooltip("The primary base color of the rock.")]
        public Color primaryColor = new Color(0.4f, 0.42f, 0.45f);
        [Tooltip("The secondary color (e.g., used for slopes, primary patches, or mid strata).")]
        public Color secondaryColor = new Color(0.35f, 0.45f, 0.3f);
        [Tooltip("The tertiary color (e.g., used for secondary patches or dark strata).")]
        public Color tertiaryColor = new Color(0.45f, 0.35f, 0.25f);
        [Tooltip("The color applied in the crevices and cavities of the rock.")]
        public Color cavityColor = new Color(0.15f, 0.14f, 0.13f);

        [Tooltip("Determines how the slope affects the placement of the secondary color.")]
        public RockSlopeMode slopeMode = RockSlopeMode.UpwardOnly;

        [Tooltip("The threshold for the slope color. Uses a non-linear curve to give fine control over the top slopes.")]
        [Range(0f, 1f)] public float slopeThreshold = 0.5f;

        [Tooltip("The smoothness of the transition between the slope color and the base color.")]
        [Range(0.01f, 1f)] public float slopeSmoothness = 0.2f;

        [Tooltip("The scale/frequency of the noise used to break up texturing patterns.")]
        [Range(0.1f, 20f)] public float texturingNoiseFrequency = 4.5f;
        [Tooltip("How strongly the texturing noise blends and distorts the pattern edges.")]
        [Range(0f, 1f)] public float texturingNoiseBlend = 0.5f;
        [Tooltip("The intensity of the shadows in the cavities and crevices.")]
        [Range(0f, 1f)] public float cavityStrength = 0.8f;

        [Tooltip("The scale of the domain warping applied to sedimentary strata.")]
        [Range(0.1f, 20f)] public float strataWarpFrequency = 2.0f;
        [Tooltip("The strength of the domain warping applied to sedimentary strata.")]
        [Range(0f, 2f)] public float strataWarpStrength = 0.5f;
        [Tooltip("The scale/frequency of the organic patches pattern.")]
        [Range(0.1f, 20f)] public float patchFrequency = 1.0f;

        [Header("Bump & Micro-Detail")]
        [Tooltip("Bakes procedural surface relief into the normal map. Most baked-texture rocks benefit from at least a small amount of bump; without it, surfaces can look flat.")]
        public bool useNormalPerturbation = true;

        [Tooltip("Physical scale/frequency of the procedural bump pattern. Lower values create finer grain; higher values create broader undulation. Tune this together with Bump Strength.")]
        [Range(0.1f, 20f)] public float normalNoiseFrequency = 8.0f;

        [Tooltip("Strength of the baked procedural bump. Moderate values usually look most natural. Very high values can make the surface look noisy, lumpy, or stylized.")]
        [Range(0f, 1f)] public float normalNoiseStrength = 0.4f;

        [Tooltip("Final multiplier for the baked normal map. 1.0 is the usual default. Increase only when you intentionally want stronger surface lighting.")]
        [Range(0.01f, 5f)] public float normalMapStrength = 1.0f;

        [Header("Micro-Detail (Bake Only)")]
        [Tooltip("Adds a fine grit/pore layer on top of the main baked bump. Use this subtly for rough stone, sand grain, pores, or mineral texture.")]
        public bool useMicroDetail = false;

        [Tooltip("Physical scale/frequency of the micro-detail layer. Lower values create larger fine texture; higher values create denser fine grain.")]
        [Range(10f, 150f)] public float microDetailFrequency = 50.0f;

        [Tooltip("Strength of the fine micro-detail layer. Keep this low unless the rock specifically needs gritty or sharp surface detail.")]
        [Range(0f, 1f)] public float microDetailStrength = 0.4f;

        [Header("Auxiliary Maps & Packing (Bake Only)")]
        [Tooltip("Resolution scale of the baked normal map relative to the Albedo map. Full matches the Albedo resolution; lower settings save memory.")]
        public RockMapResolution normalMapResolution = RockMapResolution.Full;

        [Tooltip("Resolution scale of auxiliary maps such as Metallic, AO, Smoothness, and Height relative to the Albedo map.")]
        public RockMapResolution auxiliaryMapResolution = RockMapResolution.Full;

        [Tooltip("Packed Mask Map stores Metallic in R, Ambient Occlusion in G, and Smoothness in A. This is the safest Unity material workflow, especially for HDRP. Individual Maps exports separate grayscale utility textures for manual/custom materials.")]
        public RockTextureExportMode textureExportMode = RockTextureExportMode.PackedMaskMap;

        [Space(5)]
        [Tooltip("Generate an Ambient Occlusion map based on cavities and crevices.")]
        public bool generateAO = true;

        [Tooltip("Strength/darkness of the generated Ambient Occlusion shadows.")]
        [Range(0f, 2f)] public float aoStrength = .5f;

        [Tooltip("Generate a Height utility map based on the procedural bump pattern. In Lite this is mainly for custom material workflows.")]
        public bool generateHeight = true;

        [Tooltip("Generate smoothness information. In Lite, texture-driven smoothness is mainly intended for metallic/mineral rocks or custom material editing. Ordinary dry stone usually uses Base Smoothness.")]
        public bool generateSmoothness = true;

        [Tooltip("Scalar smoothness used for ordinary non-metal stone and as a fallback when no material smoothness map is active. Low values are usually best for dry rock.")]
        [Range(0f, 1f)] public float baseSmoothness = 0.05f;

        [Header("Metals & Minerals (Bake Only)")]
        [Tooltip("The procedural style of metallic deposits.")]
        public RockMetallicStyle metallicStyle = RockMetallicStyle.None;
        [Tooltip("The color tint of the metallic ore veins/deposits.")]
        public Color oreColor = new Color(0.85f, 0.75f, 0.3f);
        [Tooltip("The scale/frequency of the ore patterns.")]
        [Range(0.1f, 20f)] public float oreFrequency = 3.0f;
        [Tooltip("How much area the ore covers over the rock.")]
        [Range(0f, 1f)] public float oreCoverage = 0.3f;
        [Tooltip("The metallic value of the ore (1 = fully metallic).")]
        [Range(0f, 1f)] public float oreMetallic = 1.0f;
        [Tooltip("The smoothness of the ore (1 = polished).")]
        [Range(0f, 1f)] public float oreSmoothness = 0.6f;

        [Header("Triplanar Output Settings")]
        [Tooltip("The visual scale of the generated textures on the rock. Higher values mean the texture repeats more often, making the details look smaller.")]
        [Range(0.1f, 20f)] public float uvScale = 1.0f;
        [Tooltip("How sharply the textures blend between the Top, Front, and Side projections. Higher values create harder edges between projection angles.")]
        [Range(1.0f, 8.0f)] public float uvBlendSharpness = 4.0f;

        public enum RockFractalMode { Standard, Billow, Ridged, PingPong, SwissErosion }
        public enum RockVoronoiOutputType { F1, F2, F2MinusF1, Edge }

        [Header("LOD Settings")]
        [Tooltip("The different Levels of Detail (LOD) to generate for the rock. The final LOD transition also controls when the rock is culled by the LODGroup.")]
        public List<LODLevel> lodLevels = new List<LODLevel>()
{
    new LODLevel { subdivisionLevel = 4, resolution = 20, screenRelativeTransitionHeight = 0.6f },
    new LODLevel { subdivisionLevel = 3, resolution = 10, screenRelativeTransitionHeight = 0.3f },
    new LODLevel { subdivisionLevel = 2, resolution = 5,  screenRelativeTransitionHeight = 0.02f }
};

        [Header("Physics & Collisions")]
        [Tooltip("The type of physics collider to generate on the root rock object.")]
        public RockColliderType colliderType = RockColliderType.ConvexMesh;

        [Tooltip("The index of the LOD level to use as the Mesh Source if an Exact Mesh Collider is selected.")]
        public int colliderLODIndex = 1;

        [Header("Triplanar Input Textures")]
        [Tooltip("The input Albedo texture for Triplanar Input Bake mode.")]
        public Texture2D inputAlbedo;
        [Tooltip("The input Normal texture for Triplanar Input Bake mode.")]
        public Texture2D inputNormal;
        [Tooltip("How frequently the input textures tile across the rock.")]
        [Range(0.1f, 20f)] public float inputTextureScale = 5.0f;

        [HideInInspector] public bool useMacroNoise = true;
        [HideInInspector] public bool useDomainWarping = false;
        [HideInInspector] public bool useVoronoi = false;
        [HideInInspector] public bool useTerracing = false;

        [Tooltip("The blending calculation used for the massive foundational shapes of the rock. Swiss Erosion creates beautiful sweeping cliffs.")]
        public RockFractalMode macroFractalType = RockFractalMode.Standard;
        [Tooltip("The blending calculation used for the medium-scale surface grit.")]
        public RockFractalMode detailFractalType = RockFractalMode.Standard;
        [Tooltip("Changes the mathematical appearance of the Voronoi crack/crystal patterns.")]
        public RockVoronoiOutputType voronoiOutputType = RockVoronoiOutputType.F1;
        [Tooltip("Changes how distance is calculated in Voronoi, resulting in square, diamond, or organic shapes.")]
        public RockVoronoiMetric voronoiMetric = RockVoronoiMetric.Euclidean;

        [Tooltip("Controls the size of the fundamental core shapes. 1-5 creates sweeping forms. Values past 20 create extremely fragmented chaotic chunks.")]
        [Range(0.01f, 100f)] public float macroNoiseFrequency = 0.4f;
        [Tooltip("How severely the large macro shapes push and deform the base geometry.")]
        [Range(0f, 5f)] public float macroNoiseStrength = 0.5f;
        [Tooltip("The number of noise layers stacked to create the macro shape. More layers = more complex chunk structures.")]
        [Range(1, 5)] public int macroNoiseOctaves = 3;

        [Tooltip("Controls the size of the surface details. 1-3 gives broad bumps, higher values give dense grit.")]
        [Range(0.1f, 20f)] public float noiseFrequency = 1.0f;
        [Tooltip("How strongly the surface detail pushes in and out of the rock.")]
        [Range(0f, 5f)] public float noiseStrength = 0.3f;
        [Tooltip("The number of detail noise layers. More octaves = more geometric detail, but slightly slower generation.")]
        [Range(1, 8)] public int octaves = 5;

        [Tooltip("Controls how quickly details shrink in each noise layer. 2.0 is natural. Values above 3.0 create harsh, static-like grit. Anything under 1.0 breaks the math.")]
        [Range(1f, 5f)] public float lacunarity = 2.0f;
        [Tooltip("Controls the intensity of the smallest details. High values make the rock look much rougher and more chaotic.")]
        [Range(0f, 1f)] public float persistence = 0.5f;

        [Tooltip("The strength of the domain warping effect.")]
        [Range(0f, 5f)] public float warpStrength = 0.5f;
        [Tooltip("The scale of the twisting and folding patterns. Lower values create large sweeps; higher values create tight knots.")]
        [Range(0.1f, 20f)] public float warpFrequency = 1.0f;

        [Tooltip("The physical size of the cracks or crystalline shapes added by Voronoi noise.")]
        [Range(0.1f, 20f)] public float voronoiFrequency = 3.0f;
        [Tooltip("How intensely the cracks/crystals carve into the rock. Mesh inversion is safely clamped.")]
        [Range(0f, 1f)] public float voronoiIntensity = 0.5f;

        [Tooltip("The number of terraces/steps to generate.")]
        [Range(1, 50)] public int terraceCount = 10;
        [Tooltip("How sharp and flat the steps are. 1.0 makes perfect stairs, lower values smooth them out.")]
        [Range(0f, 1f)] public float terraceIntensity = 0.7f;

        public int GetTriangleCountForLOD(int index)
        {
            if (lodLevels == null || index < 0 || index >= lodLevels.Count) return 0;

            LODLevel lod = lodLevels[index];

            if (baseShape == RockBaseShape.CubeSphere)
            {
                int res = Mathf.Max(1, lod.resolution);
                return 12 * res * res;
            }
            else
            {
                int subDiv = Mathf.Clamp(lod.subdivisionLevel, 0, 6);
                int segments = (int)Mathf.Pow(2, subDiv);
                return 20 * segments * segments;
            }
        }
    }
}