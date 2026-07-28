using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Veridian.RockGenLite.Noise;
using static Unity.Mathematics.math;

namespace Veridian.RockGenLite.Runtime.Jobs
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct IcosphereJob : IJob
    {
        [ReadOnly] public int RecursionLevel;

        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> Vertices;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int> Triangles;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float2> UVs;

        public void Execute()
        {
            int subdivisions = math.clamp(RecursionLevel, 0, 6);
            int segments = (int)math.pow(2, subdivisions);

            float t = (1.0f + math.sqrt(5.0f)) / 2.0f;
            NativeArray<float3> baseVerts = new NativeArray<float3>(12, Allocator.Temp)
            {
                [0] = math.normalize(new float3(-1, t, 0)),
                [1] = math.normalize(new float3(1, t, 0)),
                [2] = math.normalize(new float3(-1, -t, 0)),
                [3] = math.normalize(new float3(1, -t, 0)),
                [4] = math.normalize(new float3(0, -1, t)),
                [5] = math.normalize(new float3(0, 1, t)),
                [6] = math.normalize(new float3(0, -1, -t)),
                [7] = math.normalize(new float3(0, 1, -t)),
                [8] = math.normalize(new float3(t, 0, -1)),
                [9] = math.normalize(new float3(t, 0, 1)),
                [10] = math.normalize(new float3(-t, 0, -1)),
                [11] = math.normalize(new float3(-t, 0, 1))
            };

            NativeArray<int> baseFaces = new NativeArray<int>(60, Allocator.Temp)
            {
                [0] = 0,
                [1] = 11,
                [2] = 5,
                [3] = 0,
                [4] = 5,
                [5] = 1,
                [6] = 0,
                [7] = 1,
                [8] = 7,
                [9] = 0,
                [10] = 7,
                [11] = 10,
                [12] = 0,
                [13] = 10,
                [14] = 11,
                [15] = 1,
                [16] = 5,
                [17] = 9,
                [18] = 5,
                [19] = 11,
                [20] = 4,
                [21] = 11,
                [22] = 10,
                [23] = 2,
                [24] = 10,
                [25] = 7,
                [26] = 6,
                [27] = 7,
                [28] = 1,
                [29] = 8,
                [30] = 3,
                [31] = 9,
                [32] = 4,
                [33] = 3,
                [34] = 4,
                [35] = 2,
                [36] = 3,
                [37] = 2,
                [38] = 6,
                [39] = 3,
                [40] = 6,
                [41] = 8,
                [42] = 3,
                [43] = 8,
                [44] = 9,
                [45] = 4,
                [46] = 9,
                [47] = 5,
                [48] = 2,
                [49] = 4,
                [50] = 11,
                [51] = 6,
                [52] = 2,
                [53] = 10,
                [54] = 8,
                [55] = 6,
                [56] = 7,
                [57] = 9,
                [58] = 8,
                [59] = 1
            };

            float uvWidth = 1.0f / 5.0f;
            float uvHeight = 1.0f / 4.0f;
            float padding = 0.015f;

            int vIndex = 0;
            int tIndex = 0;

            for (int faceIdx = 0; faceIdx < 20; faceIdx++)
            {
                float3 vA = baseVerts[baseFaces[faceIdx * 3]];
                float3 vB = baseVerts[baseFaces[faceIdx * 3 + 1]];
                float3 vC = baseVerts[baseFaces[faceIdx * 3 + 2]];

                int startVertIndex = vIndex;

                float uCol = faceIdx % 5;
                float vRow = faceIdx / 5;

                float uMin = uCol * uvWidth + padding;
                float uMax = (uCol + 1) * uvWidth - padding;
                float vMin = vRow * uvHeight + padding;
                float vMax = (vRow + 1) * uvHeight - padding;

                float2 uvA = new float2(uMin, vMin);
                float2 uvB = new float2(uMax, vMin);
                float2 uvC = new float2(math.lerp(uMin, uMax, 0.5f), vMax);

                for (int row = 0; row <= segments; row++)
                {
                    for (int col = 0; col <= segments - row; col++)
                    {
                        float wB = (float)col / segments;
                        float wC = (float)row / segments;
                        float wA = 1.0f - wB - wC;

                        float3 rawPos = math.normalize(vA * wA + vB * wB + vC * wC);

                        // THE FIX: Output raw calculated position. No artificial rounding!
                        Vertices[vIndex] = rawPos;

                        UVs[vIndex] = uvA * wA + uvB * wB + uvC * wC;
                        vIndex++;
                    }
                }

                int currentVert = startVertIndex;
                for (int row = 0; row < segments; row++)
                {
                    for (int col = 0; col < segments - row; col++)
                    {
                        int i0 = currentVert;
                        int i1 = currentVert + 1;
                        int i2 = currentVert + (segments - row + 1);

                        Triangles[tIndex++] = i0; Triangles[tIndex++] = i1; Triangles[tIndex++] = i2;

                        if (col < segments - row - 1)
                        {
                            int i3 = currentVert + (segments - row + 2);
                            Triangles[tIndex++] = i1; Triangles[tIndex++] = i3; Triangles[tIndex++] = i2;
                        }
                        currentVert++;
                    }
                    currentVert++;
                }
            }
            baseVerts.Dispose(); baseFaces.Dispose();
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct RockDisplacementJob : IJobParallelFor
    {
        [ReadOnly] public RockShapeData Settings;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> Vertices;

        public void Execute(int index)
        {
            float3 unitVertex = Vertices[index];
            float3 baseShapeScale = Settings.BaseShapeScale;

            float3 currentPos = unitVertex * baseShapeScale;
            float3 currentNormal = unitVertex / baseShapeScale;
            currentNormal = math.normalizesafe(currentNormal, unitVertex);

            // Track the distance to the origin to prevent the mesh from turning inside-out
            float currentDist = math.length(currentPos);

            if (Settings.UseMacroNoise && Settings.MacroNoiseStrength > 0.001f)
            {
                float macroDisplacement;

                if (Settings.MacroFBMConfig.FractalType == RockFractalType.SwissErosion)
                {
                    macroDisplacement = RockNoiseCore.GetSwissFBM_3D(in currentPos, Settings.MacroNoiseFrequency, Settings.Seed + 55, in Settings.MacroFBMConfig, RockNoiseType.Simplex, 1.0f);
                }
                else
                {
                    macroDisplacement = RockNoiseCore.GetFBM_3D(in currentPos, Settings.MacroNoiseFrequency, Settings.Seed + 55, in Settings.MacroFBMConfig, RockNoiseType.Simplex, 1.0f);
                }

                float actualMacro = macroDisplacement * Settings.MacroNoiseStrength;

                // SAFETY CLAMP: Prevent displacement from pushing the vertex past the rock's origin
                actualMacro = math.max(actualMacro, -(currentDist * 0.95f));

                currentPos += currentNormal * actualMacro;
                currentNormal = math.normalizesafe(currentPos);
                currentDist = math.length(currentPos); // Update distance for the next noise layer
            }

            if (Settings.NoiseStrength > 0.001f)
            {
                float detailDisplacement = EvaluateDetailNoise(in currentPos);
                float actualDetail = detailDisplacement * Settings.NoiseStrength;

                // SAFETY CLAMP: Prevent extreme Voronoi or detail noise from inverting the mesh
                actualDetail = math.max(actualDetail, -(currentDist * 0.95f));

                currentPos += currentNormal * actualDetail;
            }

            currentPos *= Settings.OverallScale;
            Vertices[index] = currentPos;
        }

        private float EvaluateDetailNoise(in float3 point)
        {
            float noiseValue = 0.0f;
            float3 evalPoint = point;

            // 1. Calculate Domain Warping offset manually so it stacks with ANY fractal type
            if (Settings.UseDomainWarping && Settings.WarpStrength > 0.001f)
            {
                float3 warpVector = new float3(
                    RockNoiseCore.GetFBM_3D(in point, Settings.WarpFrequency, Settings.Seed, in Settings.WarpFBMConfig, RockNoiseType.Flow, 1.0f),
                    RockNoiseCore.GetFBM_3D(in point, Settings.WarpFrequency, Settings.Seed + 193, in Settings.WarpFBMConfig, RockNoiseType.Flow, 1.0f),
                    RockNoiseCore.GetFBM_3D(in point, Settings.WarpFrequency, Settings.Seed + 317, in Settings.WarpFBMConfig, RockNoiseType.Flow, 1.0f)
                ) * Settings.WarpStrength;

                evalPoint += warpVector;
            }

            // 2. Evaluate Base Fractal Noise
            if (Settings.BaseFBMConfig.FractalType == RockFractalType.SwissErosion)
            {
                noiseValue = RockNoiseCore.GetSwissFBM_3D(in evalPoint, Settings.NoiseFrequency, Settings.Seed, in Settings.BaseFBMConfig, RockNoiseType.Simplex, 1.0f);
            }
            else
            {
                // Standard, Ridged, Billow, and PingPong naturally pass through GetFBM_3D
                noiseValue = RockNoiseCore.GetFBM_3D(in evalPoint, Settings.NoiseFrequency, Settings.Seed, in Settings.BaseFBMConfig, RockNoiseType.Simplex, 1.0f);
            }

            // 3. Inject Voronoi Modifier
            if (Settings.UseVoronoi && Settings.VoronoiIntensity > 0.01f)
            {
                float voronoiNoise = RockNoiseCore.GetVoronoi3D(
                    in point, Settings.VoronoiFrequency, Settings.Seed, 1.0f,
                    Settings.VoronoiOutputType, true, Settings.VoronoiMetric);

                // Keeping this negative is crucial! For "Edge" it pushes cracks inwards. For "F2MinusF1" it makes craters.
                voronoiNoise = -voronoiNoise;
                noiseValue = lerp(noiseValue, voronoiNoise, Settings.VoronoiIntensity);
            }

            // 4. Inject Terracing Modifier
            if (Settings.UseTerracing && Settings.TerraceIntensity > 0.01f)
            {
                int steps = max(1, Settings.TerraceCount);
                float value01 = noiseValue * 0.5f + 0.5f;
                float stepped = round(value01 * steps) / steps;
                noiseValue = lerp(value01, stepped, saturate(Settings.TerraceIntensity)) * 2.0f - 1.0f;
            }

            return noiseValue;
        }
    }
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct BuildWeldMapJob : IJob
    {
        // THE FIX: Removed [ReadOnly] attribute. This job must write to the Vertices to snap them perfectly together.
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> Vertices;
        public NativeArray<int> WeldMap;

        public void Execute()
        {
            float invCellSize = 100.0f; // Creates a search grid resolution
            float thresholdSq = 0.0001f * 0.0001f; // Maximum distance to merge (Fuzzy search tolerance)
            int hashSize = Vertices.Length;

            var head = new NativeArray<int>(hashSize, Allocator.Temp);
            var next = new NativeArray<int>(Vertices.Length, Allocator.Temp);

            for (int i = 0; i < hashSize; i++) head[i] = -1;
            for (int i = 0; i < Vertices.Length; i++) next[i] = -1;

            for (int i = 0; i < Vertices.Length; i++)
            {
                float3 pos = Vertices[i];
                int3 cell = new int3(math.floor(pos * invCellSize));
                int matchIndex = -1;

                // Fuzzy Search: Check 3x3x3 neighboring cells for any vertex that is microscopically close
                for (int x = -1; x <= 1 && matchIndex == -1; x++)
                {
                    for (int y = -1; y <= 1 && matchIndex == -1; y++)
                    {
                        for (int z = -1; z <= 1 && matchIndex == -1; z++)
                        {
                            int3 checkCell = cell + new int3(x, y, z);
                            uint h = math.hash(checkCell);
                            int bucket = (int)(h % (uint)hashSize);

                            int curr = head[bucket];
                            while (curr != -1)
                            {
                                if (math.distancesq(pos, Vertices[curr]) < thresholdSq)
                                {
                                    matchIndex = curr;
                                    break; // Found our microscopic match
                                }
                                curr = next[curr];
                            }
                        }
                    }
                }

                if (matchIndex != -1)
                {
                    WeldMap[i] = matchIndex;
                    // THE VITAL FIX: Snap perfectly! Guarantee identical math in the noise displacement job.
                    // This stops the mesh from ripping apart as the noise evaluates the seams.
                    Vertices[i] = Vertices[matchIndex];
                }
                else
                {
                    WeldMap[i] = i;

                    uint h = math.hash(cell);
                    int bucket = (int)(h % (uint)hashSize);
                    next[i] = head[bucket];
                    head[bucket] = i;
                }
            }

            head.Dispose();
            next.Dispose();
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct NormalCalculationJob : IJob
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<float3> Vertices;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<int> Triangles;

        [ReadOnly] public NativeArray<int> WeldMap;

        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> Normals;

        public void Execute()
        {
            var accumulated = new NativeArray<float3>(Vertices.Length, Allocator.Temp);

            for (int i = 0; i < accumulated.Length; i++) accumulated[i] = new float3(0f, 0f, 0f);

            for (int i = 0; i < Triangles.Length; i += 3)
            {
                int i1 = Triangles[i];
                int i2 = Triangles[i + 1];
                int i3 = Triangles[i + 2];

                // Calculate the raw cross product (whose magnitude equals the triangle's surface area)
                float3 rawFaceNormal = math.cross(Vertices[i2] - Vertices[i1], Vertices[i3] - Vertices[i1]);

                // PHASE 1 FIX: NORMALIZE the face normal before accumulation. 
                // This strips the area bias, giving huge center faces and tiny corner faces an equal weight of 1.0.
                float3 faceNormal = math.normalizesafe(rawFaceNormal);

                accumulated[WeldMap[i1]] += faceNormal;
                accumulated[WeldMap[i2]] += faceNormal;
                accumulated[WeldMap[i3]] += faceNormal;
            }

            for (int i = 0; i < Vertices.Length; i++)
            {
                Normals[i] = math.normalizesafe(accumulated[WeldMap[i]], new float3(0, 1, 0));
            }

            accumulated.Dispose();
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct PerturbNormalsJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<float3> Vertices;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> Normals;

        public float NormalNoiseFrequency;
        public float NormalNoiseStrength;
        public int Seed;

        public void Execute(int index)
        {
            float3 position = Vertices[index];
            float3 normal = Normals[index];

            RockFBMConfig fbm = RockFBMConfig.Default();
            fbm.Octaves = 3;

            // Evaluates mathematical derivative noise to bump the normal without needing a texture map
            RockNoiseCore.GetFBM_3D_Deriv(in position, NormalNoiseFrequency, Seed + 200, in fbm, RockNoiseType.Simplex, 1.0f, out float4 deriv);

            float3 grad = deriv.xyz;
            grad = grad - normal * dot(grad, normal); // Project gradient to tangent plane

            Normals[index] = normalizesafe(normal - grad * NormalNoiseStrength);
        }
    }
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct UVAndColorJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<float3> Vertices;
        [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<float3> Normals;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float2> UVs;
        [WriteOnly, NativeDisableContainerSafetyRestriction] public NativeArray<Color32> Colors;

        public int ColorizationMethod;
        public float UVScale;
        public float BlendSharpness;
        public int ColorPattern;

        public float4 PrimaryColor;
        public float4 SecondaryColor;
        public float4 TertiaryColor;
        public float4 CavityColor;

        public int SlopeMode;
        public float SlopeThreshold;
        public float SlopeBlend;

        public float TextureNoiseFreq;
        public float TextureNoiseBlend;
        public float CavityStrength;

        public float StrataWarpFreq;
        public float StrataWarpStrength;
        public float PatchFreq;

        public int Seed;

        public void Execute(int index)
        {
            float3 position = Vertices[index];
            float3 normal = Normals[index];

            if (ColorizationMethod == 0)
            {
                float3 weights = math.abs(normal);
                weights = math.pow(math.max(weights, 1e-6f), math.max(1.0f, BlendSharpness));
                float sum = weights.x + weights.y + weights.z;
                weights = sum > 1e-6f ? weights / sum : new float3(1.0f / 3.0f);

                UVs[index] = (position.yz * UVScale) * weights.x + (position.xz * UVScale) * weights.y + (position.xy * UVScale) * weights.z;
            }

            Veridian.RockGenLite.Noise.RockFBMConfig texFBM = Veridian.RockGenLite.Noise.RockFBMConfig.Default();
            texFBM.Octaves = 4;

            Veridian.RockGenLite.Noise.RockNoiseCore.GetFBM_3D_Deriv(in position, TextureNoiseFreq, Seed + 999, in texFBM, Veridian.RockGenLite.Noise.RockNoiseType.Simplex, 1.0f, out float4 detailDeriv);

            float4 baseColor = new float4(0);
            float rawCavity = math.saturate((math.length(detailDeriv.xyz) * 0.15f) * CavityStrength);
            float cavityMask = rawCavity * rawCavity;
            float texNoise = math.saturate(detailDeriv.w * 0.5f + 0.5f);

            if (ColorPattern == 1)
            {
                Veridian.RockGenLite.Noise.RockFBMConfig warpFBM = Veridian.RockGenLite.Noise.RockFBMConfig.Default();
                warpFBM.Octaves = 3;
                Veridian.RockGenLite.Noise.RockNoiseCore.GetFBM_3D_Deriv(in position, StrataWarpFreq, Seed + 412, in warpFBM, Veridian.RockGenLite.Noise.RockNoiseType.Simplex, 1.0f, out float4 warpDeriv);

                float warpedY = position.y + (warpDeriv.w * StrataWarpStrength);

                float strataFrequency = TextureNoiseFreq * 2.0f;
                float wave = math.sin((warpedY * strataFrequency) + (texNoise - 0.5f) * TextureNoiseBlend * 5.0f);
                wave = wave * 0.5f + 0.5f;

                float4 strataColor;
                if (wave < 0.5f)
                {
                    float t = math.smoothstep(0.1f, 0.9f, wave * 2.0f);
                    strataColor = math.lerp(PrimaryColor, SecondaryColor, t);
                }
                else
                {
                    float t = math.smoothstep(0.1f, 0.9f, (wave - 0.5f) * 2.0f);
                    strataColor = math.lerp(SecondaryColor, TertiaryColor, t);
                }

                baseColor = math.lerp(strataColor, CavityColor, cavityMask);
                baseColor = math.lerp(baseColor, baseColor * (texNoise * 0.5f + 0.5f), TextureNoiseBlend * 0.3f);
            }
            else if (ColorPattern == 2)
            {
                float patch1 = Veridian.RockGenLite.Noise.RockNoiseCore.GetSimplex3D(in position, PatchFreq, Seed + 222) * 0.5f + 0.5f;
                float patch2 = Veridian.RockGenLite.Noise.RockNoiseCore.GetSimplex3D(in position, PatchFreq * 1.5f, Seed + 333) * 0.5f + 0.5f;

                float edgeSoftness = math.lerp(0.01f, 0.4f, TextureNoiseBlend);
                float breakup = (texNoise - 0.5f) * TextureNoiseBlend;

                float mask1 = math.smoothstep(0.5f - edgeSoftness, 0.5f + edgeSoftness, patch1 + breakup);
                float mask2 = math.smoothstep(0.6f - edgeSoftness, 0.6f + edgeSoftness, patch2 + breakup);

                baseColor = PrimaryColor;
                baseColor = math.lerp(baseColor, SecondaryColor, mask1);
                baseColor = math.lerp(baseColor, TertiaryColor, mask2 * (1.0f - mask1));
                baseColor = math.lerp(baseColor, CavityColor, cavityMask);
            }
            else
            {
                float slopeMask = 0.0f;

                if (SlopeMode != 0)
                {
                    float slopeDot = math.dot(normal, new float3(0, 1, 0));
                    float slope = (SlopeMode == 2) ? math.abs(slopeDot) : math.max(0.0f, slopeDot);

                    // FIX: Apply curve matching the HLSL shader logic
                    float curvedThreshold = math.pow(SlopeThreshold, 3.0f);
                    float internalThreshold = math.lerp(1.0f, -1.0f, curvedThreshold);

                    float minEdge = internalThreshold - SlopeBlend * 0.5f;
                    float maxEdge = internalThreshold + SlopeBlend * 0.5f;
                    if (math.abs(maxEdge - minEdge) < 0.001f) maxEdge = minEdge + 0.001f;

                    slopeMask = math.saturate(math.smoothstep(minEdge, maxEdge, slope) - (texNoise * TextureNoiseBlend * 0.5f));
                }

                baseColor = math.lerp(PrimaryColor, SecondaryColor, slopeMask);
                baseColor = math.lerp(baseColor, PrimaryColor, texNoise * TextureNoiseBlend);
                baseColor = math.lerp(baseColor, CavityColor, cavityMask);
            }

            Colors[index] = new Color32(
                (byte)(math.saturate(baseColor.x) * 255f),
                (byte)(math.saturate(baseColor.y) * 255f),
                (byte)(math.saturate(baseColor.z) * 255f),
                255);
        }
    }


    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct CubeSphereJob : IJob
    {
        [ReadOnly] public int Resolution;

        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> Vertices;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int> Triangles;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float2> UVs;

        public void Execute()
        {
            int r = math.max(1, Resolution);
            int vertsPerFace = (r + 1) * (r + 1);

            NativeArray<int3> faceNormals = new NativeArray<int3>(6, Allocator.Temp)
            {
                [0] = new int3(0, 0, 1),
                [1] = new int3(0, 0, -1),
                [2] = new int3(1, 0, 0),
                [3] = new int3(-1, 0, 0),
                [4] = new int3(0, 1, 0),
                [5] = new int3(0, -1, 0)
            };

            NativeArray<int3> faceRights = new NativeArray<int3>(6, Allocator.Temp)
            {
                [0] = new int3(1, 0, 0),
                [1] = new int3(-1, 0, 0),
                [2] = new int3(0, 0, -1),
                [3] = new int3(0, 0, 1),
                [4] = new int3(1, 0, 0),
                [5] = new int3(1, 0, 0)
            };

            NativeArray<int3> faceUps = new NativeArray<int3>(6, Allocator.Temp)
            {
                [0] = new int3(0, 1, 0),
                [1] = new int3(0, 1, 0),
                [2] = new int3(0, 1, 0),
                [3] = new int3(0, 1, 0),
                [4] = new int3(0, 0, -1),
                [5] = new int3(0, 0, 1)
            };

            float uvWidth = 1.0f / 3.0f;
            float uvHeight = 1.0f / 2.0f;
            float padding = 0.015f;
            int tIndex = 0;

            for (int i = 0; i < 6; i++)
            {
                int3 normal = faceNormals[i];
                int3 right = faceRights[i];
                int3 up = faceUps[i];
                int faceVertOffset = i * vertsPerFace;

                float uCol = i % 3;
                float vRow = i / 3;

                float uMin = uCol * uvWidth + padding;
                float uMax = (uCol + 1) * uvWidth - padding;
                float vMin = vRow * uvHeight + padding;
                float vMax = (vRow + 1) * uvHeight - padding;

                for (int y = 0; y <= r; y++)
                {
                    for (int x = 0; x <= r; x++)
                    {
                        int3 gridPos = normal * r + right * (x * 2 - r) + up * (y * 2 - r);
                        float3 p = new float3(gridPos.x, gridPos.y, gridPos.z) / (float)r;

                        float x2 = p.x * p.x; float y2 = p.y * p.y; float z2 = p.z * p.z;
                        float3 spherePos = new float3(
                            p.x * math.sqrt(1.0f - y2 / 2.0f - z2 / 2.0f + y2 * z2 / 3.0f),
                            p.y * math.sqrt(1.0f - x2 / 2.0f - z2 / 2.0f + x2 * z2 / 3.0f),
                            p.z * math.sqrt(1.0f - x2 / 2.0f - y2 / 2.0f + x2 * y2 / 3.0f)
                        );

                        int vIndex = faceVertOffset + y * (r + 1) + x;

                        // THE FIX: Output raw calculated position. No artificial rounding!
                        Vertices[vIndex] = spherePos;

                        float u = math.lerp(uMin, uMax, (float)x / r);
                        float v = math.lerp(vMin, vMax, (float)y / r);
                        UVs[vIndex] = new float2(u, v);
                    }
                }

                for (int y = 0; y < r; y++)
                {
                    for (int x = 0; x < r; x++)
                    {
                        int i00 = faceVertOffset + y * (r + 1) + x;
                        int i10 = faceVertOffset + y * (r + 1) + (x + 1);
                        int i01 = faceVertOffset + (y + 1) * (r + 1) + x;
                        int i11 = faceVertOffset + (y + 1) * (r + 1) + (x + 1);

                        Triangles[tIndex++] = i00; Triangles[tIndex++] = i11; Triangles[tIndex++] = i01;
                        Triangles[tIndex++] = i00; Triangles[tIndex++] = i10; Triangles[tIndex++] = i11;
                    }
                }
            }

            faceNormals.Dispose(); faceRights.Dispose(); faceUps.Dispose();
        }
    }
}
