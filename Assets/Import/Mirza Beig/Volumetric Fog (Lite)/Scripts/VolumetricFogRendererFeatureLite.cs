using System;

using UnityEngine;

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace MirzaBeig.VolumetricFogLite
{
    public interface IVolumetricFog
    {
        public int GetDownsampleLevel();
        public void SetDownsampleLevel(int downsampleLevel);
    }

    public class VolumetricFogRendererFeatureLite : ScriptableRendererFeature, IVolumetricFog
    {
        [Serializable]
        public enum RenderTextureQuality
        {
            Low,
            Medium,
            High
        }

        [Serializable]
        public class Settings
        {
            [Range(1, 8)]
            public int fogDownsampleLevel = 4;

            [Space]

            public Material fogMaterial;
            public Material depthMaterial;

            [Space]

            public Material compositeMaterial;

            [Space]

            public string compositeMaterialColourTextureName = "_ColourTexture";

            [Space]

            public string compositeMaterialFogTextureName = "_FogTexture";
            public string compositeMaterialDepthTextureName = "_DepthTexture";

            [Space]

            public RenderTextureQuality renderTextureQuality = RenderTextureQuality.Medium;
        }

        public int GetDownsampleLevel()
        {
            return settings.fogDownsampleLevel;
        }

        public void SetDownsampleLevel(int downsampleLevel)
        {
            settings.fogDownsampleLevel = downsampleLevel;
        }

        public bool renderInSceneView = true;

        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingSkybox;

        [Space]

        public Settings settings;
        private CustomRenderPass customRenderPass;

        public override void Create()
        {
            customRenderPass = new CustomRenderPass(settings, renderInSceneView)
            {
                renderPassEvent = renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!LevelAtmosphere.VolumetricFogEnabled)
                return;

            if (!ShouldEnqueuePass(renderingData.cameraData))
                return;

            renderer.EnqueuePass(customRenderPass);
        }

        protected override void Dispose(bool disposing)
        {
            customRenderPass?.Dispose();
        }

        private bool ShouldEnqueuePass(CameraData cameraData)
        {
            bool enqueuePass = cameraData.cameraType == CameraType.Game;
            enqueuePass |= cameraData.cameraType == CameraType.Reflection;

            if (renderInSceneView)
                enqueuePass |= cameraData.cameraType == CameraType.SceneView;

            return enqueuePass;
        }

        class CustomRenderPass : ScriptableRenderPass
        {
            private readonly Settings settings;
            private readonly bool renderInSceneView;
            private readonly ProfilingSampler profilingSampler = new("VolumetricFogLite");

            private RenderTextureDescriptor colourTextureDescriptor;
            private RenderTextureDescriptor fogTextureDescriptor;
            private RenderTextureDescriptor depthTextureDescriptor;

            private RTHandle colourTextureHandle;
            private RTHandle fogTextureHandle;
            private RTHandle depthTextureHandle;

            public CustomRenderPass(Settings settings, bool renderInSceneView)
            {
                this.settings = settings;
                this.renderInSceneView = renderInSceneView;
                if (settings == null)
                    return;

                RenderTextureFormat renderTextureFormat = GetColorFormat(settings.renderTextureQuality);
                colourTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, renderTextureFormat, 0);
                fogTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, renderTextureFormat, 0);
                depthTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.RFloat, 0);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!LevelAtmosphere.VolumetricFogEnabled || settings == null)
                    return;

                if (!settings.fogMaterial || !settings.depthMaterial || !settings.compositeMaterial)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                if (!ShouldRenderCamera(cameraData.camera.cameraType))
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                if (!source.IsValid())
                    return;

                UpdateDescriptors(cameraData.cameraTargetDescriptor);
                AllocateTargets();

                TextureHandle colour = renderGraph.ImportTexture(colourTextureHandle);
                TextureHandle fog = renderGraph.ImportTexture(fogTextureHandle);
                TextureHandle depth = renderGraph.ImportTexture(depthTextureHandle);

                if (!colour.IsValid() || !fog.IsValid() || !depth.IsValid())
                    return;

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(source, colour, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0),
                    "VolumetricFog_CopyColor");

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(source, depth, settings.depthMaterial, 0),
                    "VolumetricFog_Depth");

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(source, fog, settings.fogMaterial, 0),
                    "VolumetricFog_Fog");

                settings.compositeMaterial.SetTexture(settings.compositeMaterialColourTextureName, colourTextureHandle);
                settings.compositeMaterial.SetTexture(settings.compositeMaterialDepthTextureName, depthTextureHandle);
                settings.compositeMaterial.SetTexture(settings.compositeMaterialFogTextureName, fogTextureHandle);

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(fog, source, settings.compositeMaterial, 0),
                    "VolumetricFog_Composite");

                resourceData.cameraColor = source;
            }

