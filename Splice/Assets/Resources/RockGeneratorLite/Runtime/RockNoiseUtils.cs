using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Veridian.RockGenLite.Noise
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    public static class RockNoiseUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateBlend(float a, float b, RockBlendMode mode, float blendSmoothness = 0.0f)
        {
            switch (mode)
            {
                case RockBlendMode.Overwrite: return b;
                case RockBlendMode.Add: return a + b;
                case RockBlendMode.Subtract: return a - b;
                case RockBlendMode.Multiply: return a * b;
                case RockBlendMode.Divide: return b != 0.0f ? a / b : a;
                case RockBlendMode.Min: return min(a, b);
                case RockBlendMode.Max: return max(a, b);
                case RockBlendMode.Overlay: return a < 0.5f ? 2.0f * a * b : 1.0f - 2.0f * (1.0f - a) * (1.0f - b);
                case RockBlendMode.SmoothMin:
                    {
                        float k = max(blendSmoothness, 1e-5f);
                        float h = saturate(0.5f + 0.5f * (b - a) / k);
                        return lerp(b, a, h) - k * h * (1.0f - h);
                    }
                case RockBlendMode.SmoothMax:
                    {
                        float k = max(blendSmoothness, 1e-5f);
                        float h = saturate(0.5f + 0.5f * (a - b) / k);
                        return lerp(b, a, h) + k * h * (1.0f - h);
                    }
                default: return a;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateSDFBoolean(float a, float b, RockSDFBooleanMode mode, float smoothness = 0.0f)
        {
            switch (mode)
            {
                case RockSDFBooleanMode.Union: return min(a, b);
                case RockSDFBooleanMode.Subtraction: return max(a, -b);
                case RockSDFBooleanMode.Intersection: return max(a, b);
                case RockSDFBooleanMode.SmoothUnion:
                    {
                        float k = max(smoothness, 1e-5f);
                        float h = saturate(0.5f + 0.5f * (b - a) / k);
                        return lerp(b, a, h) - k * h * (1.0f - h);
                    }
                case RockSDFBooleanMode.SmoothIntersection:
                    {
                        float k = max(smoothness, 1e-5f);
                        float h = saturate(0.5f - 0.5f * (b - a) / k);
                        return lerp(b, a, h) + k * h * (1.0f - h);
                    }
                case RockSDFBooleanMode.SmoothSubtraction:
                    {
                        float k = max(smoothness, 1e-5f);
                        float h = saturate(0.5f - 0.5f * (-b - a) / k);
                        return lerp(-b, a, h) + k * h * (1.0f - h);
                    }
                default: return a;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Remap(float value, float oldMin, float oldMax, float newMin, float newMax)
        {
            float t = saturate((value - oldMin) / max(oldMax - oldMin, 1e-5f));
            return lerp(newMin, newMax, t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyBias(float value, float bias)
        {
            float t = saturate(value);
            float b = clamp(bias, 0.0001f, 0.9999f);
            float k = (1.0f / b) - 2.0f;
            return t / (k * (1.0f - t) + 1.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyGain(float value, float gain)
        {
            float st = saturate(value);
            float g = clamp(gain, 0.0001f, 0.9999f);
            float k = (1.0f / g) - 2.0f;
            return (st < 0.5f) ? st / (k * (1.0f - 2.0f * st) + 1.0f) : (k * (1.0f - 2.0f * st) - st) / (k * (1.0f - 2.0f * st) - 1.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 BlendNormalsRNM(in float3 n1, in float3 n2, bool isYUp = false)
        {
            if (isYUp)
            {
                float3 t = n1 + new float3(0.0f, 1.0f, 0.0f);
                float3 u = n2 * new float3(-1.0f, 1.0f, -1.0f);
                return normalize(t * dot(t, u) / max(t.y, 1e-5f) - u);
            }
            else
            {
                float3 t = n1 + new float3(0.0f, 0.0f, 1.0f);
                float3 u = n2 * new float3(-1.0f, -1.0f, 1.0f);
                return normalize(t * dot(t, u) / max(t.z, 1e-5f) - u);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateCurveLUT(float value, in NativeArray<float> lut, out float slope)
        {
            int maxIndex = lut.Length - 1;
            if (maxIndex <= 0)
            {
                slope = 0f;
                return maxIndex == 0 ? lut[0] : 0f;
            }
            float t = saturate(value);
            float continuousIndex = t * maxIndex;
            int i0 = (int)floor(continuousIndex);
            if (i0 >= maxIndex) { i0 = maxIndex - 1; continuousIndex = maxIndex; }
            int i1 = i0 + 1;
            float frac = continuousIndex - i0;
            float y0 = lut[i0];
            float y1 = lut[i1];
            slope = (y1 - y0) * maxIndex;
            return lerp(y0, y1, frac);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LinearToSRGB(float c)
        {
            float v = saturate(c);
            return v <= 0.0031308f ? v * 12.92f : 1.055f * pow(v, 1.0f / 2.4f) - 0.055f;
        }

        private static readonly float[] BayerMatrix8x8 = new float[64] {
             0f/64f, 32f/64f,  8f/64f, 40f/64f,  2f/64f, 34f/64f, 10f/64f, 42f/64f,
            48f/64f, 16f/64f, 56f/64f, 24f/64f, 50f/64f, 18f/64f, 58f/64f, 26f/64f,
            12f/64f, 44f/64f,  4f/64f, 36f/64f, 14f/64f, 46f/64f,  6f/64f, 38f/64f,
            60f/64f, 28f/64f, 52f/64f, 20f/64f, 62f/64f, 30f/64f, 54f/64f, 22f/64f,
             3f/64f, 35f/64f, 11f/64f, 43f/64f,  1f/64f, 33f/64f,  9f/64f, 41f/64f,
            51f/64f, 19f/64f, 59f/64f, 27f/64f, 49f/64f, 17f/64f, 57f/64f, 25f/64f,
            15f/64f, 47f/64f,  7f/64f, 39f/64f, 13f/64f, 45f/64f,  5f/64f, 37f/64f,
            63f/64f, 31f/64f, 55f/64f, 23f/64f, 61f/64f, 29f/64f, 53f/64f, 21f/64f
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyBayerDither(float value, int2 pixelCoord, float spread = 1.0f / 255.0f)
        {
            int bX = pixelCoord.x & 7; int bY = pixelCoord.y & 7;
            float bayerOffset = BayerMatrix8x8[bY * 8 + bX] - 0.5f;
            return saturate(value + bayerOffset * spread);
        }
    }
}