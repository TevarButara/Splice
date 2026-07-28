using Unity.Mathematics;
using UnityEngine;
using Veridian.RockGenLite.Noise;

namespace Veridian.RockGenLite.Runtime
{
    [System.Serializable]
    public struct RockShapeData
    {
        public int Seed;
        public float3 BaseShapeScale;
        public float3 OverallScale;

        public bool UseMacroNoise;
        public bool UseDomainWarping;
        public bool UseVoronoi;
        public bool UseTerracing;

        public float MacroNoiseFrequency;
        public float MacroNoiseStrength;
        public RockFBMConfig MacroFBMConfig;

        public float NoiseFrequency;
        public float NoiseStrength;
        public RockFBMConfig BaseFBMConfig;

        public float WarpStrength;
        public float WarpFrequency;
        public RockFBMConfig WarpFBMConfig;

        public float VoronoiFrequency;
        public float VoronoiIntensity;
        public RockNoiseType VoronoiOutputType;
        public RockVoronoiMetric VoronoiMetric;
        public int TerraceCount;
        public float TerraceIntensity;

        public static RockShapeData FromSettings(RockSettings settings)
        {
            float lacunarity = Mathf.Max(1.0f, settings.lacunarity);
            float persistence = Mathf.Clamp01(settings.persistence);

            RockNoiseType mappedVoronoiType = RockNoiseType.VoronoiF1;
            switch (settings.voronoiOutputType)
            {
                case RockSettings.RockVoronoiOutputType.F2: mappedVoronoiType = RockNoiseType.VoronoiF2; break;
                case RockSettings.RockVoronoiOutputType.F2MinusF1: mappedVoronoiType = RockNoiseType.VoronoiF2F1; break;
                case RockSettings.RockVoronoiOutputType.Edge: mappedVoronoiType = RockNoiseType.VoronoiEdge; break;
            }

            float3 finalProportions;
            if (settings.randomizeProportions)
            {
                uint safeSeed = (uint)settings.seed ^ 0x45D9F3Bu;
                if (safeSeed == 0) safeSeed = 1u;

                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(safeSeed);

                float3 safeMin = math.min(settings.minRandomProportions, settings.maxRandomProportions);
                float3 safeMax = math.max(settings.minRandomProportions, settings.maxRandomProportions);

                finalProportions = rng.NextFloat3(safeMin, safeMax);
            }
            else
            {
                finalProportions = settings.baseProportions;
            }

            float targetRadius = Mathf.Max(0.01f, settings.targetDiameter) / 2.0f;

            return new RockShapeData
            {
                Seed = settings.seed,
                BaseShapeScale = math.max(new float3(0.001f), finalProportions),
                OverallScale = new float3(targetRadius, targetRadius, targetRadius),

                UseMacroNoise = settings.useMacroNoise,
                UseDomainWarping = settings.useDomainWarping,
                UseVoronoi = settings.useVoronoi,
                UseTerracing = settings.useTerracing,

                MacroNoiseFrequency = Mathf.Max(0.001f, settings.macroNoiseFrequency),
                MacroNoiseStrength = Mathf.Max(0f, settings.macroNoiseStrength),
                MacroFBMConfig = new RockFBMConfig { Octaves = Mathf.Clamp(settings.macroNoiseOctaves, 1, 10), Lacunarity = lacunarity, Persistence = persistence, FractalType = (RockFractalType)(int)settings.macroFractalType, ErosionStrength = 0.8f },

                NoiseFrequency = Mathf.Max(0.001f, settings.noiseFrequency),
                NoiseStrength = Mathf.Max(0f, settings.noiseStrength),
                BaseFBMConfig = new RockFBMConfig { Octaves = Mathf.Clamp(settings.octaves, 1, 10), Lacunarity = lacunarity, Persistence = persistence, FractalType = (RockFractalType)(int)settings.detailFractalType, ErosionStrength = 0.8f },

                WarpStrength = Mathf.Max(0f, settings.warpStrength),
                WarpFrequency = Mathf.Max(0.001f, settings.warpFrequency),
                WarpFBMConfig = new RockFBMConfig { Octaves = 3, Lacunarity = 2.0f, Persistence = 0.5f, FractalType = RockFractalType.Flow },

                VoronoiFrequency = Mathf.Max(0.001f, settings.voronoiFrequency),
                VoronoiIntensity = settings.voronoiIntensity,
                VoronoiOutputType = mappedVoronoiType,
                VoronoiMetric = settings.voronoiMetric,

                // We no longer need Mathf.RoundToInt since terraceCount is now an int
                TerraceCount = Mathf.Max(1, settings.terraceCount),
                TerraceIntensity = settings.terraceIntensity
            };
        }
    }
}