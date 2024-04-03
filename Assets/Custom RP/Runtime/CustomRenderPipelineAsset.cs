using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline Asset")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    private bool useDynamicBatching;
    [SerializeField]
    private bool useGPUInstancing;
    [SerializeField]
    private bool useSRPBatcher;
    [SerializeField]
    private ShadowSettings shadows = default;
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher,shadows);
    }
}
