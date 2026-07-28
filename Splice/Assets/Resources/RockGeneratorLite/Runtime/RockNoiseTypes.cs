using System;
using Unity.Mathematics;

namespace Veridian.RockGenLite.Noise
{
    public enum RockNoiseOutputRange { Unnormalized11, Normalized01 }

    public enum RockNoiseType
    {
        WhiteNoise, Value, Perlin, Simplex,
        VoronoiF1, VoronoiF2, VoronoiF2F1, VoronoiF1DivF2, VoronoiCellID, VoronoiEdge,
        Flow
    }

    public enum RockFractalType { Standard, Billow, Ridged, PingPong, SwissErosion, Flow, Hybrid }
    public enum RockVoronoiMetric { Euclidean, Manhattan, Chebyshev }
    public enum RockBlendMode { Overwrite, Add, Subtract, Multiply, Divide, Min, Max, Overlay, SmoothMin, SmoothMax }
    public enum RockSDFBooleanMode { Union, Subtraction, Intersection, SmoothUnion, SmoothSubtraction, SmoothIntersection }

    public delegate float RockNoise2DFunction(in float2 point, float frequency, int seed);
    public delegate float RockNoise3DFunction(in float3 point, float frequency, int seed);

    [Serializable]
    public struct RockFBMConfig
    {
        public int Octaves;
        public float Lacunarity;
        public float Persistence;
        public RockFractalType FractalType;
        public float ErosionStrength;
        public float FlowWarpStrength;
        public float HybridOffset;

        public static RockFBMConfig Default() => new RockFBMConfig
        {
            Octaves = 5,
            Lacunarity = 2.0f,
            Persistence = 0.5f,
            FractalType = RockFractalType.Standard,
            ErosionStrength = 0.5f,
            FlowWarpStrength = 0.25f,
            HybridOffset = 0.5f
        };
    }

    [Serializable]
    public struct RockVoronoiCellData
    {
        public float2 Centroid;
        public uint ID;
        public float DistanceF1;
        public int2 GridCoords;
    }
}