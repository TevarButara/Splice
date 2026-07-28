#if UNITY_EDITOR
using UnityEngine;
using Veridian.RockGenLite.Noise;

namespace Veridian.RockGenLite.Editor
{
    public static class RockPresetUtility
    {
        private const float PresetDiameterMeters = 2.0f;

        private static void SetBumpNormalized(RockSettings p, float scale01, float strength, float normalStrength = 1.0f)
        {
            float minScaleMeters = Mathf.Max(0.005f, PresetDiameterMeters * 0.005f);
            float maxScaleMeters = Mathf.Max(minScaleMeters + 0.0001f, PresetDiameterMeters * 0.15f);
            float scaleMeters = Mathf.Lerp(minScaleMeters, maxScaleMeters, Mathf.Clamp01(scale01));

            p.useNormalPerturbation = true;
            p.normalNoiseFrequency = 1.0f / Mathf.Max(0.00001f, scaleMeters);
            p.normalNoiseStrength = strength;
            p.normalMapStrength = normalStrength;
        }
        private static void ApplyDefaultLODTransitions(RockSettings p)
        {
            if (p == null) return;

            if (p.lodLevels == null)
            {
                p.lodLevels = new System.Collections.Generic.List<LODLevel>();
            }

            if (p.lodLevels.Count == 0)
            {
                p.lodLevels.Add(new LODLevel { subdivisionLevel = 4, resolution = 20, screenRelativeTransitionHeight = 0.6f });
                p.lodLevels.Add(new LODLevel { subdivisionLevel = 3, resolution = 10, screenRelativeTransitionHeight = 0.3f });
                p.lodLevels.Add(new LODLevel { subdivisionLevel = 2, resolution = 5, screenRelativeTransitionHeight = 0.02f });
                return;
            }

            if (p.lodLevels.Count >= 1)
            {
                LODLevel lod0 = p.lodLevels[0];
                lod0.screenRelativeTransitionHeight = 0.6f;
                p.lodLevels[0] = lod0;
            }

            if (p.lodLevels.Count >= 2)
            {
                LODLevel lod1 = p.lodLevels[1];
                lod1.screenRelativeTransitionHeight = 0.3f;
                p.lodLevels[1] = lod1;
            }

            int lastIndex = p.lodLevels.Count - 1;
            LODLevel finalLOD = p.lodLevels[lastIndex];
            finalLOD.screenRelativeTransitionHeight = 0.02f;
            p.lodLevels[lastIndex] = finalLOD;
        }
        public static void ApplyPreset(RockSettings p, RockPresetType preset)
        {
            if (p == null || preset == RockPresetType.None) return;

            ResetToDefaults(p);
            p.exportName = preset.ToString();

            switch (preset)
            {
                case RockPresetType.DesertSandstone:
                    p.exportName = "DesertSandstone";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.6f, 0.6f, 1.4f);
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.75f, 0.45f, 0.25f);
                    p.secondaryColor = new Color(0.65f, 0.35f, 0.15f);
                    p.tertiaryColor = new Color(0.85f, 0.55f, 0.35f);
                    p.cavityColor = new Color(0.3f, 0.15f, 0.05f);
                    p.cavityStrength = 0.8f;
                    p.texturingNoiseFrequency = 20.0f;
                    p.texturingNoiseBlend = 0.02f;
                    p.strataWarpFrequency = 1.0f;
                    p.strataWarpStrength = 0.15f;
                    p.useTerracing = true;
                    p.terraceCount = 14;
                    p.terraceIntensity = 0.8f;
                    p.useMacroNoise = true;
                    p.macroNoiseStrength = 0.3f;
                    break;

                case RockPresetType.VolcanicObsidian:
                    p.exportName = "VolcanicObsidian";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.15f, 0.82f, 1.35f);
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.slopeMode = RockSlopeMode.UpwardAndDownward;
                    p.slopeThreshold = 0.25f;
                    p.slopeSmoothness = 0.65f;
                    p.primaryColor = new Color(0.012f, 0.012f, 0.014f);
                    p.secondaryColor = new Color(0.045f, 0.042f, 0.048f);
                    p.cavityColor = new Color(0.18f, 0.045f, 0.02f);
                    p.cavityStrength = 0.22f;
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.55f;
                    p.macroNoiseStrength = 0.24f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.85f;
                    p.noiseStrength = 0.04f;
                    p.octaves = 2;
                    p.lacunarity = 1.65f;
                    p.persistence = 0.32f;
                    p.useVoronoi = true;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F2MinusF1;
                    p.voronoiFrequency = 1.1f;
                    p.voronoiIntensity = 0.32f;
                    p.baseSmoothness = 0.78f;
                    SetBumpNormalized(p, 0.65f, 0.55f, 1.0f);
                    break;

                case RockPresetType.ColumnarBasaltFragment:
                    p.exportName = "ColumnarBasaltFragment";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(0.82f, 1.9f, 0.78f);
                    p.useMacroNoise = true;
                    p.macroFractalType = RockSettings.RockFractalMode.Ridged;
                    p.macroNoiseFrequency = 0.42f;
                    p.macroNoiseStrength = 0.28f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.85f;
                    p.noiseStrength = 0.18f;
                    p.octaves = 3;
                    p.lacunarity = 1.75f;
                    p.persistence = 0.38f;
                    p.useVoronoi = true;
                    p.voronoiMetric = RockVoronoiMetric.Chebyshev;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F1;
                    p.voronoiFrequency = 1.0f;
                    p.voronoiIntensity = 0.42f;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.slopeMode = RockSlopeMode.UpwardAndDownward;
                    p.slopeThreshold = 0.35f;
                    p.slopeSmoothness = 0.4f;
                    p.primaryColor = new Color(0.105f, 0.11f, 0.12f);
                    p.secondaryColor = new Color(0.19f, 0.19f, 0.205f);
                    p.cavityColor = new Color(0.025f, 0.025f, 0.03f);
                    p.cavityStrength = 0.9f;
                    p.baseSmoothness = 0.06f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 7.0f;
                    p.normalNoiseStrength = 0.36f;
                    break;

                case RockPresetType.GoldVeinedQuartz:
                    p.exportName = "GoldVeinedQuartz";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.useMacroNoise = true;
                    p.macroNoiseStrength = 0.4f;
                    p.useDomainWarping = true;
                    p.warpStrength = 1.5f;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.primaryColor = new Color(0.85f, 0.85f, 0.85f);
                    p.secondaryColor = new Color(0.75f, 0.75f, 0.75f);
                    p.cavityColor = new Color(0.3f, 0.3f, 0.3f);
                    p.metallicStyle = RockMetallicStyle.Veins;
                    p.oreColor = new Color(1.0f, 0.8f, 0.2f);
                    p.oreCoverage = 0.35f;
                    p.oreFrequency = 2.0f;
                    p.oreMetallic = 1.0f;
                    p.oreSmoothness = 0.8f;
                    p.baseSmoothness = 0.3f;
                    break;

