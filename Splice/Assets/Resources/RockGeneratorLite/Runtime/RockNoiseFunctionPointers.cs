using Unity.Burst;
using Unity.Mathematics;

namespace Veridian.RockGenLite.Noise
{
    [BurstCompile]
    public static class RockNoiseFunctionPointers
    {
        public static readonly FunctionPointer<RockNoise3DFunction> Simplex3D;
        public static readonly FunctionPointer<RockNoise3DFunction> Voronoi3D;

        static RockNoiseFunctionPointers()
        {
            Simplex3D = BurstCompiler.CompileFunctionPointer<RockNoise3DFunction>(Simplex3DWrapper);
            Voronoi3D = BurstCompiler.CompileFunctionPointer<RockNoise3DFunction>(Voronoi3DWrapper);
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(RockNoise3DFunction))]
        public static float Simplex3DWrapper(in float3 point, float frequency, int seed)
        {
            return RockNoiseCore.GetSimplex3D(in point, frequency, seed);
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(RockNoise3DFunction))]
        public static float Voronoi3DWrapper(in float3 point, float frequency, int seed)
        {
            return RockNoiseCore.GetVoronoi3D(in point, frequency, seed, 1.0f, RockNoiseType.VoronoiF1, true, RockVoronoiMetric.Euclidean);
        }
    }
}