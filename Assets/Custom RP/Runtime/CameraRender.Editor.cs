using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
public partial class CameraRender : MonoBehaviour
{
#if UNITY_EDITOR
    static Material errorMaterial;
    static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM"),
    };
    private partial void DrawUnSupportShaders()
    {
        if (errorMaterial == null)
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
        {
            overrideMaterial = errorMaterial
        };
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        var filteringSetting = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSetting);
    }
    private partial void DrawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
    private partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
    }
#endif
#if UNITY_EDITOR
    protected string SampleName { get; set; }
    private partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = camera.name;
        Profiler.EndSample();
    }
#else
    const string SampleName = bufferName;
#endif
}
