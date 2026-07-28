using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Veridian.RockGenLite.Runtime.Jobs;

namespace Veridian.RockGenLite.Runtime
{
    public class RuntimeRockGenerator : MonoBehaviour
    {
        private const int UInt16IndexFormatVertexLimit = 65535;

        private Queue<RockRequest> _requestQueue = new Queue<RockRequest>();
        private List<RockGenerationState> _activeGenerations = new List<RockGenerationState>();

        public void GenerateRock(RockRequest request)
        {
            if (request == null)
            {
                return;
            }

            if (request.Settings == null)
            {
                request.OnComplete?.Invoke(null);
                return;
            }

            if (request.SharedMaterial == null)
            {
                request.OnComplete?.Invoke(null);
                return;
            }

            if (request.Settings.lodLevels == null || request.Settings.lodLevels.Count == 0)
            {
                request.OnComplete?.Invoke(null);
                return;
            }

            _requestQueue.Enqueue(request);
        }
        private void Update()
        {
            CheckForCompletion();
            ProcessQueue();
        }
#if UNITY_EDITOR
        public void EditorUpdate()
        {
            Update();
        }
#endif

        private void OnDestroy()
        {
            // If the GameObject is destroyed (e.g. window is closed), immediately complete
            // any in-flight background Burst jobs and dispose of their NativeArray memory.
            if (_activeGenerations != null)
            {
                foreach (var state in _activeGenerations)
                {
                    if (state != null)
                    {
                        state.Dispose();
                    }
                }
                _activeGenerations.Clear();
            }

            if (_requestQueue != null)
            {
                _requestQueue.Clear();
            }
        }
        private void ProcessQueue()
        {
            while (_requestQueue.Count > 0) StartGeneration(_requestQueue.Dequeue());
        }

        private void StartGeneration(RockRequest request)
        {
            RockSettings settings = request.Settings;
            int lodCount = settings.lodLevels.Count;

            // NEW: Determine if we need to silently generate an ultra-low-poly mesh for physics
            bool generateConvex = request.GenerateColliders && settings.colliderType == RockColliderType.ConvexMesh;

            RockShapeData shapeData = RockShapeData.FromSettings(settings);

            RockGenerationState state = new RockGenerationState(request, lodCount, generateConvex);
            _activeGenerations.Add(state);

            float effectiveNormalStrength = settings.normalNoiseStrength * (settings.targetDiameter * 0.2f);
            float effectiveCavityStrength = settings.cavityStrength * 0.2f;

            NativeArray<VertexAttributeDescriptor> layout = default;

            try
            {
                state.MeshDataArray = Mesh.AllocateWritableMeshData(state.TotalMeshCount);
                state.MeshDataAllocated = true;

                layout = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
                layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
                layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1);
                layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, stream: 2);
                layout[3] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, stream: 3);

                var verts = new NativeArray<float3>[state.TotalMeshCount];
                var tris = new NativeArray<int>[state.TotalMeshCount];
                var norms = new NativeArray<float3>[state.TotalMeshCount];
                var uvs = new NativeArray<float2>[state.TotalMeshCount];
                var colors = new NativeArray<Color32>[state.TotalMeshCount];
                int[] vertexCounts = new int[state.TotalMeshCount];

                for (int i = 0; i < state.TotalMeshCount; i++)
                {
                    int vertexCount, triangleCount;
                    bool isConvex = generateConvex && i == lodCount;

                    if (settings.baseShape == RockBaseShape.CubeSphere)
                    {
                        // NEW: Force Grid Resolution 4 (192 Tris) for the Convex Mesh
                        int res = isConvex ? 4 : Mathf.Max(1, settings.lodLevels[i].resolution);
                        vertexCount = 6 * (res + 1) * (res + 1);
                        triangleCount = 36 * res * res;
                    }
                    else
                    {
                        // NEW: Force Subdiv 1 (80 Tris) for the Convex Mesh
                        int subDiv = isConvex ? 1 : Mathf.Clamp(settings.lodLevels[i].subdivisionLevel, 0, 6);
                        int segments = (int)Mathf.Pow(2, subDiv);
                        vertexCount = 20 * ((segments + 1) * (segments + 2) / 2);
                        triangleCount = 60 * segments * segments;
                    }

                    vertexCounts[i] = vertexCount;
                    state.IndexCounts[i] = triangleCount;

                    Mesh.MeshData data = state.MeshDataArray[i];
                    data.SetVertexBufferParams(vertexCount, layout);
                    data.SetIndexBufferParams(triangleCount, IndexFormat.UInt32);

                    verts[i] = data.GetVertexData<float3>(0);
                    norms[i] = data.GetVertexData<float3>(1);
                    uvs[i] = data.GetVertexData<float2>(2);
                    colors[i] = data.GetVertexData<Color32>(3);
                    tris[i] = data.GetIndexData<int>();

                    state.WeldMaps[i] = new NativeArray<int>(vertexCount, Allocator.Persistent);
                }