                case RockPresetType.AlienGeode:
                    p.exportName = "AlienGeode";
                    p.useMacroNoise = true;
                    p.macroNoiseStrength = 0.8f;
                    p.useDomainWarping = true;
                    p.warpStrength = 2.5f;
                    p.warpFrequency = 1.5f;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.15f, 0.18f, 0.2f);
                    p.secondaryColor = new Color(0.1f, 0.12f, 0.15f);
                    p.tertiaryColor = new Color(0.2f, 0.22f, 0.25f);
                    p.cavityColor = new Color(0.05f, 0.05f, 0.05f);
                    p.metallicStyle = RockMetallicStyle.CrystallineNodules;
                    p.oreColor = new Color(0.6f, 0.1f, 0.9f);
                    p.oreCoverage = 0.45f;
                    p.oreFrequency = 4.0f;
                    p.oreMetallic = 1.0f;
                    p.oreSmoothness = 0.9f;
                    break;

                case RockPresetType.MossyRiverBoulder:
                    p.exportName = "MossyRiverBoulder";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.65f, 0.55f, 1.35f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.35f;
                    p.macroNoiseStrength = 0.17f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 1.0f;
                    p.noiseStrength = 0.06f;
                    p.octaves = 3;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.slopeMode = RockSlopeMode.UpwardOnly;
                    p.slopeThreshold = 0.5f;
                    p.slopeSmoothness = 0.72f;
                    p.primaryColor = new Color(0.34f, 0.36f, 0.38f);
                    p.secondaryColor = new Color(0.18f, 0.32f, 0.13f);
                    p.cavityColor = new Color(0.11f, 0.12f, 0.11f);
                    p.cavityStrength = 0.45f;
                    p.texturingNoiseBlend = 0.18f;
                    p.baseSmoothness = 0.12f;
                    SetBumpNormalized(p, 0.5f, 0.25f, 1.0f);
                    break;

                case RockPresetType.TexturedGranite:
                    p.exportName = "TexturedGranite";
                    p.useMacroNoise = true;
                    p.macroNoiseStrength = 0.6f;
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.7f, 0.7f, 0.7f);
                    p.secondaryColor = new Color(0.3f, 0.3f, 0.3f);
                    p.tertiaryColor = new Color(0.85f, 0.85f, 0.85f);
                    p.cavityColor = new Color(0.1f, 0.1f, 0.1f);
                    p.patchFrequency = 18.0f;
                    p.texturingNoiseBlend = 0.7f;
                    p.texturingNoiseFrequency = 25.0f;
                    p.baseSmoothness = 0.08f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 12.0f;
                    p.normalNoiseStrength = 0.5f;
                    break;

                case RockPresetType.FrostRimedAlpineSpire:
                    p.exportName = "AlpineSpire";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(0.6f, 2.5f, 0.6f);
                    p.useMacroNoise = true;
                    p.macroFractalType = RockSettings.RockFractalMode.SwissErosion;
                    p.macroNoiseStrength = 0.8f;
                    p.macroNoiseFrequency = 0.5f;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.slopeMode = RockSlopeMode.UpwardOnly;
                    p.slopeThreshold = 0.65f;
                    p.slopeSmoothness = 0.05f;
                    p.primaryColor = new Color(0.15f, 0.16f, 0.18f);
                    p.secondaryColor = new Color(0.9f, 0.95f, 1.0f);
                    p.cavityColor = new Color(0.05f, 0.05f, 0.06f);
                    p.baseSmoothness = 0.1f;
                    break;

                case RockPresetType.BrittleShaleSlab:
                    p.exportName = "BrittleShale";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(2.5f, 0.3f, 2.5f);
                    p.useMacroNoise = true;
                    p.detailFractalType = RockSettings.RockFractalMode.Ridged;
                    p.macroNoiseStrength = 0.2f;
                    p.useTerracing = true;
                    p.terraceCount = 35;
                    p.terraceIntensity = 0.95f;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.2f, 0.22f, 0.25f);
                    p.secondaryColor = new Color(0.15f, 0.17f, 0.2f);
                    p.tertiaryColor = new Color(0.25f, 0.28f, 0.3f);
                    p.cavityColor = new Color(0.05f, 0.05f, 0.05f);
                    p.texturingNoiseFrequency = 25.0f;
                    p.texturingNoiseBlend = 0.1f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseStrength = 0.6f;
                    break;

                case RockPresetType.SunBleachedDesertPillar:
                    p.exportName = "SunBleachedDesertPillar";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(0.72f, 2.75f, 0.76f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.38f;
                    p.macroNoiseStrength = 0.34f;
                    p.macroNoiseOctaves = 3;
                    p.noiseFrequency = 1.0f;
                    p.noiseStrength = 0.18f;
                    p.octaves = 3;
                    p.useTerracing = true;
                    p.terraceCount = 16;
                    p.terraceIntensity = 0.45f;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.73f, 0.57f, 0.38f);
                    p.secondaryColor = new Color(0.86f, 0.75f, 0.58f);
                    p.tertiaryColor = new Color(0.55f, 0.28f, 0.17f);
                    p.cavityColor = new Color(0.28f, 0.13f, 0.08f);
                    p.cavityStrength = 0.55f;
                    p.texturingNoiseFrequency = 8.0f;
                    p.texturingNoiseBlend = 0.18f;
                    p.strataWarpFrequency = 0.8f;
                    p.strataWarpStrength = 0.26f;
                    p.baseSmoothness = 0.025f;
                    break;

                case RockPresetType.FoldedMetamorphicSchist:
                    p.exportName = "FoldedMetamorphicSchist";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.75f, 0.55f, 1.05f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.28f;
                    p.macroNoiseStrength = 0.32f;
                    p.macroNoiseOctaves = 2;
                    p.detailFractalType = RockSettings.RockFractalMode.Ridged;
                    p.noiseFrequency = 0.9f;
                    p.noiseStrength = 0.08f;
                    p.octaves = 3;
                    p.lacunarity = 1.65f;
                    p.persistence = 0.35f;
                    p.useDomainWarping = true;
                    p.warpStrength = 1.25f;
                    p.warpFrequency = 0.45f;
                    p.useVoronoi = false;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.22f, 0.22f, 0.26f);
                    p.secondaryColor = new Color(0.43f, 0.40f, 0.46f);
                    p.tertiaryColor = new Color(0.14f, 0.13f, 0.16f);
                    p.cavityColor = new Color(0.045f, 0.04f, 0.05f);
                    p.cavityStrength = 0.65f;
                    p.strataWarpFrequency = 0.7f;
                    p.strataWarpStrength = 1.45f;
                    p.texturingNoiseFrequency = 4.0f;
                    p.texturingNoiseBlend = 0.2f;
                    p.baseSmoothness = 0.04f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 5.5f;
                    p.normalNoiseStrength = 0.22f;
                    break;

                case RockPresetType.DeepSeaHydrothermalVent:
                    p.exportName = "DeepSeaHydrothermalVent";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(0.8f, 2.25f, 0.82f);
                    p.useMacroNoise = true;
                    p.macroFractalType = RockSettings.RockFractalMode.Ridged;
                    p.macroNoiseFrequency = 0.72f;
                    p.macroNoiseStrength = 0.95f;
                    p.macroNoiseOctaves = 2;
                    p.detailFractalType = RockSettings.RockFractalMode.Ridged;
                    p.noiseFrequency = 0.95f;
                    p.noiseStrength = 0.22f;
                    p.octaves = 3;
                    p.lacunarity = 1.7f;
                    p.persistence = 0.42f;
                    p.useVoronoi = true;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F2MinusF1;
                    p.voronoiFrequency = 1.25f;
                    p.voronoiIntensity = 0.36f;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.slopeMode = RockSlopeMode.UpwardAndDownward;
                    p.slopeThreshold = 0.68f;
                    p.slopeSmoothness = 0.65f;
                    p.primaryColor = new Color(0.026f, 0.036f, 0.048f);
                    p.secondaryColor = new Color(0.30f, 0.36f, 0.38f);
                    p.cavityColor = new Color(0.003f, 0.005f, 0.008f);
                    p.cavityStrength = 0.9f;
                    p.texturingNoiseFrequency = 3.2f;
                    p.texturingNoiseBlend = 0.14f;
                    p.metallicStyle = RockMetallicStyle.CavityDeposits;
                    p.oreColor = new Color(0.42f, 0.44f, 0.40f);
                    p.oreFrequency = 3.5f;
                    p.oreCoverage = 0.28f;
                    p.oreMetallic = 0.75f;
                    p.oreSmoothness = 0.34f;
                    p.baseSmoothness = 0.08f;
                    SetBumpNormalized(p, 0.5f, 0.34f, 1.0f);
                    break;

                case RockPresetType.LichenCrustedLimestone:
                    p.exportName = "LichenLimestone";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.6f, 0.62f, 0.65f);
                    p.secondaryColor = new Color(0.65f, 0.65f, 0.2f);
                    p.tertiaryColor = new Color(0.45f, 0.55f, 0.35f);
                    p.cavityColor = new Color(0.2f, 0.2f, 0.2f);
                    p.patchFrequency = 1.5f;
                    p.texturingNoiseBlend = 0.85f;
                    p.texturingNoiseFrequency = 10.0f;
                    break;

                case RockPresetType.PorousVolcanicPumice:
                    p.exportName = "PorousVolcanicPumice";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.25f, 0.78f, 1.05f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.45f;
                    p.macroNoiseStrength = 0.32f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 1.2f;
                    p.noiseStrength = 0.08f;
                    p.octaves = 3;
                    p.lacunarity = 1.7f;
                    p.persistence = 0.35f;
                    p.useVoronoi = true;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F1;
                    p.voronoiFrequency = 4.5f;
                    p.voronoiIntensity = 0.38f;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.slopeMode = RockSlopeMode.UpwardAndDownward;
                    p.slopeThreshold = 0.25f;
                    p.slopeSmoothness = 0.7f;
                    p.primaryColor = new Color(0.66f, 0.64f, 0.57f);
                    p.secondaryColor = new Color(0.76f, 0.73f, 0.64f);
                    p.cavityColor = new Color(0.24f, 0.22f, 0.18f);
                    p.cavityStrength = 0.95f;
                    p.baseSmoothness = 0.015f;
                    SetBumpNormalized(p, 0.57f, 0.71f, 1.0f);
                    break;

                case RockPresetType.BandedIronFormation:
                    p.exportName = "BandedIron";
                    p.useMacroNoise = true;
                    p.macroNoiseStrength = 0.3f;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.45f, 0.15f, 0.1f);
                    p.secondaryColor = new Color(0.35f, 0.1f, 0.05f);
                    p.tertiaryColor = new Color(0.25f, 0.05f, 0.05f);
                    p.texturingNoiseFrequency = 6.0f;
                    p.texturingNoiseBlend = 0.05f;
                    p.metallicStyle = RockMetallicStyle.Veins;
                    p.oreFrequency = 6.0f;
                    p.oreCoverage = 0.45f;
                    p.oreColor = new Color(0.7f, 0.7f, 0.75f);
                    p.oreMetallic = 1.0f;
                    p.oreSmoothness = 0.65f;
                    break;

                case RockPresetType.ScorchedMeteorite:
                    p.exportName = "ScorchedMeteorite";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.08f, 0.9f, 0.98f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.34f;
                    p.macroNoiseStrength = 0.22f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.85f;
                    p.noiseStrength = 0.08f;
                    p.octaves = 2;
                    p.useVoronoi = true;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F2MinusF1;
                    p.voronoiFrequency = 1.45f;
                    p.voronoiIntensity = 0.52f;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.04f, 0.038f, 0.034f);
                    p.secondaryColor = new Color(0.28f, 0.12f, 0.055f);
                    p.tertiaryColor = new Color(0.52f, 0.19f, 0.08f);
                    p.cavityColor = new Color(0.62f, 0.18f, 0.055f);
                    p.cavityStrength = 1.0f;
                    p.patchFrequency = 1.15f;
                    p.texturingNoiseFrequency = 4.5f;
                    p.texturingNoiseBlend = 0.68f;
                    p.metallicStyle = RockMetallicStyle.CavityDeposits;
                    p.oreColor = new Color(0.33f, 0.30f, 0.26f);
                    p.oreFrequency = 2.6f;
                    p.oreCoverage = 0.32f;
                    p.oreMetallic = 0.62f;
                    p.oreSmoothness = 0.34f;
                    p.baseSmoothness = 0.075f;
                    SetBumpNormalized(p, 0.6f, 0.6f, 1.0f);
                    break;

                case RockPresetType.GeometricBismuthCluster:
                    p.exportName = "GeometricBismuth";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.useMacroNoise = false;
                    p.useVoronoi = true;
                    p.voronoiMetric = RockVoronoiMetric.Chebyshev;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F1;
                    p.voronoiFrequency = 4.0f;
                    p.voronoiIntensity = 1.0f;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.9f, 0.2f, 0.6f);
                    p.secondaryColor = new Color(0.1f, 0.8f, 0.8f);
                    p.tertiaryColor = new Color(0.9f, 0.7f, 0.1f);
                    p.cavityColor = new Color(0.1f, 0.05f, 0.1f);
                    p.patchFrequency = 1.5f;
                    p.texturingNoiseBlend = 0.1f;
                    p.baseSmoothness = 0.8f;
                    p.metallicStyle = RockMetallicStyle.CavityDeposits;
                    p.oreColor = Color.white;
                    p.oreCoverage = 0.8f;
                    p.oreMetallic = 1.0f;
                    break;

                case RockPresetType.LayeredCanyonSandstone:
                    p.exportName = "CanyonSandstone";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.9f, 0.85f, 0.75f);
                    p.secondaryColor = new Color(0.85f, 0.65f, 0.55f);
                    p.tertiaryColor = new Color(0.75f, 0.45f, 0.35f);
                    p.cavityColor = new Color(0.45f, 0.35f, 0.25f);
                    p.cavityStrength = 0.5f;
                    p.texturingNoiseFrequency = 20.0f;
                    p.strataWarpStrength = 0.1f;
                    p.useTerracing = true;
                    p.terraceCount = 20;
                    p.terraceIntensity = 0.8f;
                    p.baseSmoothness = 0.02f;
                    break;

                case RockPresetType.ScouredGlacialErratic:
                    p.exportName = "GlacialErratic";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.macroFractalType = RockSettings.RockFractalMode.SwissErosion;
                    p.useDomainWarping = true;
                    p.warpFrequency = 0.5f;
                    p.warpStrength = 1.5f;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.primaryColor = new Color(0.85f, 0.88f, 0.9f);
                    p.secondaryColor = new Color(0.75f, 0.8f, 0.85f);
                    p.cavityColor = new Color(0.55f, 0.6f, 0.65f);
                    p.cavityStrength = 0.4f;
                    p.baseSmoothness = 0.1f;
                    break;

                case RockPresetType.HardpackedMudstone:
                    p.exportName = "HardpackedMudstone";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.macroFractalType = RockSettings.RockFractalMode.Standard;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.75f, 0.65f, 0.55f);
                    p.secondaryColor = new Color(0.65f, 0.55f, 0.45f);
                    p.tertiaryColor = new Color(0.55f, 0.45f, 0.35f);
                    p.cavityColor = new Color(0.35f, 0.28f, 0.2f);
                    p.cavityStrength = 0.5f;
                    p.useMicroDetail = false;
                    p.microDetailFrequency = 120.0f;
                    p.microDetailStrength = 0.0f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 18.0f;
                    p.normalNoiseStrength = 0.36f;
                    p.baseSmoothness = 0.0f;
                    break;

                case RockPresetType.WeatheredChalkCliffBlock:
                    p.exportName = "WeatheredChalkCliffBlock";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.45f, 1.75f, 0.72f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.32f;
                    p.macroNoiseStrength = 0.26f;
                    p.macroNoiseOctaves = 2;
                    p.detailFractalType = RockSettings.RockFractalMode.Ridged;
                    p.noiseFrequency = 1.1f;
                    p.noiseStrength = 0.09f;
                    p.octaves = 3;
                    p.lacunarity = 1.7f;
                    p.persistence = 0.35f;
                    p.useTerracing = true;
                    p.terraceCount = 18;
                    p.terraceIntensity = 0.42f;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.90f, 0.88f, 0.82f);
                    p.secondaryColor = new Color(0.78f, 0.75f, 0.66f);
                    p.tertiaryColor = new Color(0.96f, 0.94f, 0.87f);
                    p.cavityColor = new Color(0.48f, 0.47f, 0.42f);
                    p.cavityStrength = 0.55f;
                    p.texturingNoiseFrequency = 9.0f;
                    p.texturingNoiseBlend = 0.16f;
                    p.strataWarpFrequency = 0.65f;
                    p.strataWarpStrength = 0.12f;
                    p.baseSmoothness = 0.0f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 7.0f;
                    p.normalNoiseStrength = 0.28f;
                    p.normalMapStrength = 1.0f;
                    break;

                case RockPresetType.RiverWornFlatBoulder:
                    p.exportName = "RiverWornFlatBoulder";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.65f, 0.42f, 1.28f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.22f;
                    p.macroNoiseStrength = 0.09f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.8f;
                    p.noiseStrength = 0.025f;
                    p.octaves = 2;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.patchFrequency = 0.75f;
                    p.texturingNoiseFrequency = 4.0f;
                    p.texturingNoiseBlend = 0.45f;
                    p.primaryColor = new Color(0.46f, 0.50f, 0.52f);
                    p.secondaryColor = new Color(0.34f, 0.38f, 0.40f);
                    p.tertiaryColor = new Color(0.57f, 0.54f, 0.47f);
                    p.cavityColor = new Color(0.22f, 0.24f, 0.25f);
                    p.cavityStrength = 0.18f;
                    p.baseSmoothness = 0.045f;
                    SetBumpNormalized(p, 0.54f, 0.5f, 1.0f);
                    break;

                case RockPresetType.OxidizedCopperMalachite:
                    p.exportName = "OxidizedCopper";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.primaryColor = new Color(0.55f, 0.75f, 0.65f);
                    p.secondaryColor = new Color(0.4f, 0.65f, 0.6f);
                    p.cavityColor = new Color(0.2f, 0.4f, 0.35f);
                    p.cavityStrength = 0.5f;
                    p.metallicStyle = RockMetallicStyle.Veins;
                    p.oreColor = new Color(0.85f, 0.5f, 0.25f);
                    p.oreCoverage = 0.35f;
                    p.oreFrequency = 4.0f;
                    p.oreMetallic = 1.0f;
                    p.oreSmoothness = 0.75f;
                    p.baseSmoothness = 0.04f;
                    break;

                case RockPresetType.CoralLimestoneBlock:
                    p.exportName = "CoralLimestoneBlock";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.25f, 0.82f, 1.1f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.38f;
                    p.macroNoiseStrength = 0.2f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 1.15f;
                    p.noiseStrength = 0.07f;
                    p.octaves = 3;
                    p.lacunarity = 1.75f;
                    p.persistence = 0.36f;
                    p.useVoronoi = true;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F1;
                    p.voronoiFrequency = 5.5f;
                    p.voronoiIntensity = 0.34f;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.82f, 0.76f, 0.62f);
                    p.secondaryColor = new Color(0.68f, 0.58f, 0.42f);
                    p.tertiaryColor = new Color(0.9f, 0.84f, 0.7f);
                    p.cavityColor = new Color(0.34f, 0.27f, 0.18f);
                    p.cavityStrength = 0.72f;
                    p.patchFrequency = 1.4f;
                    p.texturingNoiseFrequency = 8.0f;
                    p.texturingNoiseBlend = 0.55f;
                    p.baseSmoothness = 0.012f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 8.0f;
                    p.normalNoiseStrength = 0.38f;
                    break;

                case RockPresetType.RawRoseQuartzMass:
                    p.exportName = "RawRoseQuartzMass";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.18f, 0.92f, 1.05f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.3f;
                    p.macroNoiseStrength = 0.18f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.9f;
                    p.noiseStrength = 0.035f;
                    p.octaves = 2;
                    p.useVoronoi = true;
                    p.voronoiMetric = RockVoronoiMetric.Euclidean;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F2MinusF1;
                    p.voronoiFrequency = 1.35f;
                    p.voronoiIntensity = 0.22f;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.94f, 0.76f, 0.82f);
                    p.secondaryColor = new Color(0.86f, 0.62f, 0.70f);
                    p.tertiaryColor = new Color(0.98f, 0.9f, 0.92f);
                    p.cavityColor = new Color(0.62f, 0.38f, 0.46f);
                    p.cavityStrength = 0.32f;
                    p.patchFrequency = 0.85f;
                    p.texturingNoiseFrequency = 3.5f;
                    p.texturingNoiseBlend = 0.35f;
                    p.baseSmoothness = 0.22f;
                    SetBumpNormalized(p, 0.3f, 0.63f, 1.0f);
                    break;

                case RockPresetType.SpeckledWhiteGranite:
                    p.exportName = "SpeckledGranite";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.macroNoiseFrequency = 0.2f;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.patchFrequency = 25.0f;
                    p.texturingNoiseBlend = 0.9f;
                    p.primaryColor = new Color(0.85f, 0.85f, 0.85f);
                    p.secondaryColor = new Color(0.3f, 0.3f, 0.3f);
                    p.tertiaryColor = new Color(0.9f, 0.88f, 0.88f);
                    p.cavityColor = new Color(0.5f, 0.5f, 0.5f);
                    p.cavityStrength = 0.4f;
                    p.baseSmoothness = 0.05f;
                    break;

                case RockPresetType.SwirlingMarble:
                    p.exportName = "SwirlingMarble";
                    p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.15f, 0.9f, 1.0f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.2f;
                    p.macroNoiseStrength = 0.08f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.65f;
                    p.noiseStrength = 0.025f;
                    p.octaves = 2;
                    p.useVoronoi = false;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.useDomainWarping = true;
                    p.warpFrequency = 0.45f;
                    p.warpStrength = 1.65f;
                    p.strataWarpFrequency = 0.65f;
                    p.strataWarpStrength = 1.85f;
                    p.texturingNoiseFrequency = 1.85f;
                    p.texturingNoiseBlend = 0.22f;
                    p.primaryColor = new Color(0.92f, 0.90f, 0.86f);
                    p.secondaryColor = new Color(0.72f, 0.70f, 0.68f);
                    p.tertiaryColor = new Color(0.42f, 0.45f, 0.48f);
                    p.cavityColor = new Color(0.36f, 0.34f, 0.32f);
                    p.cavityStrength = 0.24f;
                    p.baseSmoothness = 0.075f;
                    SetBumpNormalized(p, 0.42f, 0.81f, 1.0f);
                    break;

                case RockPresetType.GreenSerpentiniteBoulder:
                    p.exportName = "GreenSerpentiniteBoulder";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.22f, 0.88f, 1.08f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.34f;
                    p.macroNoiseStrength = 0.28f;
                    p.macroNoiseOctaves = 2;
                    p.useDomainWarping = true;
                    p.warpFrequency = 0.5f;
                    p.warpStrength = 0.8f;
                    p.noiseFrequency = 0.9f;
                    p.noiseStrength = 0.08f;
                    p.octaves = 3;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.18f, 0.30f, 0.20f);
                    p.secondaryColor = new Color(0.34f, 0.48f, 0.30f);
                    p.tertiaryColor = new Color(0.08f, 0.13f, 0.10f);
                    p.cavityColor = new Color(0.035f, 0.055f, 0.04f);
                    p.cavityStrength = 0.62f;
                    p.patchFrequency = 1.15f;
                    p.texturingNoiseFrequency = 4.0f;
                    p.texturingNoiseBlend = 0.48f;
                    p.baseSmoothness = 0.09f;
                    SetBumpNormalized(p, 0.47f, 0.42f, 1.0f);
                    break;

                case RockPresetType.RedJasperChertNodule:
                    p.exportName = "RedJasperChertNodule";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.08f, 0.78f, 0.96f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.25f;
                    p.macroNoiseStrength = 0.12f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.75f;
                    p.noiseStrength = 0.025f;
                    p.octaves = 2;
                    p.useVoronoi = true;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F2MinusF1;
                    p.voronoiFrequency = 1.15f;
                    p.voronoiIntensity = 0.18f;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.52f, 0.08f, 0.045f);
                    p.secondaryColor = new Color(0.78f, 0.28f, 0.12f);
                    p.tertiaryColor = new Color(0.28f, 0.055f, 0.035f);
                    p.cavityColor = new Color(0.14f, 0.025f, 0.018f);
                    p.cavityStrength = 0.42f;
                    p.patchFrequency = 0.8f;
                    p.texturingNoiseFrequency = 3.0f;
                    p.texturingNoiseBlend = 0.32f;
                    p.baseSmoothness = 0.11f;
                    SetBumpNormalized(p, 0.25f, 0.47f, 1.0f);
                    break;

                case RockPresetType.TravertineTerraceLimestone:
                    p.exportName = "TravertineTerraceLimestone";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.45f, 0.7f, 1.2f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.3f;
                    p.macroNoiseStrength = 0.2f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.9f;
                    p.noiseStrength = 0.05f;
                    p.octaves = 3;
                    p.useTerracing = true;
                    p.terraceCount = 12;
                    p.terraceIntensity = 0.62f;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.82f, 0.74f, 0.55f);
                    p.secondaryColor = new Color(0.96f, 0.88f, 0.68f);
                    p.tertiaryColor = new Color(0.58f, 0.46f, 0.28f);
                    p.cavityColor = new Color(0.36f, 0.29f, 0.18f);
                    p.cavityStrength = 0.55f;
                    p.texturingNoiseFrequency = 6.5f;
                    p.texturingNoiseBlend = 0.2f;
                    p.strataWarpFrequency = 0.55f;
                    p.strataWarpStrength = 0.18f;
                    p.baseSmoothness = 0.035f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 6.0f;
                    p.normalNoiseStrength = 0.22f;
                    break;

                case RockPresetType.PinkRhyoliteTuff:
                    p.exportName = "PinkRhyoliteTuff";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.25f, 0.85f, 1.05f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.4f;
                    p.macroNoiseStrength = 0.28f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 1.05f;
                    p.noiseStrength = 0.08f;
                    p.octaves = 3;
                    p.lacunarity = 1.7f;
                    p.persistence = 0.36f;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.70f, 0.42f, 0.42f);
                    p.secondaryColor = new Color(0.86f, 0.62f, 0.54f);
                    p.tertiaryColor = new Color(0.50f, 0.36f, 0.30f);
                    p.cavityColor = new Color(0.30f, 0.19f, 0.16f);
                    p.cavityStrength = 0.58f;
                    p.patchFrequency = 1.6f;
                    p.texturingNoiseFrequency = 7.0f;
                    p.texturingNoiseBlend = 0.62f;
                    p.baseSmoothness = 0.025f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 7.5f;
                    p.normalNoiseStrength = 0.34f;
                    break;

                case RockPresetType.PyriteSlateSlab:
                    p.exportName = "PyriteSlateSlab";
                    p.baseShape = RockBaseShape.CubeSphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(2.05f, 0.32f, 1.55f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.24f;
                    p.macroNoiseStrength = 0.14f;
                    p.macroNoiseOctaves = 2;
                    p.detailFractalType = RockSettings.RockFractalMode.Ridged;
                    p.noiseFrequency = 0.8f;
                    p.noiseStrength = 0.045f;
                    p.octaves = 3;
                    p.useTerracing = true;
                    p.terraceCount = 26;
                    p.terraceIntensity = 0.7f;
                    p.colorPattern = RockColorPattern.SedimentaryStrata;
                    p.primaryColor = new Color(0.08f, 0.095f, 0.11f);
                    p.secondaryColor = new Color(0.16f, 0.18f, 0.20f);
                    p.tertiaryColor = new Color(0.035f, 0.04f, 0.05f);
                    p.cavityColor = new Color(0.018f, 0.02f, 0.024f);
                    p.cavityStrength = 0.75f;
                    p.texturingNoiseFrequency = 9.0f;
                    p.texturingNoiseBlend = 0.08f;
                    p.strataWarpFrequency = 0.7f;
                    p.strataWarpStrength = 0.25f;
                    p.metallicStyle = RockMetallicStyle.Veins;
                    p.oreColor = new Color(0.95f, 0.72f, 0.24f);
                    p.oreFrequency = 4.0f;
                    p.oreCoverage = 0.45f;
                    p.oreMetallic = 1.0f;
                    p.oreSmoothness = 0.72f;
                    p.baseSmoothness = 0.035f;
                    SetBumpNormalized(p, 0.6f, 0.18f, 1.0f);
                    break;

                case RockPresetType.HematiteIronOreNodule:
                    p.exportName = "HematiteIronOreNodule";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.05f, 0.92f, 0.95f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.36f;
                    p.macroNoiseStrength = 0.34f;
                    p.macroNoiseOctaves = 2;
                    p.detailFractalType = RockSettings.RockFractalMode.Ridged;
                    p.noiseFrequency = 0.9f;
                    p.noiseStrength = 0.12f;
                    p.octaves = 3;
                    p.useVoronoi = true;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F2MinusF1;
                    p.voronoiFrequency = 1.6f;
                    p.voronoiIntensity = 0.28f;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.slopeMode = RockSlopeMode.UpwardAndDownward;
                    p.slopeThreshold = 0.35f;
                    p.slopeSmoothness = 0.55f;
                    p.primaryColor = new Color(0.16f, 0.055f, 0.045f);
                    p.secondaryColor = new Color(0.34f, 0.10f, 0.065f);
                    p.cavityColor = new Color(0.035f, 0.028f, 0.026f);
                    p.cavityStrength = 0.85f;
                    p.metallicStyle = RockMetallicStyle.CavityDeposits;
                    p.oreColor = new Color(0.34f, 0.32f, 0.30f);
                    p.oreFrequency = 3.5f;
                    p.oreCoverage = 0.36f;
                    p.oreMetallic = 0.95f;
                    p.oreSmoothness = 0.48f;
                    p.baseSmoothness = 0.05f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 6.0f;
                    p.normalNoiseStrength = 0.26f;
                    break;

                case RockPresetType.LapisPyriteBoulder:
                    p.exportName = "LapisPyriteBoulder";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.15f, 0.92f, 1.08f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.3f;
                    p.macroNoiseStrength = 0.18f;
                    p.macroNoiseOctaves = 2;
                    p.noiseFrequency = 0.85f;
                    p.noiseStrength = 0.04f;
                    p.octaves = 2;
                    p.colorPattern = RockColorPattern.OrganicPatches;
                    p.primaryColor = new Color(0.025f, 0.10f, 0.38f);
                    p.secondaryColor = new Color(0.03f, 0.17f, 0.62f);
                    p.tertiaryColor = new Color(0.78f, 0.76f, 0.68f);
                    p.cavityColor = new Color(0.012f, 0.035f, 0.12f);
                    p.cavityStrength = 0.4f;
                    p.patchFrequency = 1.1f;
                    p.texturingNoiseFrequency = 4.5f;
                    p.texturingNoiseBlend = 0.5f;
                    p.metallicStyle = RockMetallicStyle.CrystallineNodules;
                    p.oreColor = new Color(0.98f, 0.74f, 0.22f);
                    p.oreFrequency = 7.0f;
                    p.oreCoverage = 0.14f;
                    p.oreMetallic = 1.0f;
                    p.oreSmoothness = 0.78f;
                    p.baseSmoothness = 0.08f;
                    SetBumpNormalized(p, 0.5f, 0.25f, 1.0f);
                    break;

                case RockPresetType.SulfurStainedFumaroleRock:
                    p.exportName = "SulfurStainedFumaroleRock";
                    p.baseShape = RockBaseShape.Icosphere;
                    p.randomizeProportions = false;
                    p.baseProportions = new Vector3(1.2f, 0.8f, 1.1f);
                    p.useMacroNoise = true;
                    p.macroNoiseFrequency = 0.42f;
                    p.macroNoiseStrength = 0.3f;
                    p.macroNoiseOctaves = 2;
                    p.detailFractalType = RockSettings.RockFractalMode.Ridged;
                    p.noiseFrequency = 1.1f;
                    p.noiseStrength = 0.12f;
                    p.octaves = 3;
                    p.useVoronoi = true;
                    p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F1;
                    p.voronoiFrequency = 3.0f;
                    p.voronoiIntensity = 0.22f;
                    p.colorPattern = RockColorPattern.SlopeAndCavity;
                    p.slopeMode = RockSlopeMode.UpwardAndDownward;
                    p.slopeThreshold = 0.42f;
                    p.slopeSmoothness = 0.48f;
                    p.primaryColor = new Color(0.48f, 0.46f, 0.38f);
                    p.secondaryColor = new Color(0.92f, 0.76f, 0.16f);
                    p.cavityColor = new Color(0.27f, 0.21f, 0.08f);
                    p.cavityStrength = 0.72f;
                    p.texturingNoiseFrequency = 5.0f;
                    p.texturingNoiseBlend = 0.35f;
                    p.baseSmoothness = 0.025f;
                    p.useNormalPerturbation = true;
                    p.normalNoiseFrequency = 8.0f;
                    p.normalNoiseStrength = 0.34f;
                    break;
            }

            ApplyDefaultLODTransitions(p);
        }

        private static void ResetToDefaults(RockSettings p)
        {
            p.baseShape = RockBaseShape.CubeSphere;
            p.rockType = RockType.Custom;

            p.targetDiameter = 2.0f;
            p.prefabScale = 1.0f;
            p.randomizeProportions = true;
            p.minRandomProportions = new Vector3(0.5f, 0.5f, 0.5f);
            p.maxRandomProportions = new Vector3(1.7f, 1.7f, 1.7f);
            p.baseProportions = Vector3.one;

            p.colorizationMethod = RockColorizationMethod.ProceduralTextureBake;
            p.colorPattern = RockColorPattern.SlopeAndCavity;
            p.textureResolution = 1024;
            p.primaryColor = new Color(0.4f, 0.42f, 0.45f);
            p.secondaryColor = new Color(0.35f, 0.45f, 0.3f);
            p.tertiaryColor = new Color(0.45f, 0.35f, 0.25f);
            p.cavityColor = new Color(0.15f, 0.14f, 0.13f);

            p.slopeMode = RockSlopeMode.UpwardOnly;
            p.slopeThreshold = 0.5f;
            p.slopeSmoothness = 0.2f;

            p.texturingNoiseFrequency = 4.5f;
            p.texturingNoiseBlend = 0.5f;
            p.cavityStrength = 0.8f;

            p.strataWarpFrequency = 2.0f;
            p.strataWarpStrength = 0.5f;
            p.patchFrequency = 1.0f;

            p.useNormalPerturbation = true;
            p.normalNoiseFrequency = 8.0f;
            p.normalNoiseStrength = 0.4f;
            p.normalMapStrength = 1.0f;

            p.useMicroDetail = false;
            p.microDetailFrequency = 50.0f;
            p.microDetailStrength = 0.4f;

            p.normalMapResolution = RockMapResolution.Full;
            p.auxiliaryMapResolution = RockMapResolution.Full;
            p.textureExportMode = RockTextureExportMode.PackedMaskMap;

            p.generateAO = false;
            p.aoStrength = 0.5f;
            p.generateHeight = false;
            p.generateSmoothness = false;
            p.baseSmoothness = 0.05f;

            p.metallicStyle = RockMetallicStyle.None;
            p.oreColor = new Color(0.85f, 0.75f, 0.3f);
            p.oreFrequency = 3.0f;
            p.oreCoverage = 0.3f;
            p.oreMetallic = 1.0f;
            p.oreSmoothness = 0.6f;

            p.uvScale = 1.0f;
            p.uvBlendSharpness = 4.0f;

            p.inputTextureScale = 5.0f;
            p.inputAlbedo = null;
            p.inputNormal = null;

            p.useMacroNoise = true;
            p.useDomainWarping = false;
            p.useVoronoi = false;
            p.useTerracing = false;

            p.macroFractalType = RockSettings.RockFractalMode.Standard;
            p.detailFractalType = RockSettings.RockFractalMode.Standard;
            p.voronoiOutputType = RockSettings.RockVoronoiOutputType.F1;
            p.voronoiMetric = RockVoronoiMetric.Euclidean;

            p.macroNoiseFrequency = 0.4f;
            p.macroNoiseStrength = 0.5f;
            p.macroNoiseOctaves = 3;

            p.noiseFrequency = 1.0f;
            p.noiseStrength = 0.3f;
            p.octaves = 5;
            p.lacunarity = 2.0f;
            p.persistence = 0.5f;

            p.warpStrength = 0.5f;
            p.warpFrequency = 1.0f;
            p.voronoiFrequency = 3.0f;
            p.voronoiIntensity = 0.5f;

            p.terraceCount = 10;
            p.terraceIntensity = 0.7f;

            p.colliderType = RockColliderType.ConvexMesh;
            p.colliderLODIndex = 1;
        }

        public static string GetPresetDisplayName(RockPresetType preset)
        {
            if (preset == RockPresetType.None)
            {
                return "None";
            }

            string raw = preset.ToString();
            System.Text.StringBuilder builder = new System.Text.StringBuilder(raw.Length + 8);

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];

                if (i > 0 && char.IsUpper(c))
                {
                    char previous = raw[i - 1];
                    bool previousIsLowerOrDigit = char.IsLower(previous) || char.IsDigit(previous);
                    bool acronymBoundary = char.IsUpper(previous) && i + 1 < raw.Length && char.IsLower(raw[i + 1]);

                    if (previousIsLowerOrDigit || acronymBoundary)
                    {
                        builder.Append(' ');
                    }
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        public static string GetPresetDescription(RockPresetType preset)
        {
            switch (preset)
            {
                case RockPresetType.DesertSandstone:
                    return "Warm layered sandstone with broad sediment bands. Good for deserts, canyons, dry cliffs, and eroded shelf rocks.";

                case RockPresetType.VolcanicObsidian:
                    return "Dark glossy volcanic stone with sharper fractured forms. Good for lava fields, volcanic caves, fantasy ruins, and alien terrain.";

                case RockPresetType.ColumnarBasaltFragment:
                    return "Tall angular basalt column fragment with darker volcanic coloring. Good for cliffs, broken columns, and stylized volcanic environments.";

                case RockPresetType.GoldVeinedQuartz:
                    return "Pale quartz with metallic gold-style vein deposits. Good for ore props, mining scenes, fantasy pickups, and exposed mineral boulders.";

                case RockPresetType.AlienGeode:
                    return "A stylized mineral/geode-like preset with strong color contrast and shiny crystalline deposits. Good for alien biomes and fantasy set dressing.";

                case RockPresetType.MossyRiverBoulder:
                    return "Rounded wet-looking boulder with mossy upward color placement. Good for riverbanks, forests, wetlands, and stream beds.";

                case RockPresetType.TexturedGranite:
                    return "General-purpose speckled granite with stronger baked texture variation. Good for neutral boulders, cliffs, and reusable environment rocks.";

                case RockPresetType.FrostRimedAlpineSpire:
                    return "Tall cold-weather rock spire with bright frost or snow-like upward tinting. Good for alpine, tundra, and high-mountain scenes.";

                case RockPresetType.BrittleShaleSlab:
                    return "Thin layered shale slab with pronounced stratification. Good for broken sedimentary plates, cliff debris, and dry rocky ground cover.";

                case RockPresetType.SunBleachedDesertPillar:
                    return "Tall sun-worn desert pillar with warm eroded strata. Good for badlands, mesas, desert ruins, and vertical landmark rocks.";

                case RockPresetType.FoldedMetamorphicSchist:
                    return "Folded dark metamorphic rock with warped banding. Good for mountain cliffs, quarry faces, and compressed geological layers.";

                case RockPresetType.DeepSeaHydrothermalVent:
                    return "Dark jagged vent-like rock with mineral deposits. Good for underwater vents, volcanic sea floors, caves, and alien geology.";

                case RockPresetType.LichenCrustedLimestone:
                    return "Light limestone with organic lichen-like patching. Good for temperate ruins, forests, old walls, and damp exposed rock.";

                case RockPresetType.PorousVolcanicPumice:
                    return "Light porous volcanic rock with cavity-heavy breakup. Good for ash fields, volcanic beaches, and lightweight eroded stones.";

                case RockPresetType.BandedIronFormation:
                    return "Red-brown layered iron-rich stone with metallic banding. Good for mineral cliffs, ore seams, and exposed sedimentary deposits.";

                case RockPresetType.ScorchedMeteorite:
                    return "Dark burnt meteorite-like rock with hot rusty coloration and metallic deposits. Good for impact sites and sci-fi props.";

                case RockPresetType.GeometricBismuthCluster:
                    return "Stylized geometric mineral cluster with saturated color and metallic behavior. Good for fantasy, sci-fi, and collectible mineral props.";

                case RockPresetType.LayeredCanyonSandstone:
                    return "Readable canyon sandstone with lighter sediment layers. Good for desert cliffs, canyon floors, and warm rock formations.";

                case RockPresetType.ScouredGlacialErratic:
                    return "Pale glacial boulder with smooth scoured surfaces. Good for alpine valleys, tundra, moraine fields, and cold landscapes.";

                case RockPresetType.HardpackedMudstone:
                    return "Dry compact mudstone with earthy colors and restrained bump. Good for badlands, dry riverbeds, and grounded sedimentary props.";

                case RockPresetType.WeatheredChalkCliffBlock:
                    return "Soft pale chalk block with weathered strata. Good for coastal cliffs, quarry blocks, pale ruins, and sedimentary outcrops.";

                case RockPresetType.RiverWornFlatBoulder:
                    return "Flat rounded river boulder with muted water-worn color patches. Good for streams, shores, wetlands, and stepping-stone layouts.";

                case RockPresetType.OxidizedCopperMalachite:
                    return "Green oxidized copper-style rock with metallic ore coloration. Good for mine entrances, mineral props, fantasy caves, and ore deposits.";

                case RockPresetType.CoralLimestoneBlock:
                    return "Warm porous limestone with organic coral-like surface variation. Good for reefs, beaches, coastal ruins, and tropical stone.";

                case RockPresetType.RawRoseQuartzMass:
                    return "Soft pink quartz-like mineral mass with smoother crystalline color. Good for fantasy caves, collectible stones, and stylized mineral props.";

                case RockPresetType.SpeckledWhiteGranite:
                    return "Light speckled granite with high patch frequency. Good for neutral boulders, gravel piles, mountain rocks, and reusable props.";

                case RockPresetType.SwirlingMarble:
                    return "Smooth marble-like rock with warped bands. Good for decorative stone, ruins, temples, fantasy props, and polished natural formations.";

                case RockPresetType.GreenSerpentiniteBoulder:
                    return "Green metamorphic boulder with organic patching and subtle shine. Good for unusual cliffs, fantasy terrain, and mineral-rich environments.";

                case RockPresetType.RedJasperChertNodule:
                    return "Red jasper/chert-like nodule with smoother mineral coloring. Good for desert props, collectible stones, and polished mineral variation.";

                case RockPresetType.TravertineTerraceLimestone:
                    return "Layered cream limestone with terrace-like sediment structure. Good for hot springs, caves, cliffs, and calcium-rich formations.";

                case RockPresetType.PinkRhyoliteTuff:
                    return "Pink volcanic tuff with patchy warm coloration. Good for volcanic deserts, stylized cliffs, and unusual terrain palettes.";

                case RockPresetType.PyriteSlateSlab:
                    return "Flat dark slate with metallic pyrite-style inclusions. Good for mines, shale shelves, dark cliffs, and ore-bearing slabs.";

                case RockPresetType.HematiteIronOreNodule:
                    return "Dense red-black iron ore nodule with darker metallic deposits. Good for mining scenes, resource nodes, and mineral-rich terrain.";

                case RockPresetType.LapisPyriteBoulder:
                    return "Deep blue lapis-style boulder with gold pyrite-like nodules. Good for fantasy caves, premium ore props, and colorful mineral rocks.";

                case RockPresetType.SulfurStainedFumaroleRock:
                    return "Sulfur-stained volcanic rock with yellow surface coloration. Good for fumaroles, vents, hot springs, volcanic fields, and toxic biomes.";

                case RockPresetType.None:
                default:
                    return "No preset selected.";
            }
        }

        public static string GetPresetTooltip(RockPresetType preset)
        {
            if (preset == RockPresetType.None)
            {
                return "No preset selected.";
            }

            return GetPresetDisplayName(preset) +
                   "\n\n" +
                   GetPresetDescription(preset) +
                   "\n\nLite presets are authored around a 2m Target Diameter. Use Prefab Scale when you want the same generated rock larger or smaller without retuning the procedural texture and bump relationships.";
        }
    }
}
#endif
