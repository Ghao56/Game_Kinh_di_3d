using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class InteractableOutlineFeature : ScriptableRendererFeature
{
    [Header("Shaders")]
    [SerializeField] private Shader maskShader;
    [SerializeField] private Shader compositeShader;

    [Header("Highlight")]
    [Tooltip("Bit trong renderingLayerMask đánh dấu vật được highlight (khớp InteractableGlow.highlightRenderingLayerBit).")]
    [SerializeField] private uint highlightRenderingLayerBit = 1u << 1;

    [Header("Outline")]
    [Tooltip("Độ dày viền tính bằng pixel (đúng nghĩa pixel-perfect).")]
    [SerializeField, Min(1f)] private float outlineThicknessPx = 3f;
    [SerializeField, Range(0f, 2f)] private float glowIntensity = 1f;
    [SerializeField, Range(0f, 20f)] private float pulseSpeed = 3f;
    [SerializeField, Range(0f, 1f)] private float pulseMin = 0.4f;

    [Header("Performance")]
    [Tooltip("1 = full resolution, 2 = nửa resolution (giảm tải GPU trên máy yếu).")]
    [SerializeField, Range(1, 4)] private int downsample = 1;

    private InteractableOutlinePass pass;
    private Material maskMaterial;
    private Material compositeMaterial;

    private static readonly int OutlineThicknessPxId = Shader.PropertyToID("_OutlineThicknessPx");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseMinId = Shader.PropertyToID("_PulseMin");

    public override void Create()
    {
        if (maskShader == null)
            maskShader = Shader.Find("Custom/InteractableOutlineMask");
        if (compositeShader == null)
            compositeShader = Shader.Find("Custom/InteractableOutlineComposite");

        DestroyMaterials();
        if (maskShader != null)
            maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
        if (compositeShader != null)
            compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);

        pass = new InteractableOutlinePass(name);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (maskMaterial == null || compositeMaterial == null)
            return;
        if (renderingData.cameraData.cameraType == CameraType.Preview
            || renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        compositeMaterial.SetFloat(OutlineThicknessPxId, outlineThicknessPx);
        compositeMaterial.SetFloat(GlowIntensityId, glowIntensity);
        compositeMaterial.SetFloat(PulseSpeedId, pulseSpeed);
        compositeMaterial.SetFloat(PulseMinId, pulseMin);

        pass.Setup(highlightRenderingLayerBit, downsample, maskMaterial, compositeMaterial);
        pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        DestroyMaterials();
        pass = null;
    }

    private void DestroyMaterials()
    {
        if (maskMaterial != null)
        {
            CoreUtils.Destroy(maskMaterial);
            maskMaterial = null;
        }
        if (compositeMaterial != null)
        {
            CoreUtils.Destroy(compositeMaterial);
            compositeMaterial = null;
        }
    }

    private class InteractableOutlinePass : ScriptableRenderPass
    {
        private uint highlightBit;
        private int downsample;
        private Material maskMaterial;
        private Material compositeMaterial;

        private static readonly List<ShaderTagId> ShaderTagIds = new List<ShaderTagId>
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
        };

        public InteractableOutlinePass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);
        }

        public void Setup(uint bit, int downscale, Material mask, Material composite)
        {
            highlightBit = bit;
            downsample = downscale;
            maskMaterial = mask;
            compositeMaterial = composite;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            if (!resources.cameraColor.IsValid())
                return;

            var maskDesc = renderGraph.GetTextureDesc(resources.cameraColor);
            maskDesc.name = "_InteractableOutlineMask";
            maskDesc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
            maskDesc.msaaSamples = (MSAASamples)1;
            maskDesc.depthBufferBits = 0;
            maskDesc.slices = 1;
            maskDesc.width = Mathf.Max(1, maskDesc.width / downsample);
            maskDesc.height = Mathf.Max(1, maskDesc.height / downsample);
            maskDesc.clearBuffer = true;
            maskDesc.clearColor = Color.clear;

            TextureHandle maskHandle = renderGraph.CreateTexture(in maskDesc);

            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(passName + "_Mask", out var passData, profilingSampler))
            {
                passData.renderers = CreateMaskRendererList(renderGraph, cameraData, renderingData, lightData);
                builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);
                builder.UseRendererList(passData.renderers);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(data.renderers);
                });
            }

            if (!resources.activeColorTexture.IsValid())
                return;

            var blitParameters = new RenderGraphUtils.BlitMaterialParameters(maskHandle, resources.activeColorTexture, compositeMaterial, 0);
            renderGraph.AddBlitPass(blitParameters, passName + "_Composite");
        }

        private RendererListHandle CreateMaskRendererList(RenderGraph renderGraph, UniversalCameraData cameraData,
            UniversalRenderingData renderingData, UniversalLightData lightData)
        {
            DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds, renderingData,
                cameraData, lightData, SortingCriteria.SortingLayer);
            drawSettings.overrideMaterial = maskMaterial;
            drawSettings.overrideMaterialPassIndex = 0;

            var filterSettings = new FilteringSettings(RenderQueueRange.all, -1)
            {
                renderingLayerMask = highlightBit
            };

            var param = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
            return renderGraph.CreateRendererList(in param);
        }

        private class MaskPassData
        {
            public RendererListHandle renderers;
        }
    }
}