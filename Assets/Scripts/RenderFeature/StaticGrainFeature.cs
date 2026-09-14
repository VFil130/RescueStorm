using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class StaticGrainFeature : ScriptableRendererFeature
{
    [SerializeField] private Material grainMaterial = null;
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

    private StaticGrainPass grainPass;

    public override void Create()
    {
        grainPass = new StaticGrainPass(grainMaterial, renderPassEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (grainMaterial != null)
            renderer.EnqueuePass(grainPass);
    }

    private class StaticGrainPass : ScriptableRenderPass
    {
        private Material material;

        public StaticGrainPass(Material mat, RenderPassEvent evt)
        {
            material = mat;
            renderPassEvent = evt;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData == null) return;

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_TempGrainRT";
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;

            TextureHandle tempHandle = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("ApplyGrain", out var passData))
            {
                passData.material = material;
                passData.source = source;

                // Читаем source, пишем в temp. Явно объявлено — Render Graph знает о зависимостях.
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempHandle, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(
                        ctx.cmd,
                        data.source,
                        new Vector4(1, 1, 0, 0),
                        data.material,
                        0
                    );
                });
            }

            // Ключевой момент: не копируем обратно, а ПОДМЕНЯЕМ активный color texture.
            // Render Graph переключит зависимости автоматически, и последующие пассы
            // (в т.ч. overlay-камеры) будут читать уже наш результат.
            resourceData.cameraColor = tempHandle;
        }
    }

    private class PassData
    {
        internal Material material;
        internal TextureHandle source;
    }
}