#pragma warning disable 618, 672

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                if (settings == null)
                    return;

                UpdateDescriptors(cameraTextureDescriptor);
                AllocateTargets();
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!LevelAtmosphere.VolumetricFogEnabled || settings == null)
                    return;

                if (!settings.fogMaterial || !settings.depthMaterial || !settings.compositeMaterial)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

                    Blit(cmd, cameraTargetHandle, colourTextureHandle);
                    settings.compositeMaterial.SetTexture(settings.compositeMaterialColourTextureName, colourTextureHandle);

                    Blit(cmd, cameraTargetHandle, depthTextureHandle, settings.depthMaterial);
                    settings.compositeMaterial.SetTexture(settings.compositeMaterialDepthTextureName, depthTextureHandle);

                    Blit(cmd, cameraTargetHandle, fogTextureHandle, settings.fogMaterial);
                    settings.compositeMaterial.SetTexture(settings.compositeMaterialFogTextureName, fogTextureHandle);

                    Blit(cmd, fogTextureHandle, cameraTargetHandle, settings.compositeMaterial);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }

#pragma warning restore 618, 672

            public void Dispose()
            {
                colourTextureHandle?.Release();
                fogTextureHandle?.Release();
                depthTextureHandle?.Release();
            }

            private bool ShouldRenderCamera(CameraType cameraType)
            {
                bool enqueuePass = cameraType == CameraType.Game;
                enqueuePass |= cameraType == CameraType.Reflection;

                if (renderInSceneView)
                    enqueuePass |= cameraType == CameraType.SceneView;

                return enqueuePass;
            }

            private void UpdateDescriptors(RenderTextureDescriptor cameraTextureDescriptor)
            {
                int fogDownsampleLevel = Mathf.Max(1, settings.fogDownsampleLevel);
                RenderTextureFormat colorFormat = GetColorFormat(settings.renderTextureQuality);

                colourTextureDescriptor = cameraTextureDescriptor;
                colourTextureDescriptor.colorFormat = colorFormat;
                colourTextureDescriptor.depthBufferBits = 0;
                colourTextureDescriptor.msaaSamples = 1;

                fogTextureDescriptor = colourTextureDescriptor;
                fogTextureDescriptor.width = Mathf.Max(1, colourTextureDescriptor.width / fogDownsampleLevel);
                fogTextureDescriptor.height = Mathf.Max(1, colourTextureDescriptor.height / fogDownsampleLevel);

                depthTextureDescriptor = fogTextureDescriptor;
                depthTextureDescriptor.colorFormat = RenderTextureFormat.RFloat;
            }

            private void AllocateTargets()
            {
                RenderingUtils.ReAllocateIfNeeded(ref colourTextureHandle, colourTextureDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_VolumetricFogColour");
                RenderingUtils.ReAllocateIfNeeded(ref fogTextureHandle, fogTextureDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_VolumetricFogFog");
                RenderingUtils.ReAllocateIfNeeded(ref depthTextureHandle, depthTextureDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_VolumetricFogDepth");
            }

            private static RenderTextureFormat GetColorFormat(RenderTextureQuality quality)
            {
                return quality switch
                {
                    RenderTextureQuality.Low => RenderTextureFormat.Default,
                    RenderTextureQuality.Medium => RenderTextureFormat.ARGB64,
                    RenderTextureQuality.High => RenderTextureFormat.ARGBFloat,
                    _ => throw new Exception("Unknown enum."),
                };
            }
        }
    }
}