                JobHandle currentCombined = default;

                for (int i = 0; i < state.TotalMeshCount; i++)
                {
                    var verticesArray = verts[i];
                    var trianglesArray = tris[i];
                    var normalsArray = norms[i];
                    var uvsArray = uvs[i];
                    var colorsArray = colors[i];
                    int vertexCount = vertexCounts[i];

                    bool isConvex = generateConvex && i == lodCount;

                    JobHandle baseShapeHandle;

                    if (settings.baseShape == RockBaseShape.CubeSphere)
                    {
                        int res = isConvex ? 4 : Mathf.Max(1, settings.lodLevels[i].resolution);
                        var cubeJob = new CubeSphereJob { Resolution = res, Vertices = verticesArray, Triangles = trianglesArray, UVs = uvsArray };
                        baseShapeHandle = cubeJob.Schedule();
                    }
                    else
                    {
                        int subDiv = isConvex ? 1 : Mathf.Clamp(settings.lodLevels[i].subdivisionLevel, 0, 6);
                        var icosphereJob = new IcosphereJob { RecursionLevel = subDiv, Vertices = verticesArray, Triangles = trianglesArray, UVs = uvsArray };
                        baseShapeHandle = icosphereJob.Schedule();
                    }
                    currentCombined = JobHandle.CombineDependencies(currentCombined, baseShapeHandle);
                    state.CombinedHandle = currentCombined;

                    var buildWeldMapJob = new BuildWeldMapJob { Vertices = verticesArray, WeldMap = state.WeldMaps[i] };
                    JobHandle weldHandle = buildWeldMapJob.Schedule(baseShapeHandle);
                    currentCombined = JobHandle.CombineDependencies(currentCombined, weldHandle);
                    state.CombinedHandle = currentCombined;

                    var displacementJob = new RockDisplacementJob { Settings = shapeData, Vertices = verticesArray };
                    JobHandle displacementHandle = displacementJob.Schedule(vertexCount, 64, weldHandle);
                    currentCombined = JobHandle.CombineDependencies(currentCombined, displacementHandle);
                    state.CombinedHandle = currentCombined;

                    // NEW: Skip processing Normals, Colors, and UVs for the hidden physical Convex Mesh to save performance
                    if (isConvex)
                    {
                        state.LODHandles[i] = displacementHandle;
                        continue;
                    }

                    var normalJob = new NormalCalculationJob { Vertices = verticesArray, Triangles = trianglesArray, WeldMap = state.WeldMaps[i], Normals = normalsArray };
                    JobHandle normalHandle = normalJob.Schedule(displacementHandle);
                    currentCombined = JobHandle.CombineDependencies(currentCombined, normalHandle);
                    state.CombinedHandle = currentCombined;

                    JobHandle perturbedNormalHandle = normalHandle;
                    bool isVertexColorMode = settings.colorizationMethod == RockColorizationMethod.VertexColors;

                    if (settings.useNormalPerturbation && isVertexColorMode)
                    {
                        var perturbJob = new PerturbNormalsJob { Vertices = verticesArray, Normals = normalsArray, NormalNoiseFrequency = settings.normalNoiseFrequency, NormalNoiseStrength = effectiveNormalStrength, Seed = settings.seed };
                        perturbedNormalHandle = perturbJob.Schedule(vertexCount, 64, normalHandle);
                        currentCombined = JobHandle.CombineDependencies(currentCombined, perturbedNormalHandle);
                        state.CombinedHandle = currentCombined;
                    }

                    var uvColorJob = new UVAndColorJob
                    {
                        Vertices = verticesArray,
                        Normals = normalsArray,
                        UVs = uvsArray,
                        Colors = colorsArray,
                        ColorizationMethod = (int)settings.colorizationMethod,
                        UVScale = settings.uvScale,
                        BlendSharpness = settings.uvBlendSharpness,
                        ColorPattern = (int)settings.colorPattern,
                        PrimaryColor = new float4(settings.primaryColor.r, settings.primaryColor.g, settings.primaryColor.b, settings.primaryColor.a),
                        SecondaryColor = new float4(settings.secondaryColor.r, settings.secondaryColor.g, settings.secondaryColor.b, settings.secondaryColor.a),
                        TertiaryColor = new float4(settings.tertiaryColor.r, settings.tertiaryColor.g, settings.tertiaryColor.b, settings.tertiaryColor.a),
                        CavityColor = new float4(settings.cavityColor.r, settings.cavityColor.g, settings.cavityColor.b, settings.cavityColor.a),

                        SlopeMode = (int)settings.slopeMode,
                        SlopeThreshold = settings.slopeThreshold,
                        SlopeBlend = settings.slopeSmoothness,

                        TextureNoiseFreq = settings.texturingNoiseFrequency,
                        TextureNoiseBlend = settings.texturingNoiseBlend,
                        StrataWarpFreq = settings.strataWarpFrequency,
                        StrataWarpStrength = settings.strataWarpStrength,
                        PatchFreq = settings.patchFrequency,
                        CavityStrength = effectiveCavityStrength,
                        Seed = settings.seed
                    };

                    JobHandle uvColorHandle = uvColorJob.Schedule(vertexCount, 64, perturbedNormalHandle);
                    state.LODHandles[i] = uvColorHandle;
                    currentCombined = JobHandle.CombineDependencies(currentCombined, uvColorHandle);
                    state.CombinedHandle = currentCombined;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RockGenerator] Generation failed during job scheduling: {e.Message}\n{e.StackTrace}");
                try { state.CombinedHandle.Complete(); } catch { }
                state.Dispose();
                _activeGenerations.Remove(state);
                request.OnComplete?.Invoke(null);
            }
            finally
            {
                if (layout.IsCreated)
                {
                    layout.Dispose();
                }
            }
        }

        private void CheckForCompletion()
        {
            for (int i = _activeGenerations.Count - 1; i >= 0; i--)
            {
                RockGenerationState state = _activeGenerations[i];
                if (state.CombinedHandle.IsCompleted)
                {
                    try
                    {
                        state.CombinedHandle.Complete();
                        FinalizeRock(state);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error finalizing rock: {e.Message}\n{e.StackTrace}");
                        state.Request.OnComplete?.Invoke(null);
                    }
                    finally
                    {
                        // FIX: Strict finally guarantees memory cleanups happen
                        state.Dispose();
                        _activeGenerations.RemoveAt(i);
                    }
                }
            }
        }

        private void FinalizeRock(RockGenerationState state)
        {
            RockRequest request = state.Request;
            RockSettings settings = request.Settings;

            Mesh[] meshesArray = new Mesh[state.TotalMeshCount];
            List<Mesh> visualMeshes = new List<Mesh>();
            Mesh convexMesh = null;

            for (int i = 0; i < state.TotalMeshCount; i++)
            {
                meshesArray[i] = new Mesh();

                if (state.HasConvexMesh && i == state.LODCount)
                {
                    meshesArray[i].name = $"{settings.name}_Runtime_ConvexCollider";
                    convexMesh = meshesArray[i];
                }
                else
                {
                    meshesArray[i].name = $"{settings.name}_Runtime_LOD{i}";
                    visualMeshes.Add(meshesArray[i]);
                }

                var meshData = state.MeshDataArray[i];
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, state.IndexCounts[i]), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            }

            var flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

            Mesh.ApplyAndDisposeWritableMeshData(state.MeshDataArray, meshesArray, flags);
            state.MeshDataApplied = true;

            for (int i = 0; i < state.TotalMeshCount; i++)
            {
                TryConvertMeshToUInt16Indices(meshesArray[i]);

                meshesArray[i].RecalculateBounds();

                if (!(state.HasConvexMesh && i == state.LODCount))
                {
                    meshesArray[i].RecalculateTangents();
                }
            }

            GameObject rootGO = new GameObject(settings.name + "_Runtime");
            rootGO.transform.position = request.Position;
            rootGO.transform.rotation = request.Rotation;
            rootGO.transform.localScale = request.Scale;

            LODGroup lodGroup = rootGO.AddComponent<LODGroup>();
            LOD[] lods = new LOD[visualMeshes.Count];

            for (int i = 0; i < visualMeshes.Count; i++)
            {
                GameObject lodGO = new GameObject($"LOD{i}");
                lodGO.transform.SetParent(rootGO.transform, false);
                lodGO.transform.localScale = Vector3.one * settings.prefabScale;

                MeshFilter mf = lodGO.AddComponent<MeshFilter>();
                mf.sharedMesh = visualMeshes[i];

                MeshRenderer mr = lodGO.AddComponent<MeshRenderer>();
                mr.sharedMaterial = request.SharedMaterial;
                if (mr.sharedMaterial != null && !mr.sharedMaterial.enableInstancing) mr.sharedMaterial.enableInstancing = true;

                lods[i] = new LOD(settings.lodLevels[i].screenRelativeTransitionHeight, new Renderer[] { mr });
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            if (request.GenerateColliders && settings.colliderType != RockColliderType.None && visualMeshes.Count > 0)
            {
                Bounds bounds = visualMeshes[0].bounds;

                if (settings.colliderType == RockColliderType.PrimitiveBox)
                {
                    BoxCollider bc = rootGO.AddComponent<BoxCollider>();
                    bc.center = bounds.center * settings.prefabScale;
                    bc.size = bounds.size * settings.prefabScale;
                }
                else if (settings.colliderType == RockColliderType.PrimitiveSphere)
                {
                    SphereCollider sc = rootGO.AddComponent<SphereCollider>();
                    sc.center = bounds.center * settings.prefabScale;
                    sc.radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * settings.prefabScale;
                }
                else if (settings.colliderType == RockColliderType.ConvexMesh && convexMesh != null)
                {
                    GameObject colGO = new GameObject("Collider_Convex");
                    colGO.transform.SetParent(rootGO.transform, false);
                    colGO.transform.localScale = Vector3.one * settings.prefabScale;

                    MeshCollider mc = colGO.AddComponent<MeshCollider>();
                    mc.sharedMesh = convexMesh;
                    mc.convex = true;
                }
                else if (settings.colliderType == RockColliderType.ExactMesh)
                {
                    GameObject colGO = new GameObject("Collider_Exact");
                    colGO.transform.SetParent(rootGO.transform, false);
                    colGO.transform.localScale = Vector3.one * settings.prefabScale;

                    int colliderIndex = Mathf.Clamp(settings.colliderLODIndex, 0, visualMeshes.Count - 1);
                    MeshCollider mc = colGO.AddComponent<MeshCollider>();
                    mc.sharedMesh = visualMeshes[colliderIndex];
                    mc.convex = false;
                }
            }

            request.OnComplete?.Invoke(rootGO);
        }
        private static void TryConvertMeshToUInt16Indices(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (mesh.vertexCount <= 0 || mesh.vertexCount > UInt16IndexFormatVertexLimit)
            {
                return;
            }

            if (mesh.indexFormat == IndexFormat.UInt16)
            {
                return;
            }

            int subMeshCount = mesh.subMeshCount;
            if (subMeshCount <= 0)
            {
                return;
            }

            int[][] subMeshIndices = new int[subMeshCount][];
            MeshTopology[] subMeshTopologies = new MeshTopology[subMeshCount];

            for (int i = 0; i < subMeshCount; i++)
            {
                subMeshTopologies[i] = mesh.GetTopology(i);
                subMeshIndices[i] = mesh.GetIndices(i);
            }

            mesh.indexFormat = IndexFormat.UInt16;
            mesh.subMeshCount = subMeshCount;

            for (int i = 0; i < subMeshCount; i++)
            {
                mesh.SetIndices(subMeshIndices[i], subMeshTopologies[i], i, false);
            }
        }
        #region Internal State Management
        internal class RockGenerationState : IDisposable
        {
            public RockRequest Request;
            public JobHandle CombinedHandle;
            public NativeArray<JobHandle> LODHandles;
            public int LODCount;
            public int TotalMeshCount; // NEW: Tracks visual LODs + hidden convex mesh
            public bool HasConvexMesh; // NEW: Flag if we are building an extra mesh
            public int[] IndexCounts;

            public Mesh.MeshDataArray MeshDataArray;
            public NativeArray<int>[] WeldMaps;

            public bool MeshDataAllocated = false;
            public bool MeshDataApplied = false;

            public RockGenerationState(RockRequest request, int lodCount, bool hasConvex)
            {
                Request = request;
                LODCount = lodCount;
                HasConvexMesh = hasConvex;
                TotalMeshCount = lodCount + (hasConvex ? 1 : 0);

                LODHandles = new NativeArray<JobHandle>(TotalMeshCount, Allocator.Persistent);
                IndexCounts = new int[TotalMeshCount];
                WeldMaps = new NativeArray<int>[TotalMeshCount];
            }

            public void Dispose()
            {
                try { CombinedHandle.Complete(); } catch { }

                if (LODHandles.IsCreated)
                {
                    try
                    {
                        for (int i = 0; i < TotalMeshCount; i++) LODHandles[i].Complete();
                    }
                    catch { }
                    finally
                    {
                        LODHandles.Dispose();
                    }
                }

                if (WeldMaps != null)
                {
                    for (int i = 0; i < TotalMeshCount; i++)
                    {
                        if (WeldMaps.Length > i && WeldMaps[i].IsCreated)
                        {
                            try { WeldMaps[i].Dispose(); } catch { }
                        }
                    }
                }

                if (MeshDataAllocated && !MeshDataApplied)
                {
                    try { MeshDataArray.Dispose(); } catch { }
                    MeshDataAllocated = false;
                }
            }
        }
        #endregion

    }
}