using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veridian.RockGenLite.Runtime
{
    public enum RockClusterShape
    {
        Disk,
        Ring,
        Rectangle,
        Line,
        Mound,
        SphereVolume,
        MeshSurface
    }

    [Serializable]
    public sealed class RockClusterSettings
    {
        public const int MaxRockCount = 64;

        public bool enabled;
        [Range(1, MaxRockCount)] public int count = 8;
        public int seed = 12345;
        public RockClusterShape shape = RockClusterShape.Disk;

        [Min(0.1f)] public float radius = 3f;
        [Min(0f)] public float innerRadius = 1.5f;
        public Vector2 rectangleSize = new Vector2(6f, 6f);
        [Min(0.1f)] public float lineLength = 6f;
        [Min(0f)] public float lineWidth = 0.5f;
        [Min(0f)] public float moundHeight = 1.5f;

        [Range(0.05f, 1f)] public float spread = 1f;
        [Range(-1f, 1f)] public float centerBias;
        [Range(0f, 1f)] public float positionVariance = 0.2f;
        [Min(0f)] public float heightVariance = 0.15f;
        [Min(0f)] public float minimumSpacing;
        [Range(1, 64)] public int placementAttempts = 24;

        [Min(0.05f)] public float minimumScale = 0.75f;
        [Min(0.05f)] public float maximumScale = 1.25f;
        [Range(0f, 0.75f)] public float nonUniformScaleVariance = 0.15f;
        [Range(0f, 180f)] public float tiltVariance = 18f;
        public bool alignToSurface = true;
        public float surfaceOffset;

        public GameObject surfaceObject;
        [Range(-1f, 1f)] public float minimumSurfaceUpDot = 0.1f;
        public bool invertSurfaceNormals;
        public bool showSurfaceInPreview = true;
        public bool includeSurfaceInExport;

        public void Sanitize()
        {
            count = Mathf.Clamp(count, 1, MaxRockCount);
            radius = Mathf.Max(0.1f, radius);
            innerRadius = Mathf.Clamp(innerRadius, 0f, radius);
            rectangleSize.x = Mathf.Max(0.1f, rectangleSize.x);
            rectangleSize.y = Mathf.Max(0.1f, rectangleSize.y);
            lineLength = Mathf.Max(0.1f, lineLength);
            lineWidth = Mathf.Max(0f, lineWidth);
            moundHeight = Mathf.Max(0f, moundHeight);
            spread = Mathf.Clamp(spread, 0.05f, 1f);
            centerBias = Mathf.Clamp(centerBias, -1f, 1f);
            positionVariance = Mathf.Clamp01(positionVariance);
            heightVariance = Mathf.Max(0f, heightVariance);
            minimumSpacing = Mathf.Max(0f, minimumSpacing);
            placementAttempts = Mathf.Clamp(placementAttempts, 1, 64);
            minimumScale = Mathf.Max(0.05f, minimumScale);
            maximumScale = Mathf.Max(minimumScale, maximumScale);
            nonUniformScaleVariance = Mathf.Clamp(nonUniformScaleVariance, 0f, 0.75f);
            tiltVariance = Mathf.Clamp(tiltVariance, 0f, 180f);
            minimumSurfaceUpDot = Mathf.Clamp(minimumSurfaceUpDot, -1f, 1f);
        }
    }

    [Serializable]
    public struct RockClusterPlacement
    {
        public int rockSeed;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector3 surfaceNormal;
    }

    /// <summary>
    /// Produces the single deterministic layout consumed by both preview and prefab export.
    /// It deliberately uses System.Random so UnityEngine.Random's global state is never changed.
    /// </summary>
    public static class RockClusterLayoutGenerator
    {
        private sealed class SurfaceTriangle
        {
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;
            public Vector3 normal;
            public float cumulativeArea;
        }

        public static List<RockClusterPlacement> Generate(
            RockClusterSettings cluster,
            RockSettings rockSettings,
            out string warning)
        {
            warning = null;
            var result = new List<RockClusterPlacement>();
            if (cluster == null || rockSettings == null)
            {
                warning = "Cluster and rock settings are required.";
                return result;
            }

            cluster.Sanitize();
            var random = new System.Random(cluster.seed);
            List<SurfaceTriangle> surfaceTriangles = null;
            float surfaceArea = 0f;

            if (cluster.shape == RockClusterShape.MeshSurface)
            {
                if (!TryBuildSurface(cluster, out surfaceTriangles, out surfaceArea, out warning))
                {
                    return result;
                }
            }

            for (int index = 0; index < cluster.count; index++)
            {
                Vector3 position = Vector3.zero;
                Vector3 normal = Vector3.up;
                float normalizedDistance = 0f;

                bool accepted = false;
                int attempts = Mathf.Max(1, cluster.placementAttempts);
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    SamplePosition(cluster, random, surfaceTriangles, surfaceArea, out position, out normal, out normalizedDistance);
                    if (cluster.minimumSpacing <= 0f || IsFarEnough(position, result, cluster.minimumSpacing))
                    {
                        accepted = true;
                        break;
                    }
                }

                // Count is an explicit contract: after the rejection budget is exhausted,
                // keep the best deterministic sample instead of silently exporting fewer rocks.
                if (!accepted && result.Count == 0)
                {
                    position = Vector3.zero;
                    normal = Vector3.up;
                }

                float uniformScale = Lerp(cluster.minimumScale, cluster.maximumScale, Next01(random));
                Vector3 scale = new Vector3(
                    uniformScale * AxisScale(random, cluster.nonUniformScaleVariance),
                    uniformScale * AxisScale(random, cluster.nonUniformScaleVariance),
                    uniformScale * AxisScale(random, cluster.nonUniformScaleVariance));

                float rockHalfHeight = rockSettings.targetDiameter * rockSettings.prefabScale * scale.y * 0.5f;
                bool restsOnSurface = cluster.shape != RockClusterShape.SphereVolume;
                if (restsOnSurface)
                {
                    position += normal * (rockHalfHeight + cluster.surfaceOffset);
                }

                if (cluster.shape == RockClusterShape.Mound)
                {
                    position += Vector3.up * ((1f - normalizedDistance) * cluster.moundHeight);
                }

                position += normal * RandomRange(random, -cluster.heightVariance, cluster.heightVariance);

                Quaternion surfaceRotation = cluster.alignToSurface
                    ? Quaternion.FromToRotation(Vector3.up, normal)
                    : Quaternion.identity;
                Quaternion yaw = Quaternion.AngleAxis(RandomRange(random, 0f, 360f), normal);
                Quaternion tilt = Quaternion.Euler(
                    RandomRange(random, -cluster.tiltVariance, cluster.tiltVariance),
                    0f,
                    RandomRange(random, -cluster.tiltVariance, cluster.tiltVariance));

                result.Add(new RockClusterPlacement
                {
                    rockSeed = DeriveRockSeed(cluster.seed, index),
                    localPosition = position,
                    localRotation = surfaceRotation * yaw * tilt,
                    localScale = scale,
                    surfaceNormal = normal
                });
            }

            return result;
        }

        public static int DeriveRockSeed(int clusterSeed, int index)
        {
            unchecked
            {
                uint value = (uint)clusterSeed;
                value ^= (uint)(index + 1) * 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
                value *= 0xC2B2AE35u;
                value ^= value >> 16;
                int seed = (int)(value & 0x7FFFFFFF);
                return seed == 0 ? index + 1 : seed;
            }
        }

        private static void SamplePosition(
            RockClusterSettings cluster,
            System.Random random,
            List<SurfaceTriangle> surfaceTriangles,
            float surfaceArea,
            out Vector3 position,
            out Vector3 normal,
            out float normalizedDistance)
        {
            normal = Vector3.up;
            normalizedDistance = 0f;

            if (cluster.shape == RockClusterShape.MeshSurface)
            {
                SampleSurface(random, surfaceTriangles, surfaceArea, out position, out normal);
                if (cluster.invertSurfaceNormals) normal = -normal;
                return;
            }

            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float radial01 = BiasedRadius(Next01(random), cluster.centerBias) * cluster.spread;
            normalizedDistance = Mathf.Clamp01(radial01);

            switch (cluster.shape)
            {
                case RockClusterShape.Ring:
                {
                    float inner = cluster.innerRadius / Mathf.Max(cluster.radius, 0.0001f);
                    float ring01 = Mathf.Lerp(inner, 1f, radial01);
                    position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (cluster.radius * ring01);
                    break;
                }
                case RockClusterShape.Rectangle:
                    position = new Vector3(
                        RandomRange(random, -0.5f, 0.5f) * cluster.rectangleSize.x * cluster.spread,
                        0f,
                        RandomRange(random, -0.5f, 0.5f) * cluster.rectangleSize.y * cluster.spread);
                    normalizedDistance = Mathf.Clamp01(new Vector2(
                        position.x / (cluster.rectangleSize.x * 0.5f),
                        position.z / (cluster.rectangleSize.y * 0.5f)).magnitude);
                    break;
                case RockClusterShape.Line:
                    position = new Vector3(
                        RandomRange(random, -0.5f, 0.5f) * cluster.lineLength * cluster.spread,
                        0f,
                        RandomRange(random, -0.5f, 0.5f) * cluster.lineWidth);
                    normalizedDistance = Mathf.Abs(position.x) / (cluster.lineLength * 0.5f);
                    break;
                case RockClusterShape.SphereVolume:
                {
                    Vector3 direction = RandomUnitVector(random);
                    float distance = Mathf.Pow(Next01(random), 1f / 3f) * cluster.radius * cluster.spread;
                    position = direction * distance;
                    normal = direction.sqrMagnitude > 0f ? direction : Vector3.up;
                    normalizedDistance = distance / cluster.radius;
                    break;
                }
                default:
                    position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (cluster.radius * radial01);
                    break;
            }

            if (cluster.shape != RockClusterShape.SphereVolume && cluster.positionVariance > 0f)
            {
                float jitter = cluster.radius * cluster.positionVariance * 0.12f;
                position += new Vector3(
                    RandomRange(random, -jitter, jitter),
                    0f,
                    RandomRange(random, -jitter, jitter));
            }
        }

        private static bool IsFarEnough(
            Vector3 candidate,
            List<RockClusterPlacement> placements,
            float minimumSpacing)
        {
            float spacingSquared = minimumSpacing * minimumSpacing;
            for (int i = 0; i < placements.Count; i++)
            {
                Vector3 delta = candidate - placements[i].localPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude < spacingSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryBuildSurface(
            RockClusterSettings cluster,
            out List<SurfaceTriangle> triangles,
            out float totalArea,
            out string warning)
        {
            triangles = new List<SurfaceTriangle>();
            totalArea = 0f;
            warning = null;

            GameObject root = cluster.surfaceObject;
            if (root == null)
            {
                warning = "Mesh Surface mode needs a scene object or prefab containing MeshFilter components.";
                return false;
            }

            Matrix4x4 rootToLocal = root.transform.worldToLocalMatrix;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
            {
                MeshFilter filter = filters[filterIndex];
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                try
                {
                    Vector3[] vertices = mesh.vertices;
                    int[] indices = mesh.triangles;
                    Matrix4x4 meshToRoot = rootToLocal * filter.transform.localToWorldMatrix;

                    for (int triangleIndex = 0; triangleIndex + 2 < indices.Length; triangleIndex += 3)
                    {
                        Vector3 a = meshToRoot.MultiplyPoint3x4(vertices[indices[triangleIndex]]);
                        Vector3 b = meshToRoot.MultiplyPoint3x4(vertices[indices[triangleIndex + 1]]);
                        Vector3 c = meshToRoot.MultiplyPoint3x4(vertices[indices[triangleIndex + 2]]);
                        Vector3 cross = Vector3.Cross(b - a, c - a);
                        float area = cross.magnitude * 0.5f;
                        Vector3 triangleNormal = cross.normalized;
                        float upDot = Vector3.Dot(
                            cluster.invertSurfaceNormals ? -triangleNormal : triangleNormal,
                            Vector3.up);
                        if (area <= 0.000001f || upDot < cluster.minimumSurfaceUpDot)
                        {
                            continue;
                        }

                        totalArea += area;
                        triangles.Add(new SurfaceTriangle
                        {
                            a = a,
                            b = b,
                            c = c,
                            normal = triangleNormal,
                            cumulativeArea = totalArea
                        });
                    }
                }
                catch (Exception exception)
                {
                    warning = $"Could not read mesh '{mesh.name}': {exception.Message}";
                }
            }

            if (triangles.Count == 0 || totalArea <= 0f)
            {
                warning = warning ?? "The selected surface has no readable MeshFilter triangles.";
                return false;
            }

            return true;
        }

        private static void SampleSurface(
            System.Random random,
            List<SurfaceTriangle> triangles,
            float totalArea,
            out Vector3 position,
            out Vector3 normal)
        {
            float targetArea = Next01(random) * totalArea;
            int low = 0;
            int high = triangles.Count - 1;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (triangles[middle].cumulativeArea < targetArea) low = middle + 1;
                else high = middle;
            }

            SurfaceTriangle triangle = triangles[low];
            float u = Mathf.Sqrt(Next01(random));
            float v = Next01(random);
            float weightA = 1f - u;
            float weightB = u * (1f - v);
            float weightC = u * v;
            position = triangle.a * weightA + triangle.b * weightB + triangle.c * weightC;
            normal = triangle.normal;
        }

        private static float BiasedRadius(float value, float bias)
        {
            float exponent = bias >= 0f
                ? Mathf.Lerp(0.5f, 2.5f, bias)
                : Mathf.Lerp(0.5f, 0.1f, -bias);
            return Mathf.Pow(Mathf.Clamp01(value), exponent);
        }

        private static Vector3 RandomUnitVector(System.Random random)
        {
            float y = RandomRange(random, -1f, 1f);
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle));
        }

        private static float AxisScale(System.Random random, float variance)
        {
            return 1f + RandomRange(random, -variance, variance);
        }

        private static float Next01(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return Lerp(minimum, maximum, Next01(random));
        }

        private static float Lerp(float minimum, float maximum, float value)
        {
            return minimum + (maximum - minimum) * value;
        }
    }
}
