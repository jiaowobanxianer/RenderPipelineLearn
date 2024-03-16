using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting";
    const int maxDirLightCount = 4;
    static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
    dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
    dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount],
    dirLightDirections = new Vector4[maxDirLightCount];
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    CullingResults cullingResults;

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        SetupLights();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    private void SetupLights()
    {
        var visibleLights = cullingResults.visibleLights;
        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            var light = visibleLights[i];
            SetupDirectionalLight(dirLightCount++,ref light);
            if (dirLightCount >= maxDirLightCount)
                break;
        }
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
    }
    private void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
    }
}
