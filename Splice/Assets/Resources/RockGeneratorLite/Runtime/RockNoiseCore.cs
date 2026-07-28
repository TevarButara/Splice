using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Veridian.RockGenLite.Noise
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    public static class RockNoiseCore
    {
        public const float UINT_TO_FLOAT_NORM = 1.0f / 4294967295.0f;
        private static readonly float F2 = 0.5f * (sqrt(3.0f) - 1.0f);
        private static readonly float G2 = (3.0f - sqrt(3.0f)) / 6.0f;
        private const float F3 = 1.0f / 3.0f;
        private const float G3 = 1.0f / 6.0f;

        #region Hashing & Math Utilities
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint input)
        {
            uint state = input * 747796405u + 2891336453u;
            uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            return (word >> 22) ^ word;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Hash1D_01(int x, int seed) => (float)Hash((uint)(x ^ seed)) * UINT_TO_FLOAT_NORM;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Hash1D_11(int x, int seed) => Hash1D_01(x, seed) * 2.0f - 1.0f;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static uint Hash2D_ID(in int2 p, int seed) => Hash((uint)seed ^ (uint)p.x * 1597334677u + (uint)p.y * 3812015801u);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Hash2D_01(in int2 p, int seed)
        {
            uint s = (uint)seed;
            uint hx = Hash((uint)p.x ^ s * 3923971u + (uint)p.y ^ s * 5924977u);
            uint hy = Hash(hx * 1664525u + 1013904223u);
            return new float2((float)hx * UINT_TO_FLOAT_NORM, (float)hy * UINT_TO_FLOAT_NORM);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Hash3D_01(in int3 p, int seed)
        {
            uint s = (uint)seed;
            uint hx = Hash((uint)p.x ^ s * 3923971u + (uint)p.y ^ s * 5924977u + (uint)p.z ^ s * 83492791u);
            uint hy = Hash(hx * 1664525u + 1013904223u);
            uint hz = Hash(hy * 1664525u + 1013904223u);
            return new float3((float)hx * UINT_TO_FLOAT_NORM, (float)hy * UINT_TO_FLOAT_NORM, (float)hz * UINT_TO_FLOAT_NORM);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Hash2D_Gradient(in int2 p, int seed)
        {
            uint h = Hash2D_ID(in p, seed);
            sincos((float)h * UINT_TO_FLOAT_NORM * 6.28318530718f, out float s, out float c);
            return new float2(c, s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Hash3D_Gradient(in int3 p, int seed)
        {
            uint hx = Hash((uint)seed ^ (uint)p.x * 1597334677u + (uint)p.y * 3812015801u + (uint)p.z * 217645177u);
            uint hy = Hash(hx * 1664525u + 1013904223u);
            uint hz = Hash(hy * 1664525u + 1013904223u);
            float3 gradient = new float3((float)hx * UINT_TO_FLOAT_NORM * 2.0f - 1.0f, (float)hy * UINT_TO_FLOAT_NORM * 2.0f - 1.0f, (float)hz * UINT_TO_FLOAT_NORM * 2.0f - 1.0f);
            float lenSq = lengthsq(gradient);
            return select(gradient * rsqrt(max(lenSq, 1e-5f)), new float3(1.0f, 0.0f, 0.0f), lenSq < 1e-6f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float2 Fade(in float2 t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float3 Fade(in float3 t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float2 FadeDeriv(in float2 t) => 30.0f * t * t * (t * (t - 2.0f) + 1.0f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float3 FadeDeriv(in float3 t) => 30.0f * t * t * (t * (t - 2.0f) + 1.0f);
        #endregion

        #region White Noise
        [BurstCompile]
        public static float GetWhiteNoise2D(in float2 point, float frequency, int seed)
        {
            int2 pos = (int2)floor(point * frequency);
            return (float)Hash2D_ID(in pos, seed) * UINT_TO_FLOAT_NORM * 2.0f - 1.0f;
        }

        [BurstCompile]
        public static void GetWhiteNoise2D_Deriv(in float2 point, float frequency, int seed, out float3 result)
            => result = new float3(0f, 0f, GetWhiteNoise2D(in point, frequency, seed));

        [BurstCompile]
        public static float GetWhiteNoise3D(in float3 point, float frequency, int seed)
        {
            int3 pos = (int3)floor(point * frequency);
            uint h = Hash((uint)seed ^ (uint)pos.x * 1597334677u + (uint)pos.y * 3812015801u + (uint)pos.z * 217645177u);
            return (float)h * UINT_TO_FLOAT_NORM * 2.0f - 1.0f;
        }

        [BurstCompile]
        public static void GetWhiteNoise3D_Deriv(in float3 point, float frequency, int seed, out float4 result)
            => result = new float4(0f, 0f, 0f, GetWhiteNoise3D(in point, frequency, seed));
        #endregion

        #region Value Noise
        [BurstCompile]
        public static float GetValue2D(in float2 point, float frequency, int seed)
        {
            float2 p = point * frequency; int2 i = (int2)floor(p); float2 u = Fade(frac(p));
            return lerp(lerp(Hash1D_11(i.x + i.y * 57, seed), Hash1D_11(i.x + 1 + i.y * 57, seed), u.x),
                        lerp(Hash1D_11(i.x + (i.y + 1) * 57, seed), Hash1D_11(i.x + 1 + (i.y + 1) * 57, seed), u.x), u.y);
        }

        [BurstCompile]
        public static void GetValue2D_Deriv(in float2 point, float frequency, int seed, out float3 result)
        {
            float2 p = point * frequency; int2 i = (int2)floor(p); float2 f = frac(p);
            float2 u = Fade(in f); float2 du = FadeDeriv(in f);
            float a = Hash1D_11(i.x + i.y * 57, seed); float b = Hash1D_11(i.x + 1 + i.y * 57, seed);
            float c = Hash1D_11(i.x + (i.y + 1) * 57, seed); float d = Hash1D_11(i.x + 1 + (i.y + 1) * 57, seed);
            float k0 = a; float k1 = b - a; float k2 = c - a; float k3 = a - b - c + d;
            float val = k0 + k1 * u.x + k2 * u.y + k3 * u.x * u.y;
            float dx = du.x * (k1 + k3 * u.y); float dy = du.y * (k2 + k3 * u.x);
            result = new float3(dx * frequency, dy * frequency, val);
        }

        [BurstCompile]
        public static float GetValue3D(in float3 point, float frequency, int seed)
        {
            float3 p = point * frequency; int3 i = (int3)floor(p); float3 u = Fade(frac(p));
            float y00 = lerp(Hash1D_11(i.x + i.y * 57 + i.z * 137, seed), Hash1D_11(i.x + 1 + i.y * 57 + i.z * 137, seed), u.x);
            float y10 = lerp(Hash1D_11(i.x + (i.y + 1) * 57 + i.z * 137, seed), Hash1D_11(i.x + 1 + (i.y + 1) * 57 + i.z * 137, seed), u.x);
            float y01 = lerp(Hash1D_11(i.x + i.y * 57 + (i.z + 1) * 137, seed), Hash1D_11(i.x + 1 + i.y * 57 + (i.z + 1) * 137, seed), u.x);
            float y11 = lerp(Hash1D_11(i.x + (i.y + 1) * 57 + (i.z + 1) * 137, seed), Hash1D_11(i.x + 1 + (i.y + 1) * 57 + (i.z + 1) * 137, seed), u.x);
            return lerp(lerp(y00, y10, u.y), lerp(y01, y11, u.y), u.z);
        }

        [BurstCompile]
        public static void GetValue3D_Deriv(in float3 point, float frequency, int seed, out float4 result)
        {
            float3 p = point * frequency; int3 i = (int3)floor(p); float3 f = frac(p);
            float3 u = Fade(in f); float3 du = FadeDeriv(in f);
            float a = Hash1D_11(i.x + i.y * 57 + i.z * 137, seed); float b = Hash1D_11(i.x + 1 + i.y * 57 + i.z * 137, seed);
            float c = Hash1D_11(i.x + (i.y + 1) * 57 + i.z * 137, seed); float d = Hash1D_11(i.x + 1 + (i.y + 1) * 57 + i.z * 137, seed);
            float e = Hash1D_11(i.x + i.y * 57 + (i.z + 1) * 137, seed); float f_val = Hash1D_11(i.x + 1 + i.y * 57 + (i.z + 1) * 137, seed);
            float g = Hash1D_11(i.x + (i.y + 1) * 57 + (i.z + 1) * 137, seed); float h = Hash1D_11(i.x + 1 + (i.y + 1) * 57 + (i.z + 1) * 137, seed);
            float k0 = a; float k1 = b - a; float k2 = c - a; float k3 = e - a;
            float k4 = a - b - c + d; float k5 = a - b - e + f_val; float k6 = a - c - e + g; float k7 = -a + b + c - d + e - f_val - g + h;
            float val = k0 + k1 * u.x + k2 * u.y + k3 * u.z + k4 * u.x * u.y + k5 * u.x * u.z + k6 * u.y * u.z + k7 * u.x * u.y * u.z;
            float3 grad = new float3(du.x * (k1 + k4 * u.y + k5 * u.z + k7 * u.y * u.z), du.y * (k2 + k4 * u.x + k6 * u.z + k7 * u.x * u.z), du.z * (k3 + k5 * u.x + k6 * u.y + k7 * u.x * u.y));
            result = new float4(grad * frequency, val);
        }
        #endregion

        #region Perlin Noise
        [BurstCompile]
        public static float GetPerlin2D(in float2 point, float frequency, int seed)
        {
            float2 p = point * frequency; int2 pi = (int2)floor(p); float2 pf = frac(p); float2 u = Fade(in pf);
            float n00 = dot(Hash2D_Gradient(pi + new int2(0, 0), seed), pf - new float2(0, 0));
            float n10 = dot(Hash2D_Gradient(pi + new int2(1, 0), seed), pf - new float2(1, 0));
            float n01 = dot(Hash2D_Gradient(pi + new int2(0, 1), seed), pf - new float2(0, 1));
            float n11 = dot(Hash2D_Gradient(pi + new int2(1, 1), seed), pf - new float2(1, 1));
            return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
        }

        [BurstCompile]
        public static void GetPerlin2D_Deriv(in float2 point, float frequency, int seed, out float3 result)
        {
            float2 p = point * frequency; int2 i = (int2)floor(p); float2 f = frac(p); float2 u = Fade(in f); float2 du = FadeDeriv(in f);
            float2 ga = Hash2D_Gradient(i + new int2(0, 0), seed); float2 gb = Hash2D_Gradient(i + new int2(1, 0), seed);
            float2 gc = Hash2D_Gradient(i + new int2(0, 1), seed); float2 gd = Hash2D_Gradient(i + new int2(1, 1), seed);
            float va = dot(ga, f - new float2(0f, 0f)); float vb = dot(gb, f - new float2(1f, 0f));
            float vc = dot(gc, f - new float2(0f, 1f)); float vd = dot(gd, f - new float2(1f, 1f));
            float k0 = va; float k1 = vb - va; float k2 = vc - va; float k3 = va - vb - vc + vd;
            float val = k0 + k1 * u.x + k2 * u.y + k3 * u.x * u.y;
            float2 dxdy = du * new float2(k1 + k3 * u.y, k2 + k3 * u.x) + new float2(ga.x + u.x * (gb.x - ga.x) + u.y * (gc.x - ga.x) + u.x * u.y * (ga.x - gb.x - gc.x + gd.x), ga.y + u.x * (gb.y - ga.y) + u.y * (gc.y - ga.y) + u.x * u.y * (ga.y - gb.y - gc.y + gd.y));
            result = new float3(dxdy * frequency, val);
        }

        [BurstCompile]
        public static float GetPerlin3D(in float3 point, float frequency, int seed)
        {
            float3 p = point * frequency; int3 pi = (int3)floor(p); float3 pf = frac(p); float3 u = Fade(in pf);
            float Dot(int3 offset) => dot(Hash3D_Gradient(pi + offset, seed), pf - (float3)offset);
            float x00 = lerp(Dot(new int3(0, 0, 0)), Dot(new int3(1, 0, 0)), u.x);
            float x10 = lerp(Dot(new int3(0, 1, 0)), Dot(new int3(1, 1, 0)), u.x);
            float x01 = lerp(Dot(new int3(0, 0, 1)), Dot(new int3(1, 0, 1)), u.x);
            float x11 = lerp(Dot(new int3(0, 1, 1)), Dot(new int3(1, 1, 1)), u.x);
            return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
        }

        [BurstCompile]
        public static void GetPerlin3D_Deriv(in float3 point, float frequency, int seed, out float4 result)
        {
            float3 p = point * frequency; int3 pi = (int3)floor(p); float3 f = frac(p); float3 u = Fade(in f); float3 du = FadeDeriv(in f);
            float3 ga = Hash3D_Gradient(pi + new int3(0, 0, 0), seed); float3 gb = Hash3D_Gradient(pi + new int3(1, 0, 0), seed);
            float3 gc = Hash3D_Gradient(pi + new int3(0, 1, 0), seed); float3 gd = Hash3D_Gradient(pi + new int3(1, 1, 0), seed);
            float3 ge = Hash3D_Gradient(pi + new int3(0, 0, 1), seed); float3 gf = Hash3D_Gradient(pi + new int3(1, 0, 1), seed);
            float3 gg = Hash3D_Gradient(pi + new int3(0, 1, 1), seed); float3 gh = Hash3D_Gradient(pi + new int3(1, 1, 1), seed);
            float va = dot(ga, f - new float3(0f, 0f, 0f)); float vb = dot(gb, f - new float3(1f, 0f, 0f));
            float vc = dot(gc, f - new float3(0f, 1f, 0f)); float vd = dot(gd, f - new float3(1f, 1f, 0f));
            float ve = dot(ge, f - new float3(0f, 0f, 1f)); float vf = dot(gf, f - new float3(1f, 0f, 1f));
            float vg = dot(gg, f - new float3(0f, 1f, 1f)); float vh = dot(gh, f - new float3(1f, 1f, 1f));
            float k0 = va; float k1 = vb - va; float k2 = vc - va; float k3 = ve - va;
            float k4 = va - vb - vc + vd; float k5 = va - vb - ve + vf; float k6 = va - vc - ve + vg; float k7 = -va + vb + vc - vd + ve - vf - vg + vh;
            float val = k0 + k1 * u.x + k2 * u.y + k3 * u.z + k4 * u.x * u.y + k5 * u.x * u.z + k6 * u.y * u.z + k7 * u.x * u.y * u.z;
            float3 dW = new float3(du.x * (k1 + k4 * u.y + k5 * u.z + k7 * u.y * u.z), du.y * (k2 + k4 * u.x + k6 * u.z + k7 * u.x * u.z), du.z * (k3 + k5 * u.x + k6 * u.y + k7 * u.x * u.y));
            float3 dD = lerp(lerp(lerp(ga, gb, u.x), lerp(gc, gd, u.x), u.y), lerp(lerp(ge, gf, u.x), lerp(gg, gh, u.x), u.y), u.z);
            result = new float4((dW + dD) * frequency, val);
        }
        #endregion

        #region Simplex Noise
        [BurstCompile]
        public static float GetSimplex2D(in float2 point, float frequency, int seed)
        {
            float2 p = point * frequency; float s = (p.x + p.y) * F2; float2 i = floor(p + s); float t = (i.x + i.y) * G2;
            float2 x0 = p - (i - t); int2 i1 = select(new int2(0, 1), new int2(1, 0), x0.x > x0.y);
            float2 x1 = x0 - (float2)i1 + G2; float2 x2 = x0 - 1.0f + 2.0f * G2;
            float n = 0.0f; const float R2 = 0.5f; int2 i_int = (int2)i;
            float tC0 = max(0.0f, R2 - dot(x0, x0)); tC0 *= tC0; n += tC0 * tC0 * dot(Hash2D_Gradient(in i_int, seed), x0);
            float tC1 = max(0.0f, R2 - dot(x1, x1)); tC1 *= tC1; n += tC1 * tC1 * dot(Hash2D_Gradient(i_int + i1, seed), x1);
            float tC2 = max(0.0f, R2 - dot(x2, x2)); tC2 *= tC2; n += tC2 * tC2 * dot(Hash2D_Gradient(i_int + new int2(1, 1), seed), x2);
            return clamp(70.0f * n, -1f, 1f);
        }

        [BurstCompile]
        public static void GetSimplex2D_Deriv(in float2 point, float frequency, int seed, out float3 result)
        {
            float2 p = point * frequency; float s = (p.x + p.y) * F2; float2 i = floor(p + s); float t_val = (i.x + i.y) * G2;
            float2 x0 = p - (i - t_val); int2 i1 = select(new int2(0, 1), new int2(1, 0), x0.x > x0.y);
            float2 x1 = x0 - (float2)i1 + G2; float2 x2 = x0 - 1.0f + 2.0f * G2;
            float3 tempResult = new float3(0f, 0f, 0f); int2 i_int = new int2((int)i.x, (int)i.y);

            float tC0 = max(0.0f, 0.5f - dot(x0, x0));
            if (tC0 > 0.0f) { float tC0_2 = tC0 * tC0; float tC0_4 = tC0_2 * tC0_2; float2 g0 = Hash2D_Gradient(i_int, seed); float d0 = dot(g0, x0); tempResult.z += tC0_4 * d0; tempResult.xy += tC0_4 * g0 - 8.0f * tC0_2 * tC0 * d0 * x0; }

            float tC1 = max(0.0f, 0.5f - dot(x1, x1));
            if (tC1 > 0.0f) { float tC1_2 = tC1 * tC1; float tC1_4 = tC1_2 * tC1_2; float2 g1 = Hash2D_Gradient(i_int + i1, seed); float d1 = dot(g1, x1); tempResult.z += tC1_4 * d1; tempResult.xy += tC1_4 * g1 - 8.0f * tC1_2 * tC1 * d1 * x1; }

            float tC2 = max(0.0f, 0.5f - dot(x2, x2));
            if (tC2 > 0.0f) { float tC2_2 = tC2 * tC2; float tC2_4 = tC2_2 * tC2_2; float2 g2 = Hash2D_Gradient(i_int + new int2(1, 1), seed); float d2 = dot(g2, x2); tempResult.z += tC2_4 * d2; tempResult.xy += tC2_4 * g2 - 8.0f * tC2_2 * tC2 * d2 * x2; }

            result = new float3(tempResult.xy * 70.0f * frequency, clamp(tempResult.z * 70.0f, -1f, 1f));
        }

        [BurstCompile]
        public static float GetSimplex3D(in float3 point, float frequency, int seed)
        {
            float3 p = point * frequency; float3 i = floor(p + (p.x + p.y + p.z) * F3); float3 x0 = p - (i - (i.x + i.y + i.z) * G3);
            int3 rank = new int3(select(0, 1, x0.x >= x0.y) + select(0, 1, x0.x >= x0.z), select(0, 1, x0.y > x0.x) + select(0, 1, x0.y >= x0.z), select(0, 1, x0.z > x0.x) + select(0, 1, x0.z > x0.y));
            int3 i1 = select(new int3(0), new int3(1), rank >= 2); int3 i2 = select(new int3(0), new int3(1), rank >= 1);
            float3 x1 = x0 - (float3)i1 + G3, x2 = x0 - (float3)i2 + 2.0f * G3, x3 = x0 - 1.0f + 3.0f * G3;
            float n = 0.0f; const float R2 = 0.6f; int3 i_int = (int3)i;

            float tC0 = max(0.0f, R2 - dot(x0, x0)); tC0 *= tC0; n += tC0 * tC0 * dot(Hash3D_Gradient(in i_int, seed), x0);
            float tC1 = max(0.0f, R2 - dot(x1, x1)); tC1 *= tC1; n += tC1 * tC1 * dot(Hash3D_Gradient(i_int + i1, seed), x1);
            float tC2 = max(0.0f, R2 - dot(x2, x2)); tC2 *= tC2; n += tC2 * tC2 * dot(Hash3D_Gradient(i_int + i2, seed), x2);
            float tC3 = max(0.0f, R2 - dot(x3, x3)); tC3 *= tC3; n += tC3 * tC3 * dot(Hash3D_Gradient(i_int + new int3(1, 1, 1), seed), x3);

            return clamp(32.0f * n, -1f, 1f);
        }

        [BurstCompile]
        public static void GetSimplex3D_Deriv(in float3 point, float frequency, int seed, out float4 result)
        {
            float3 p = point * frequency; float3 i = floor(p + (p.x + p.y + p.z) * F3); float3 x0 = p - (i - (i.x + i.y + i.z) * G3);
            int3 rank = new int3(select(0, 1, x0.x >= x0.y) + select(0, 1, x0.x >= x0.z), select(0, 1, x0.y > x0.x) + select(0, 1, x0.y >= x0.z), select(0, 1, x0.z > x0.x) + select(0, 1, x0.z > x0.y));
            int3 i1 = select(new int3(0), new int3(1), rank >= 2); int3 i2 = select(new int3(0), new int3(1), rank >= 1);
            float3 x1 = x0 - (float3)i1 + G3, x2 = x0 - (float3)i2 + 2.0f * G3, x3 = x0 - 1.0f + 3.0f * G3;
            float4 tempResult = new float4(0f, 0f, 0f, 0f); int3 i_int = (int3)i;

            float tC0 = max(0.0f, 0.6f - dot(x0, x0));
            if (tC0 > 0.0f) { float tC0_2 = tC0 * tC0; float tC0_4 = tC0_2 * tC0_2; float3 g0 = Hash3D_Gradient(in i_int, seed); float d0 = dot(g0, x0); tempResult.w += tC0_4 * d0; tempResult.xyz += tC0_4 * g0 - 8.0f * tC0_2 * tC0 * d0 * x0; }

            float tC1 = max(0.0f, 0.6f - dot(x1, x1));
            if (tC1 > 0.0f) { float tC1_2 = tC1 * tC1; float tC1_4 = tC1_2 * tC1_2; float3 g1 = Hash3D_Gradient(i_int + i1, seed); float d1 = dot(g1, x1); tempResult.w += tC1_4 * d1; tempResult.xyz += tC1_4 * g1 - 8.0f * tC1_2 * tC1 * d1 * x1; }

            float tC2 = max(0.0f, 0.6f - dot(x2, x2));
            if (tC2 > 0.0f) { float tC2_2 = tC2 * tC2; float tC2_4 = tC2_2 * tC2_2; float3 g2 = Hash3D_Gradient(i_int + i2, seed); float d2 = dot(g2, x2); tempResult.w += tC2_4 * d2; tempResult.xyz += tC2_4 * g2 - 8.0f * tC2_2 * tC2 * d2 * x2; }

            float tC3 = max(0.0f, 0.6f - dot(x3, x3));
            if (tC3 > 0.0f) { float tC3_2 = tC3 * tC3; float tC3_4 = tC3_2 * tC3_2; float3 g3 = Hash3D_Gradient(i_int + new int3(1, 1, 1), seed); float d3 = dot(g3, x3); tempResult.w += tC3_4 * d3; tempResult.xyz += tC3_4 * g3 - 8.0f * tC3_2 * tC3 * d3 * x3; }

            result = new float4(tempResult.xyz * 32.0f * frequency, clamp(tempResult.w * 32.0f, -1f, 1f));
        }
        #endregion

        #region Voronoi Noise
        [BurstCompile]
        public static float GetVoronoi2D(in float2 point, float frequency, int seed, float jitter, RockNoiseType type, bool normalize, RockVoronoiMetric metric)
        {
            float2 p = point * frequency; int2 gridCoords = (int2)floor(p); float2 fracCoords = frac(p);
            float j = saturate(jitter); float d1 = float.MaxValue, d2 = float.MaxValue; float2 diff1 = new float2(0f), diff2 = new float2(0f);

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    int2 neighborGrid = gridCoords + new int2(x, y);
                    float2 diff = (float2)new int2(x, y) + lerp(new float2(0.5f), Hash2D_01(in neighborGrid, seed), j) - fracCoords;
                    float dist = metric == RockVoronoiMetric.Manhattan ? abs(diff.x) + abs(diff.y) : metric == RockVoronoiMetric.Chebyshev ? max(abs(diff.x), abs(diff.y)) : dot(diff, diff);
                    if (dist < d1) { d2 = d1; diff2 = diff1; d1 = dist; diff1 = diff; }
                    else if (dist < d2) { d2 = dist; diff2 = diff; }
                }
            }

            if (type == RockNoiseType.VoronoiEdge)
            {
                float edgeDist = 0f;
                if (metric == RockVoronoiMetric.Euclidean)
                {
                    float2 cellDiff = diff2 - diff1; float lenSq = dot(cellDiff, cellDiff);
                    if (lenSq > 1e-5f) edgeDist = dot((diff1 + diff2) * 0.5f, cellDiff) * rsqrt(lenSq);
                }
                else { edgeDist = (d2 - d1) * 0.5f; }
                return normalize ? clamp((edgeDist / 0.5f) * 2.0f - 1.0f, -1.0f, 1.0f) : edgeDist;
            }

            float f1 = metric == RockVoronoiMetric.Euclidean ? sqrt(d1) : d1;
            float f2 = metric == RockVoronoiMetric.Euclidean ? sqrt(d2) : d2;

            if (!normalize)
            {
                if (type == RockNoiseType.VoronoiF2) return f2;
                if (type == RockNoiseType.VoronoiF2F1) return f2 - f1;
                if (type == RockNoiseType.VoronoiF1DivF2) return f1 / max(f2, 1e-5f);
                return f1;
            }
            float result = type == RockNoiseType.VoronoiF2 ? (f2 / 1.2f) * 2.0f - 1.0f : type == RockNoiseType.VoronoiF2F1 ? ((f2 - f1) / 0.6f) * 2.0f - 1.0f : type == RockNoiseType.VoronoiF1DivF2 ? (f1 / max(f2, 1e-5f)) * 2.0f - 1.0f : f1 * 2.0f - 1.0f;
            return clamp(result, -1.0f, 1.0f);
        }

        [BurstCompile]
        public static float GetVoronoi3D(in float3 point, float frequency, int seed, float jitter, RockNoiseType type, bool normalize, RockVoronoiMetric metric)
        {
            float3 p = point * frequency; int3 gridCoords = (int3)floor(p); float3 fracCoords = frac(p);
            float d1 = float.MaxValue, d2 = float.MaxValue; float3 diff1 = new float3(0f), diff2 = new float3(0f); float j = saturate(jitter);

            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int3 neighborOffset = new int3(x, y, z); int3 nGrid = gridCoords + neighborOffset;
                        float3 randomPoint = lerp(new float3(0.5f), Hash3D_01(in nGrid, seed), j);
                        float3 diff = (float3)neighborOffset + randomPoint - fracCoords;
                        float dist = metric == RockVoronoiMetric.Manhattan ? abs(diff.x) + abs(diff.y) + abs(diff.z) : metric == RockVoronoiMetric.Chebyshev ? max(max(abs(diff.x), abs(diff.y)), abs(diff.z)) : dot(diff, diff);
                        if (dist < d1) { d2 = d1; diff2 = diff1; d1 = dist; diff1 = diff; }
                        else if (dist < d2) { d2 = dist; diff2 = diff; }
                    }
                }
            }
            if (type == RockNoiseType.VoronoiEdge)
            {
                float edgeDist = 0f;
                if (metric == RockVoronoiMetric.Euclidean)
                {
                    float3 cellDiff = diff2 - diff1; float lenSq = dot(cellDiff, cellDiff);
                    if (lenSq > 1e-5f) edgeDist = dot((diff1 + diff2) * 0.5f, cellDiff) * rsqrt(lenSq);
                }
                else { edgeDist = (d2 - d1) * 0.5f; }
                return normalize ? clamp((edgeDist / 0.5f) * 2.0f - 1.0f, -1.0f, 1.0f) : edgeDist;
            }
            float f1 = metric == RockVoronoiMetric.Euclidean ? sqrt(d1) : d1; float f2 = metric == RockVoronoiMetric.Euclidean ? sqrt(d2) : d2;
            if (!normalize)
            {
                if (type == RockNoiseType.VoronoiF2) return f2; if (type == RockNoiseType.VoronoiF2F1) return f2 - f1; if (type == RockNoiseType.VoronoiF1DivF2) return f1 / max(f2, 1e-5f); return f1;
            }
            float result = type == RockNoiseType.VoronoiF2 ? (f2 / 1.2f) * 2.0f - 1.0f : type == RockNoiseType.VoronoiF2F1 ? ((f2 - f1) / 0.6f) * 2.0f - 1.0f : type == RockNoiseType.VoronoiF1DivF2 ? (f1 / max(f2, 1e-5f)) * 2.0f - 1.0f : f1 * 2.0f - 1.0f;
            return clamp(result, -1.0f, 1.0f);
        }

        [BurstCompile]
        public static void GetVoronoi3D_Deriv(in float3 point, float frequency, int seed, float jitter, out float4 result, RockNoiseType type, bool normalize, RockVoronoiMetric metric)
        {
            float3 p = point * frequency; int3 gridCoords = (int3)floor(p); float3 fracCoords = frac(p);
            float d1_sq = float.MaxValue, d2_sq = float.MaxValue; float3 diff1 = new float3(0f), diff2 = new float3(0f); int3 closestGrid = new int3(0); float j = saturate(jitter);

            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int3 nGrid = gridCoords + new int3(x, y, z);
                        float3 diff = new float3(x, y, z) + lerp(new float3(0.5f), Hash3D_01(in nGrid, seed), j) - fracCoords;
                        float dist = metric == RockVoronoiMetric.Manhattan ? abs(diff.x) + abs(diff.y) + abs(diff.z) : metric == RockVoronoiMetric.Chebyshev ? max(max(abs(diff.x), abs(diff.y)), abs(diff.z)) : dot(diff, diff);
                        if (dist < d1_sq) { d2_sq = d1_sq; diff2 = diff1; d1_sq = dist; diff1 = diff; closestGrid = nGrid; }
                        else if (dist < d2_sq) { d2_sq = dist; diff2 = diff; }
                    }
                }
            }

            if (type == RockNoiseType.VoronoiCellID) { result = new float4(0f, 0f, 0f, Hash1D_01(closestGrid.x + closestGrid.y * 113 + closestGrid.z * 317, seed + 1337) * 2.0f - 1.0f); return; }

            float val = 0f; float3 grad = new float3(0f);
            if (type == RockNoiseType.VoronoiEdge)
            {
                if (metric == RockVoronoiMetric.Euclidean)
                {
                    float3 cellDiff = diff2 - diff1; float lenSq = dot(cellDiff, cellDiff);
                    if (lenSq > 1e-5f) { float invLen = rsqrt(lenSq); val = dot((diff1 + diff2) * 0.5f, cellDiff) * invLen; grad = -cellDiff * invLen; }
                }
                else
                {
                    val = (d2_sq - d1_sq) * 0.5f; float3 grad1 = -select(new float3(-1.0f), new float3(1.0f), diff1 >= 0.0f); float3 grad2 = -select(new float3(-1.0f), new float3(1.0f), diff2 >= 0.0f); grad = (grad2 - grad1) * 0.5f;
                }
                if (normalize) { val = (val / 0.5f) * 2.0f - 1.0f; grad *= (2.0f / 0.5f); }
            }
            else
            {
                float f1 = metric == RockVoronoiMetric.Euclidean ? sqrt(d1_sq) : d1_sq; float f2 = metric == RockVoronoiMetric.Euclidean ? sqrt(d2_sq) : d2_sq; float3 grad1, grad2;
                if (metric == RockVoronoiMetric.Manhattan) { grad1 = -select(new float3(-1.0f), new float3(1.0f), diff1 >= 0.0f); grad2 = -select(new float3(-1.0f), new float3(1.0f), diff2 >= 0.0f); }
                else if (metric == RockVoronoiMetric.Chebyshev)
                {
                    float3 a1 = abs(diff1), a2 = abs(diff2);
                    grad1 = (a1.x >= a1.y && a1.x >= a1.z) ? new float3(-select(-1.0f, 1.0f, diff1.x >= 0f), 0f, 0f) : (a1.y >= a1.x && a1.y >= a1.z) ? new float3(0f, -select(-1.0f, 1.0f, diff1.y >= 0f), 0f) : new float3(0f, 0f, -select(-1.0f, 1.0f, diff1.z >= 0f));
                    grad2 = (a2.x >= a2.y && a2.x >= a2.z) ? new float3(-select(-1.0f, 1.0f, diff2.x >= 0f), 0f, 0f) : (a2.y >= a2.x && a2.y >= a2.z) ? new float3(0f, -select(-1.0f, 1.0f, diff2.y >= 0f), 0f) : new float3(0f, 0f, -select(-1.0f, 1.0f, diff2.z >= 0f));
                }
                else { grad1 = -diff1 / max(f1, 1e-5f); grad2 = -diff2 / max(f2, 1e-5f); }
                if (type == RockNoiseType.VoronoiF1) { val = f1; grad = grad1; if (normalize) { val = val * 2.0f - 1.0f; grad *= 2.0f; } }
                else if (type == RockNoiseType.VoronoiF2) { val = f2; grad = grad2; if (normalize) { val = (val / 1.2f) * 2.0f - 1.0f; grad *= (2.0f / 1.2f); } }
                else if (type == RockNoiseType.VoronoiF2F1) { val = f2 - f1; grad = grad2 - grad1; if (normalize) { val = (val / 0.6f) * 2.0f - 1.0f; grad *= (2.0f / 0.6f); } }
                else if (type == RockNoiseType.VoronoiF1DivF2) { float v_safe = max(f2, 1e-5f); val = f1 / v_safe; grad = (grad1 * v_safe - f1 * grad2) / (v_safe * v_safe); if (normalize) { val = val * 2.0f - 1.0f; grad *= 2.0f; } }
            }
            result = new float4(grad * frequency, clamp(val, -1f, 1f));
        }
        #endregion

        #region Base Evaluators & Modifiers
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateNoise2D(in float2 point, float frequency, int seed, RockNoiseType type, float jitter)
        {
            switch (type)
            {
                case RockNoiseType.Value: return GetValue2D(in point, frequency, seed);
                case RockNoiseType.Perlin: return GetPerlin2D(in point, frequency, seed);
                case RockNoiseType.WhiteNoise: return GetWhiteNoise2D(in point, frequency, seed);
                case RockNoiseType.VoronoiF1: return GetVoronoi2D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiF2: return GetVoronoi2D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiF2F1: return GetVoronoi2D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiF1DivF2: return GetVoronoi2D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiEdge: return GetVoronoi2D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiCellID: return GetVoronoi2D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.Simplex:
                default: return GetSimplex2D(in point, frequency, seed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EvaluateNoise2D_Deriv(in float2 point, float frequency, int seed, RockNoiseType type, float jitter, out float3 result)
        {
            switch (type)
            {
                case RockNoiseType.Value: GetValue2D_Deriv(in point, frequency, seed, out result); break;
                case RockNoiseType.Perlin: GetPerlin2D_Deriv(in point, frequency, seed, out result); break;
                case RockNoiseType.WhiteNoise: GetWhiteNoise2D_Deriv(in point, frequency, seed, out result); break;
                case RockNoiseType.Simplex: GetSimplex2D_Deriv(in point, frequency, seed, out result); break;
                default: GetVoronoi2D_Deriv(in point, frequency, seed, jitter, out result, type, true, RockVoronoiMetric.Euclidean); break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateNoise3D(in float3 point, float frequency, int seed, RockNoiseType type, float jitter)
        {
            switch (type)
            {
                case RockNoiseType.Value: return GetValue3D(in point, frequency, seed);
                case RockNoiseType.Perlin: return GetPerlin3D(in point, frequency, seed);
                case RockNoiseType.WhiteNoise: return GetWhiteNoise3D(in point, frequency, seed);
                case RockNoiseType.VoronoiF1: return GetVoronoi3D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiF2: return GetVoronoi3D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiF2F1: return GetVoronoi3D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiF1DivF2: return GetVoronoi3D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiEdge: return GetVoronoi3D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.VoronoiCellID: return GetVoronoi3D(in point, frequency, seed, jitter, type, true, RockVoronoiMetric.Euclidean);
                case RockNoiseType.Simplex:
                default: return GetSimplex3D(in point, frequency, seed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EvaluateNoise3D_Deriv(in float3 point, float frequency, int seed, RockNoiseType type, float jitter, out float4 result)
        {
            switch (type)
            {
                case RockNoiseType.Value: GetValue3D_Deriv(in point, frequency, seed, out result); break;
                case RockNoiseType.Perlin: GetPerlin3D_Deriv(in point, frequency, seed, out result); break;
                case RockNoiseType.WhiteNoise: GetWhiteNoise3D_Deriv(in point, frequency, seed, out result); break;
                case RockNoiseType.Simplex: GetSimplex3D_Deriv(in point, frequency, seed, out result); break;
                default: GetVoronoi3D_Deriv(in point, frequency, seed, jitter, out result, type, true, RockVoronoiMetric.Euclidean); break;
            }
        }
        #endregion

        #region FBM Architectures 2D/3D
        [BurstCompile]
        public static float GetFBM_2D(in float2 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                float noiseValue = EvaluateNoise2D(in point, freq, octaveSeed, noiseType, jitter);
                noiseSum += ApplyFractal(noiseValue, config.FractalType) * amplitude;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }

        [BurstCompile]
        public static void GetFBM_2D_Deriv(in float2 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter, out float3 result)
        {
            float3 sum = new float3(0f, 0f, 0f);
            float amplitude = 1f, freq = frequency, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                EvaluateNoise2D_Deriv(in point, freq, octaveSeed, noiseType, jitter, out float3 d);
                d = ApplyFractalDeriv(d, config.FractalType);
                sum += d * amplitude;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            result = sum * normFactor;
        }

        [BurstCompile]
        public static float GetFBM_3D(in float3 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                float noiseValue = EvaluateNoise3D(in point, freq, octaveSeed, noiseType, jitter);
                noiseSum += ApplyFractal(noiseValue, config.FractalType) * amplitude;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }

        [BurstCompile]
        public static void GetFBM_3D_Deriv(in float3 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter, out float4 result)
        {
            float4 sum = new float4(0f, 0f, 0f, 0f);
            float amplitude = 1f, freq = frequency, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                EvaluateNoise3D_Deriv(in point, freq, octaveSeed, noiseType, jitter, out float4 d);
                d = ApplyFractalDeriv(d, config.FractalType);
                sum += d * amplitude;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            result = sum * normFactor;
        }

        // Delegate overload for Generator compatibility
        [BurstCompile]
        public static float GetFBM_3D(in float3 point, float frequency, int seed, in RockFBMConfig config, FunctionPointer<RockNoise3DFunction> noiseFunc)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                float noiseValue = noiseFunc.Invoke(in point, freq, seed + i * 79);
                noiseSum += ApplyFractal(noiseValue, config.FractalType) * amplitude;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }

        [BurstCompile]
        public static float GetSwissFBM_2D(in float2 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, weight = 1f, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                EvaluateNoise2D_Deriv(in point, freq, octaveSeed, noiseType, jitter, out float3 d);
                float ridge = 1.0f - abs(d.z);
                noiseSum += ridge * amplitude * weight;
                weight = saturate(ridge * length(d.xy) * config.ErosionStrength);
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }

        [BurstCompile]
        public static float GetSwissFBM_3D(in float3 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, weight = 1f, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                EvaluateNoise3D_Deriv(in point, freq, octaveSeed, noiseType, jitter, out float4 d);
                float ridge = 1.0f - abs(d.w);
                noiseSum += ridge * amplitude * weight;
                weight = saturate(ridge * length(d.xyz) * config.ErosionStrength);
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }

        [BurstCompile]
        public static float GetFlowFBM_2D(in float2 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter, in float2 warpMask)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, normFactor = CalculateFBMNormalization(in config);
            float2 p = point;
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                EvaluateNoise2D_Deriv(in p, freq, octaveSeed, noiseType, jitter, out float3 d);
                noiseSum += ApplyFractal(d.z, config.FractalType) * amplitude;
                p += d.xy * config.FlowWarpStrength * warpMask;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }

        [BurstCompile]
        public static float GetFlowFBM_3D(in float3 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter, in float3 warpMask)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, normFactor = CalculateFBMNormalization(in config);
            float3 p = point;
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                EvaluateNoise3D_Deriv(in p, freq, octaveSeed, noiseType, jitter, out float4 d);
                noiseSum += ApplyFractal(d.w, config.FractalType) * amplitude;
                p += d.xyz * config.FlowWarpStrength * warpMask;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }

        [BurstCompile]
        public static float GetHybridFBM_2D(in float2 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, weight = 1f, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                float noiseValue = ApplyFractal(EvaluateNoise2D(in point, freq, octaveSeed, noiseType, jitter), config.FractalType);
                float signal = (noiseValue + config.HybridOffset) * amplitude;
                float wClamped = saturate(weight);
                noiseSum += wClamped * signal;
                weight = wClamped * signal;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }

        [BurstCompile]
        public static float GetHybridFBM_3D(in float3 point, float frequency, int seed, in RockFBMConfig config, RockNoiseType noiseType, float jitter)
        {
            float noiseSum = 0f, amplitude = 1f, freq = frequency, weight = 1f, normFactor = CalculateFBMNormalization(in config);
            for (int i = 0; i < clamp(config.Octaves, 1, 16); i++)
            {
                int octaveSeed = seed + i * 79;
                float noiseValue = ApplyFractal(EvaluateNoise3D(in point, freq, octaveSeed, noiseType, jitter), config.FractalType);
                float signal = (noiseValue + config.HybridOffset) * amplitude;
                float wClamped = saturate(weight);
                noiseSum += wClamped * signal;
                weight = wClamped * signal;
                amplitude *= config.Persistence; freq *= config.Lacunarity;
            }
            return noiseSum * normFactor;
        }
        #endregion

        #region Domain Warping 2D/3D
        [BurstCompile]
        public static float GetDomainWarped2DNoise(
             in float2 point, float warpStrength,
             float baseFrequency, int baseSeed, in RockFBMConfig baseFBM, RockNoiseType baseType, float baseJitter,
             float warpFrequency, int warpSeed, in RockFBMConfig warpFBM, RockNoiseType warpType, float warpJitter)
        {
            float2 warpVector = new float2(
                GetFBM_2D(in point, warpFrequency, warpSeed, in warpFBM, warpType, warpJitter),
                GetFBM_2D(in point, warpFrequency, warpSeed + 193, in warpFBM, warpType, warpJitter)
            ) * warpStrength;
            return GetFBM_2D(point + warpVector, baseFrequency, baseSeed, in baseFBM, baseType, baseJitter);
        }

        [BurstCompile]
        public static void GetDomainWarped2D_Deriv(
            in float2 point, float warpStrength,
            float baseFrequency, int baseSeed, in RockFBMConfig baseFBM, RockNoiseType baseType, float baseJitter,
            float warpFrequency, int warpSeed, in RockFBMConfig warpFBM, RockNoiseType warpType, float warpJitter, out float3 result)
        {
            GetFBM_2D_Deriv(in point, warpFrequency, warpSeed, in warpFBM, warpType, warpJitter, out float3 warpX);
            GetFBM_2D_Deriv(in point, warpFrequency, warpSeed + 193, in warpFBM, warpType, warpJitter, out float3 warpY);

            float2x2 J_warp = new float2x2(warpStrength * warpX.x, warpStrength * warpY.x, warpStrength * warpX.y, warpStrength * warpY.y);
            float2 warpedCoordinates = point + new float2(warpX.z, warpY.z) * warpStrength;

            GetFBM_2D_Deriv(in warpedCoordinates, baseFrequency, baseSeed, in baseFBM, baseType, baseJitter, out float3 baseNoise);
            float2x2 J_total = new float2x2(1f + J_warp.c0.x, J_warp.c1.x, J_warp.c0.y, 1f + J_warp.c1.y);
            result = new float3(mul(baseNoise.xy, J_total), baseNoise.z);
        }

        // Delegate overload for Generator compatibility
        [BurstCompile]
        public static float GetDomainWarped3DNoise(
            in float3 point, float warpStrength, float baseFrequency, int baseSeed, in RockFBMConfig baseFBM, FunctionPointer<RockNoise3DFunction> baseNoiseFunc,
            float warpFrequency, int warpSeed, in RockFBMConfig warpFBM, FunctionPointer<RockNoise3DFunction> warpNoiseFunc)
        {
            float3 warpVector = new float3(
                GetFBM_3D(in point, warpFrequency, warpSeed, in warpFBM, warpNoiseFunc),
                GetFBM_3D(in point, warpFrequency, warpSeed + 193, in warpFBM, warpNoiseFunc),
                GetFBM_3D(in point, warpFrequency, warpSeed + 317, in warpFBM, warpNoiseFunc)
            ) * warpStrength;

            float3 warpedCoordinates = point + warpVector;
            return GetFBM_3D(in warpedCoordinates, baseFrequency, baseSeed, in baseFBM, baseNoiseFunc);
        }

        [BurstCompile]
        public static float GetDomainWarped3DNoise(
             in float3 point, float warpStrength,
             float baseFrequency, int baseSeed, in RockFBMConfig baseFBM, RockNoiseType baseType, float baseJitter,
             float warpFrequency, int warpSeed, in RockFBMConfig warpFBM, RockNoiseType warpType, float warpJitter)
        {
            float3 warpVector = new float3(
                GetFBM_3D(in point, warpFrequency, warpSeed, in warpFBM, warpType, warpJitter),
                GetFBM_3D(in point, warpFrequency, warpSeed + 193, in warpFBM, warpType, warpJitter),
                GetFBM_3D(in point, warpFrequency, warpSeed + 317, in warpFBM, warpType, warpJitter)
            ) * warpStrength;
            return GetFBM_3D(point + warpVector, baseFrequency, baseSeed, in baseFBM, baseType, baseJitter);
        }

        [BurstCompile]
        public static void GetDomainWarped3D_Deriv(
            in float3 point, float warpStrength,
            float baseFrequency, int baseSeed, in RockFBMConfig baseFBM, RockNoiseType baseType, float baseJitter,
            float warpFrequency, int warpSeed, in RockFBMConfig warpFBM, RockNoiseType warpType, float warpJitter, out float4 result)
        {
            GetFBM_3D_Deriv(in point, warpFrequency, warpSeed, in warpFBM, warpType, warpJitter, out float4 warpX);
            GetFBM_3D_Deriv(in point, warpFrequency, warpSeed + 193, in warpFBM, warpType, warpJitter, out float4 warpY);
            GetFBM_3D_Deriv(in point, warpFrequency, warpSeed + 317, in warpFBM, warpType, warpJitter, out float4 warpZ);

            float3x3 J_warp = new float3x3(
                warpStrength * warpX.x, warpStrength * warpX.y, warpStrength * warpX.z,
                warpStrength * warpY.x, warpStrength * warpY.y, warpStrength * warpY.z,
                warpStrength * warpZ.x, warpStrength * warpZ.y, warpStrength * warpZ.z
            );

            float3 warpedCoords = point + new float3(warpX.w, warpY.w, warpZ.w) * warpStrength;
            GetFBM_3D_Deriv(in warpedCoords, baseFrequency, baseSeed, in baseFBM, baseType, baseJitter, out float4 baseNoise);

            float3x3 J_total = new float3x3(
                1f + J_warp.c0.x, J_warp.c1.x, J_warp.c2.x,
                J_warp.c0.y, 1f + J_warp.c1.y, J_warp.c2.y,
                J_warp.c0.z, J_warp.c1.z, 1f + J_warp.c2.z
            );

            result = new float4(mul(baseNoise.xyz, J_total), baseNoise.w);
        }
        #endregion

        #region Missing Fractal & FBM Utilities
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateFBMNormalization(in RockFBMConfig config)
        {
            float maxAmplitude = 0.0f, amplitude = 1.0f;
            int octaves = clamp(config.Octaves, 1, 16);
            for (int i = 0; i < octaves; i++) { maxAmplitude += amplitude; amplitude *= config.Persistence; }
            return 1.0f / max(maxAmplitude, 1e-5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyFractal(float val, RockFractalType type)
        {
            if (type == RockFractalType.Billow) return abs(val);
            if (type == RockFractalType.Ridged) { float r = 1.0f - abs(val); return r * r; }
            if (type == RockFractalType.PingPong) return sin(val * PI);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ApplyFractalDeriv(float2 d, RockFractalType type)
        {
            if (type == RockFractalType.Standard || type == RockFractalType.Flow || type == RockFractalType.SwissErosion || type == RockFractalType.Hybrid) return d;
            if (type == RockFractalType.PingPong)
            {
                sincos(d.y * PI, out float s_val, out float c_val);
                return new float2(d.x * PI * c_val, s_val);
            }
            float s = select(-1.0f, 1.0f, d.y >= 0.0f);
            if (type == RockFractalType.Billow) return new float2(d.x * s, abs(d.y));
            float r = 1.0f - abs(d.y); return new float2(d.x * -2.0f * r * s, r * r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ApplyFractalDeriv(float3 d, RockFractalType type)
        {
            if (type == RockFractalType.Standard || type == RockFractalType.Flow || type == RockFractalType.SwissErosion || type == RockFractalType.Hybrid) return d;
            if (type == RockFractalType.PingPong)
            {
                sincos(d.z * PI, out float s_val, out float c_val);
                return new float3(d.xy * PI * c_val, s_val);
            }
            float s = select(-1.0f, 1.0f, d.z >= 0.0f);
            if (type == RockFractalType.Billow) return new float3(d.xy * s, abs(d.z));
            float r = 1.0f - abs(d.z); return new float3(d.xy * -2.0f * r * s, r * r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 ApplyFractalDeriv(float4 d, RockFractalType type)
        {
            if (type == RockFractalType.Standard || type == RockFractalType.Flow || type == RockFractalType.SwissErosion || type == RockFractalType.Hybrid) return d;
            if (type == RockFractalType.PingPong)
            {
                sincos(d.w * PI, out float s_val, out float c_val);
                return new float4(d.xyz * PI * c_val, s_val);
            }
            float s = select(-1.0f, 1.0f, d.w >= 0.0f);
            if (type == RockFractalType.Billow) return new float4(d.xyz * s, abs(d.w));
            float r = 1.0f - abs(d.w); return new float4(d.xyz * -2.0f * r * s, r * r);
        }
        #endregion

        #region Missing Voronoi 2D Deriv
        [BurstCompile]
        public static void GetVoronoi2D_Deriv(in float2 point, float frequency, int seed, float jitter, out float3 result, RockNoiseType type, bool normalize, RockVoronoiMetric metric)
        {
            float2 p = point * frequency;
            int2 gridCoords = new int2((int)floor(p.x), (int)floor(p.y));
            float2 fracCoords = frac(p);
            float j = saturate(jitter);

            float d1 = float.MaxValue, d2 = float.MaxValue;
            float2 diff1 = new float2(0f), diff2 = new float2(0f);
            int2 closestGrid = new int2(0);

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    int2 nGrid = gridCoords + new int2(x, y);
                    float2 diff = new float2(x, y) + lerp(new float2(0.5f), Hash2D_01(nGrid, seed), j) - fracCoords;

                    float dist = metric == RockVoronoiMetric.Manhattan ? abs(diff.x) + abs(diff.y) :
                                 metric == RockVoronoiMetric.Chebyshev ? max(abs(diff.x), abs(diff.y)) : dot(diff, diff);

                    if (dist < d1)
                    {
                        d2 = d1; diff2 = diff1;
                        d1 = dist; diff1 = diff;
                        closestGrid = nGrid;
                    }
                    else if (dist < d2) { d2 = dist; diff2 = diff; }
                }
            }

            if (type == RockNoiseType.VoronoiCellID) { result = new float3(0f, 0f, Hash1D_01(closestGrid.x + closestGrid.y * 113, seed + 1337) * 2.0f - 1.0f); return; }

            float valBase = 0f; float2 gradBase = new float2(0f);

            if (type == RockNoiseType.VoronoiEdge)
            {
                if (metric == RockVoronoiMetric.Euclidean)
                {
                    float2 cellDiff = diff2 - diff1;
                    float lenSq = dot(cellDiff, cellDiff);
                    if (lenSq > 1e-5f)
                    {
                        float invLen = rsqrt(lenSq);
                        valBase = dot((diff1 + diff2) * 0.5f, cellDiff) * invLen;
                        gradBase = -cellDiff * invLen;
                    }
                }
                else
                {
                    valBase = (d2 - d1) * 0.5f;
                    float2 grad1 = -select(new float2(-1.0f), new float2(1.0f), diff1 >= 0.0f);
                    float2 grad2 = -select(new float2(-1.0f), new float2(1.0f), diff2 >= 0.0f);
                    gradBase = (grad2 - grad1) * 0.5f;
                }
                if (normalize) { valBase = (valBase / 0.5f) * 2.0f - 1.0f; gradBase *= (2.0f / 0.5f); }
            }
            else
            {
                float f1 = metric == RockVoronoiMetric.Euclidean ? sqrt(d1) : d1;
                float f2 = metric == RockVoronoiMetric.Euclidean ? sqrt(d2) : d2;
                float2 grad1, grad2;

                if (metric == RockVoronoiMetric.Manhattan)
                {
                    grad1 = -select(new float2(-1.0f), new float2(1.0f), diff1 >= 0.0f);
                    grad2 = -select(new float2(-1.0f), new float2(1.0f), diff2 >= 0.0f);
                }
                else if (metric == RockVoronoiMetric.Chebyshev)
                {
                    float2 a1 = abs(diff1), a2 = abs(diff2);
                    grad1 = a1.x > a1.y ? new float2(-select(-1.0f, 1.0f, diff1.x >= 0f), 0f) : new float2(0f, -select(-1.0f, 1.0f, diff1.y >= 0f));
                    grad2 = a2.x > a2.y ? new float2(-select(-1.0f, 1.0f, diff2.x >= 0f), 0f) : new float2(0f, -select(-1.0f, 1.0f, diff2.y >= 0f));
                }
                else
                {
                    grad1 = -diff1 / max(f1, 1e-5f);
                    grad2 = -diff2 / max(f2, 1e-5f);
                }

                if (type == RockNoiseType.VoronoiF1) { valBase = f1; gradBase = grad1; if (normalize) { valBase = valBase * 2.0f - 1.0f; gradBase *= 2.0f; } }
                else if (type == RockNoiseType.VoronoiF2) { valBase = f2; gradBase = grad2; if (normalize) { valBase = (valBase / 1.2f) * 2.0f - 1.0f; gradBase *= (2.0f / 1.2f); } }
                else if (type == RockNoiseType.VoronoiF2F1) { valBase = f2 - f1; gradBase = grad2 - grad1; if (normalize) { valBase = (valBase / 0.6f) * 2.0f - 1.0f; gradBase *= (2.0f / 0.6f); } }
                else if (type == RockNoiseType.VoronoiF1DivF2)
                {
                    float v_safe = max(f2, 1e-5f);
                    valBase = f1 / v_safe;
                    gradBase = (grad1 * v_safe - f1 * grad2) / (v_safe * v_safe);
                    if (normalize) { valBase = valBase * 2.0f - 1.0f; gradBase *= 2.0f; }
                }
            }

            result = new float3(gradBase * frequency, clamp(valBase, -1f, 1f));
        }
        #endregion
    }
}