using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";
    const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;
    int ShadowedDirectionalLightCount;

    static int direShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
    dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
    cascadeCountId = Shader.PropertyToID("_CascadeCount"),
    cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
    cascadeDataId = Shader.PropertyToID("_CascadeData"),
    shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
    shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades],
    cascadeData = new Vector4[maxCascades];

    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
    static string[] directionalFilterKeywords = new string[]
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };
    static string[] cascadeBlendKeywords = new string[]
    {
        "_CASCADE_BLEND_HARD",
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };
    ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    CommandBuffer commandBuffer = new CommandBuffer()
    {
        name = bufferName
    };
    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionalLightCount = 0;
    }
    private void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
    }
    public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount
        && light.shadows != LightShadows.None && light.shadowStrength > 0f
        && cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds bounds))
        {
            shadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight
            {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            return new Vector3(light.shadowStrength, settings.directional.cascadeCount * ShadowedDirectionalLightCount++, light.shadowNormalBias);
        }
        return Vector3.zero;
    }
    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
            RenderDirectionalShadows();
        else
        {
            commandBuffer.GetTemporaryRT(direShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }
    private void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        commandBuffer.GetTemporaryRT(direShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        commandBuffer.SetRenderTarget(direShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        commandBuffer.ClearRenderTarget(true, false, Color.clear);
        commandBuffer.BeginSample(bufferName);
        ExecuteBuffer();
        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        float f = 1f - settings.directional.cascadeFade;
        commandBuffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f), 0f));
        SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
        commandBuffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        commandBuffer.EndSample(bufferName);
        ExecuteBuffer();
    }
    private void SetKeywords(string[] keywords, int enableIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enableIndex)
                commandBuffer.EnableShaderKeyword(keywords[i]);
            else
                commandBuffer.DisableShaderKeyword(keywords[i]);
        }
    }
    private void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        var light = shadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        var ratios = settings.CascadeRatios;
        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset, out var viewMatrix, out var projMatrix, out var splitData);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            int tileIndex = tileOffset + i;
            dirShadowMatrices[index] = Convert2AtlasMatrix(projMatrix * viewMatrix, SetTileViewport(index, split, tileSize), split);
            commandBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            commandBuffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
            commandBuffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
            commandBuffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
            commandBuffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
            commandBuffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            commandBuffer.SetGlobalDepthBias(0f, 0f);
        }
    }
    private void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }
    private Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        var offset = new Vector2(index % split, index / split);
        commandBuffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }
    private Matrix4x4 Convert2AtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 *= -1;
            m.m21 *= -1;
            m.m22 *= -1;
            m.m23 *= -1;
        }
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.x * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.x * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.x * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.x * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }
    public void Cleanup()
    {
        commandBuffer.ReleaseTemporaryRT(direShadowAtlasId);
        ExecuteBuffer();
    }

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
}
