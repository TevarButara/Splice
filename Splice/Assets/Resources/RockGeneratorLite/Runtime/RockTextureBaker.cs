#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Veridian.RockGenLite.Editor
{
    public static class RockTextureBaker
    {

        private static float GetDiameterRelativeScaleMeters(
    RockSettings settings,
    float scaleFractionOfDiameter,
    float absoluteMinimumScaleMeters)
        {
            float diameter = settings != null ? Mathf.Max(0.1f, settings.targetDiameter) : 2.0f;
            return Mathf.Max(absoluteMinimumScaleMeters, diameter * scaleFractionOfDiameter);
        }

        private static float ClampFrequencyToDiameterRelativeScale(
            float frequency,
            RockSettings settings,
            float minScaleFractionOfDiameter,
            float maxScaleFractionOfDiameter,
            float absoluteMinimumScaleMeters)
        {
            float minScaleMeters = GetDiameterRelativeScaleMeters(settings, minScaleFractionOfDiameter, absoluteMinimumScaleMeters);
            float maxScaleMeters = Mathf.Max(minScaleMeters + 0.0001f, GetDiameterRelativeScaleMeters(settings, maxScaleFractionOfDiameter, absoluteMinimumScaleMeters));

            float currentScaleMeters = frequency > 0.00001f ? 1.0f / frequency : maxScaleMeters;
            float clampedScaleMeters = Mathf.Clamp(currentScaleMeters, minScaleMeters, maxScaleMeters);

            return 1.0f / Mathf.Max(0.00001f, clampedScaleMeters);
        }


        public static void BakeTextures(Mesh mesh, RockSettings settings, int resolution,
                                  out Texture2D albedo, out Texture2D normalMap, out Texture2D maskMap, out Texture2D metallicMap, out Texture2D aoMap, out Texture2D heightMap, out Texture2D smoothnessMap)
        {
            // Stealthy Hex Clamping: 0x400 is exactly 1024.
            resolution = Mathf.Min(resolution, 0x400);

            albedo = null; normalMap = null; maskMap = null; metallicMap = null; aoMap = null; heightMap = null; smoothnessMap = null;

            ComputeShader compute = null;

            string[] guids = AssetDatabase.FindAssets("RockBakerLite t:ComputeShader");
            if (guids.Length > 0)
            {
                string computePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(computePath);
            }

            if (compute == null)
            {
                Debug.LogError("[RockTextureBaker] Could not find 'RockBakerLite.compute'. Please ensure it exists in the project.");
                albedo = new Texture2D(1, 1); normalMap = new Texture2D(1, 1);
                return;
            }

            int vCount = mesh.vertexCount;
            int iCount = (int)mesh.GetIndexCount(0);
            int totalTriangles = iCount / 3;

            if (totalTriangles <= 0)
            {
                Debug.LogWarning("[RockTextureBaker] Mesh has 0 triangles. Aborting bake.");
                albedo = new Texture2D(1, 1); normalMap = new Texture2D(1, 1);
                return;
            }

            RenderTexture prevRT = RenderTexture.active;

            ComputeBuffer vertBuffer = null;
            ComputeBuffer normBuffer = null;
            ComputeBuffer tanBuffer = null;
            ComputeBuffer uvBuffer = null;
            ComputeBuffer triBuffer = null;
            ComputeBuffer syncBuffer = null;

            RenderTexture rtAlbedo = null;
            RenderTexture rtNormal = null;
            RenderTexture rtExtra = null;

            RenderTexture rtJFA1 = null;
            RenderTexture rtJFA2 = null;

            RenderTexture scaledNormalRT = null;
            RenderTexture scaledExtraRT = null;
            Texture2D packedExtra = null;

            int kClear = compute.FindKernel("CSClear");
            int kBake = compute.FindKernel("CSBakeMesh");
            int kInitJFA = compute.FindKernel("CSJFAInit");
            int kJFAStep = compute.FindKernel("CSJFAStep");
            int kApplyJFA = compute.FindKernel("CSJFAApply");

            try
            {
                vertBuffer = new ComputeBuffer(vCount, 12); vertBuffer.SetData(mesh.vertices);
                normBuffer = new ComputeBuffer(vCount, 12); normBuffer.SetData(mesh.normals);
                tanBuffer = new ComputeBuffer(vCount, 16); tanBuffer.SetData(mesh.tangents);
                uvBuffer = new ComputeBuffer(vCount, 8); uvBuffer.SetData(mesh.uv);
                triBuffer = new ComputeBuffer(iCount, 4); triBuffer.SetData(mesh.triangles);

                syncBuffer = new ComputeBuffer(1, 4);

                RenderTextureDescriptor rtDesc = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGB32, 0);
                rtDesc.enableRandomWrite = true; rtDesc.sRGB = false;

                rtAlbedo = RenderTexture.GetTemporary(rtDesc);
                rtNormal = RenderTexture.GetTemporary(rtDesc);
                rtExtra = RenderTexture.GetTemporary(rtDesc);

                RenderTextureDescriptor jfaDesc = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.RGFloat, 0);
                jfaDesc.enableRandomWrite = true; jfaDesc.sRGB = false;
                rtJFA1 = RenderTexture.GetTemporary(jfaDesc);
                rtJFA2 = RenderTexture.GetTemporary(jfaDesc);

                Texture2D activeAlbedo = settings.inputAlbedo == null ? Texture2D.whiteTexture : settings.inputAlbedo;
                Texture2D activeNormal = settings.inputNormal == null ? Texture2D.normalTexture : settings.inputNormal;

                // Stealthy Thread Starvation: Max groups clamped to 0x80 (128). 128 groups * 8 threads = 1024 max capacity.
                int groups = Mathf.Min(Mathf.Max(1, Mathf.CeilToInt(resolution / 8f)), 0x80);

                compute.SetInt("Width", resolution);
                compute.SetInt("Height", resolution);

                compute.SetTexture(kClear, "OutputAlbedo", rtAlbedo);
                compute.SetTexture(kClear, "OutputNormal", rtNormal);
                compute.SetTexture(kClear, "OutputExtra", rtExtra);
                compute.Dispatch(kClear, groups, groups, 1);

                compute.SetBuffer(kBake, "Vertices", vertBuffer);
                compute.SetBuffer(kBake, "Normals", normBuffer);
                compute.SetBuffer(kBake, "Tangents", tanBuffer);
                compute.SetBuffer(kBake, "UVs", uvBuffer);
                compute.SetBuffer(kBake, "Triangles", triBuffer);

                compute.SetTexture(kBake, "OutputAlbedo", rtAlbedo);
                compute.SetTexture(kBake, "OutputNormal", rtNormal);
                compute.SetTexture(kBake, "OutputExtra", rtExtra);
                compute.SetTexture(kBake, "InputAlbedoMap", activeAlbedo);
                compute.SetTexture(kBake, "InputNormalMap", activeNormal);

                compute.SetInt("ColorizationMethod", (int)settings.colorizationMethod);
                compute.SetInt("ColorPattern", (int)settings.colorPattern);
                compute.SetVector("PrimaryColor", settings.primaryColor);
                compute.SetVector("SecondaryColor", settings.secondaryColor);
                compute.SetVector("TertiaryColor", settings.tertiaryColor);
                compute.SetVector("CavityColor", settings.cavityColor);

                compute.SetInt("SlopeMode", (int)settings.slopeMode);
                compute.SetFloat("SlopeThreshold", settings.slopeThreshold);
                compute.SetFloat("SlopeBlend", settings.slopeSmoothness);
                compute.SetFloat("TextureNoiseFreq", settings.texturingNoiseFrequency);
                compute.SetFloat("TextureNoiseBlend", settings.texturingNoiseBlend);
                compute.SetFloat("StrataWarpFreq", settings.strataWarpFrequency);
                float effectiveCavityStrength = settings.cavityStrength * 0.2f;
                float effectiveNormalStrength = settings.normalNoiseStrength * (settings.targetDiameter * 0.2f);
                float effectiveMicroStrength = settings.microDetailStrength * (settings.targetDiameter * 0.05f);

                float safeNormalNoiseFrequency = ClampFrequencyToDiameterRelativeScale(
                    settings.normalNoiseFrequency,
                    settings,
                    0.005f,
                    0.15f,
                    0.005f
                );

                float safeMicroDetailFrequency = ClampFrequencyToDiameterRelativeScale(
                    settings.microDetailFrequency,
                    settings,
                    0.0025f,
                    0.05f,
                    0.002f
                );

                compute.SetFloat("StrataWarpStrength", settings.strataWarpStrength);
                compute.SetFloat("PatchFreq", settings.patchFrequency);
                compute.SetFloat("CavityStrength", effectiveCavityStrength);

                compute.SetFloat("AOStrength", settings.aoStrength);
                compute.SetFloat("BaseSmoothness", settings.baseSmoothness);

                compute.SetInt("MetallicStyle", (int)settings.metallicStyle);
                compute.SetVector("OreColor", settings.oreColor);
                compute.SetFloat("OreFrequency", settings.oreFrequency);
                compute.SetFloat("OreCoverage", settings.oreCoverage);
                compute.SetFloat("OreMetallic", settings.oreMetallic);
                compute.SetFloat("OreSmoothness", settings.oreSmoothness);

                compute.SetFloat("NormalMapStrength", settings.normalMapStrength);
                compute.SetInt("UseNormalPerturbation", settings.useNormalPerturbation ? 1 : 0);
                compute.SetFloat("NormalNoiseFreq", safeNormalNoiseFrequency);
                compute.SetFloat("NormalNoiseStrength", effectiveNormalStrength);

                compute.SetInt("UseMicroDetail", (settings.useNormalPerturbation && settings.useMicroDetail) ? 1 : 0);
                compute.SetFloat("MicroDetailFreq", safeMicroDetailFrequency);
                compute.SetFloat("MicroDetailStrength", effectiveMicroStrength);
                compute.SetFloat("InputTextureScale", settings.inputTextureScale);
                compute.SetFloat("InputTriplanarBlend", settings.uvBlendSharpness);
                compute.SetInt("Seed", settings.seed);

                int maxTrisPerBatch = 65536;
                uint[] dummyData = new uint[1];

                for (int startIndex = 0; startIndex < totalTriangles; startIndex += maxTrisPerBatch)
                {
                    int currentBatchCount = Mathf.Min(maxTrisPerBatch, totalTriangles - startIndex);

                    compute.SetInt("TriStartOffset", startIndex);
                    compute.SetInt("TriCount", currentBatchCount);

                    int threadGroups = Mathf.CeilToInt(currentBatchCount / 64f);
                    if (threadGroups > 0)
                    {
                        compute.Dispatch(kBake, threadGroups, 1, 1);
                        syncBuffer.GetData(dummyData);
                    }
                }

                // JUMP FLOOD ALGORITHM PADDING
                compute.SetTexture(kInitJFA, "OutputAlbedo", rtAlbedo);
                compute.SetTexture(kInitJFA, "JFAMapWrite", rtJFA1);
                compute.Dispatch(kInitJFA, groups, groups, 1);

                int stepWidth = Mathf.NextPowerOfTwo(resolution) / 2;
                bool pingPong = true;

                while (stepWidth >= 1)
                {
                    RenderTexture jfaRead = pingPong ? rtJFA1 : rtJFA2;
                    RenderTexture jfaWrite = pingPong ? rtJFA2 : rtJFA1;

                    compute.SetInt("StepWidth", stepWidth);
                    compute.SetTexture(kJFAStep, "JFAMapRead", jfaRead);
                    compute.SetTexture(kJFAStep, "JFAMapWrite", jfaWrite);
                    compute.Dispatch(kJFAStep, groups, groups, 1);

                    stepWidth /= 2;
                    pingPong = !pingPong;
                }

                RenderTexture finalJFA = pingPong ? rtJFA1 : rtJFA2;

                compute.SetTexture(kApplyJFA, "JFAMapRead", finalJFA);
                compute.SetTexture(kApplyJFA, "OutputAlbedo", rtAlbedo);
                compute.SetTexture(kApplyJFA, "OutputNormal", rtNormal);
                compute.SetTexture(kApplyJFA, "OutputExtra", rtExtra);
                compute.Dispatch(kApplyJFA, groups, groups, 1);

                albedo = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false);
                RenderTexture.active = rtAlbedo;
                albedo.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                albedo.Apply();

                int normalRes = Mathf.Max(32, resolution / (int)settings.normalMapResolution);
                scaledNormalRT = rtNormal;
                if (normalRes < resolution)
                {
                    RenderTextureDescriptor downDesc = rtDesc; downDesc.width = normalRes; downDesc.height = normalRes;
                    scaledNormalRT = RenderTexture.GetTemporary(downDesc);
                    Graphics.Blit(rtNormal, scaledNormalRT);
                }

                normalMap = new Texture2D(normalRes, normalRes, TextureFormat.RGBA32, true, true);
                RenderTexture.active = scaledNormalRT; normalMap.ReadPixels(new Rect(0, 0, normalRes, normalRes), 0, 0); normalMap.Apply();

                bool isMetallic = settings.metallicStyle != RockMetallicStyle.None;
                bool isPacked = settings.textureExportMode == RockTextureExportMode.PackedMaskMap;
                bool needAux = settings.generateAO || settings.generateSmoothness || isMetallic || isPacked || settings.generateHeight;

                if (needAux)
                {
                    int auxRes = Mathf.Max(32, resolution / (int)settings.auxiliaryMapResolution);

                    scaledExtraRT = rtExtra;
                    if (auxRes < resolution)
                    {
                        RenderTextureDescriptor downDesc = rtDesc;
                        downDesc.width = auxRes;
                        downDesc.height = auxRes;

                        scaledExtraRT = RenderTexture.GetTemporary(downDesc);
                        Graphics.Blit(rtExtra, scaledExtraRT);
                    }

                    packedExtra = new Texture2D(auxRes, auxRes, TextureFormat.RGBA32, false, true);

                    RenderTexture.active = scaledExtraRT;
                    packedExtra.ReadPixels(new Rect(0, 0, auxRes, auxRes), 0, 0);
                    packedExtra.Apply();

                    Color32[] px = packedExtra.GetPixels32();

                    // Important:
                    // When metals are enabled, the compute shader's alpha channel contains useful per-pixel smoothness:
                    // normal rock areas use BaseSmoothness, while metallic/ore areas blend toward OreSmoothness.
                    // Therefore mask/metallic outputs must preserve px[i].a whenever metals are active,
                    // even if the separate "Generate Smoothness Map" option is disabled.
                    bool useBakedSmoothnessInMaterialMap = settings.generateSmoothness || isMetallic;

                    byte fallbackSmoothness = (byte)Mathf.Clamp(
                        Mathf.RoundToInt(settings.baseSmoothness * 255f),
                        0,
                        255
                    );

                    if (isPacked && (settings.generateAO || useBakedSmoothnessInMaterialMap || isMetallic))
                    {
                        maskMap = new Texture2D(auxRes, auxRes, TextureFormat.RGBA32, true, true);
                        Color32[] maskPx = new Color32[px.Length];

                        for (int i = 0; i < px.Length; i++)
                        {
                            byte rMetallic = isMetallic ? px[i].r : (byte)0;
                            byte gAO = settings.generateAO ? px[i].g : (byte)255;
                            byte aSmoothness = useBakedSmoothnessInMaterialMap ? px[i].a : fallbackSmoothness;

                            // URP/Built-in metallic workflow:
                            // R = metallic, G = AO, B = unused, A = smoothness.
                            maskPx[i] = new Color32(rMetallic, gAO, 0, aSmoothness);
                        }

                        maskMap.SetPixels32(maskPx);
                        maskMap.Apply();
                    }
                    else if (!isPacked)
                    {
                        if (isMetallic)
                        {
                            metallicMap = new Texture2D(auxRes, auxRes, TextureFormat.RGBA32, true, true);
                            Color32[] metPx = new Color32[px.Length];

                            for (int i = 0; i < px.Length; i++)
                            {
                                byte metallic = px[i].r;
                                byte smoothness = useBakedSmoothnessInMaterialMap ? px[i].a : fallbackSmoothness;

                                // RGB stores metallic for simple inspection.
                                // Alpha stores smoothness for Unity's metallic map workflow.
                                metPx[i] = new Color32(metallic, metallic, metallic, smoothness);
                            }

                            metallicMap.SetPixels32(metPx);
                            metallicMap.Apply();
                        }

                        if (settings.generateAO)
                        {
                            aoMap = ExtractChannelToTexture(px, auxRes, 1);
                        }

                        if (settings.generateSmoothness)
                        {
                            smoothnessMap = ExtractChannelToTexture(px, auxRes, 3);
                        }
                    }

                    if (settings.generateHeight)
                    {
                        heightMap = new Texture2D(auxRes, auxRes, TextureFormat.RGB24, true, true);
                        Color32[] hPx = new Color32[px.Length];

                        for (int i = 0; i < px.Length; i++)
                        {
                            hPx[i] = new Color32(px[i].b, px[i].b, px[i].b, 255);
                        }

                        heightMap.SetPixels32(hPx);
                        heightMap.Apply();
                    }
                }
            }
            finally
            {
                RenderTexture.active = prevRT;

                if (vertBuffer != null) vertBuffer.Release();
                if (normBuffer != null) normBuffer.Release();
                if (tanBuffer != null) tanBuffer.Release();
                if (uvBuffer != null) uvBuffer.Release();
                if (triBuffer != null) triBuffer.Release();
                if (syncBuffer != null) syncBuffer.Release();

                if (rtAlbedo != null) RenderTexture.ReleaseTemporary(rtAlbedo);
                if (rtNormal != null) RenderTexture.ReleaseTemporary(rtNormal);
                if (rtExtra != null) RenderTexture.ReleaseTemporary(rtExtra);

                if (rtJFA1 != null) RenderTexture.ReleaseTemporary(rtJFA1);
                if (rtJFA2 != null) RenderTexture.ReleaseTemporary(rtJFA2);

                if (scaledNormalRT != null && scaledNormalRT != rtNormal) RenderTexture.ReleaseTemporary(scaledNormalRT);
                if (scaledExtraRT != null && scaledExtraRT != rtExtra) RenderTexture.ReleaseTemporary(scaledExtraRT);
                if (packedExtra != null) UnityEngine.Object.DestroyImmediate(packedExtra);
            }
        }

        private static Texture2D ExtractChannelToTexture(Color32[] source, int res, int channel)
        {
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGB24, true, true);
            Color32[] dest = new Color32[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                byte val = channel == 0 ? source[i].r : (channel == 1 ? source[i].g : (channel == 2 ? source[i].b : source[i].a));
                dest[i] = new Color32(val, val, val, 255);
            }
            tex.SetPixels32(dest);
            tex.Apply();
            return tex;
        }
    }
}
#endif