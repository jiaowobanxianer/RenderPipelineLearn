using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public partial class CameraRender : MonoBehaviour
{
    const string bufferName = "Render Camera";
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
    litShaderTagId = new ShaderTagId("CustomLit");
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    ScriptableRenderContext context;
    CullingResults cullingResults;

    Camera camera;
    Lighting lighting = new Lighting();
    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing)
    {
        this.context = context;
        this.camera = camera;
        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull())
        {
            return;
        }
        Setup();
        lighting.Setup(context,cullingResults);
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        DrawUnSupportShaders();
        DrawGizmos();
        Submit();
    }
    private void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth, flags <= CameraClearFlags.Color, flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }
    private bool Cull()
    {
        if (!camera.TryGetCullingParameters(out var cullingParameters))
        {
            return false;
        }
        cullingResults = context.Cull(ref cullingParameters);
        return true;
    }
    private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
    {
        var sortingSetting = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSetting = new DrawingSettings(unlitShaderTagId, sortingSetting)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing
        };
        drawingSetting.SetShaderPassName(1, litShaderTagId);
        var filteringSetting = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSetting, ref filteringSetting);
        context.DrawSkybox(camera);

        sortingSetting.criteria = SortingCriteria.CommonTransparent;
        drawingSetting.sortingSettings = sortingSetting; ;
        filteringSetting.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSetting, ref filteringSetting);
    }
    private partial void DrawUnSupportShaders();
    private partial void DrawGizmos();
    private partial void PrepareForSceneWindow();
    private partial void PrepareBuffer();
    private void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }
    private void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
