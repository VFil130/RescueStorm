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
            // Требуем буфер глубины (для World Space)
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData == null)
                return;

            TextureHandle inputHandle = resourceData.activeColorTexture;

            var desc = renderGraph.GetTextureDesc(inputHandle);
            desc.name = "_TempGrainRT";
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;

            TextureHandle tempHandle = renderGraph.CreateTexture(desc);

            // Проход 1: зерно
            using (var builder = renderGraph.AddUnsafePass<PassData>("ApplyGrain", out var passData))
            {
                passData.material = material;
                passData.source = inputHandle;
                passData.destination = tempHandle;

                builder.UseTexture(inputHandle, AccessFlags.Read);
                builder.UseTexture(tempHandle, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    cmd.SetRenderTarget(data.destination);
                    cmd.SetGlobalTexture("_BlitTexture", data.source);
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1);
                });
            }

            // Проход 2: копия обратно
            using (var builder = renderGraph.AddUnsafePass<PassData>("CopyBack", out var passData))
            {
                passData.material = null;
                passData.source = tempHandle;
                passData.destination = inputHandle;

                builder.UseTexture(tempHandle, AccessFlags.Read);
                builder.UseTexture(inputHandle, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    cmd.Blit(data.source, data.destination);
                });
            }
        }
    }

    private class PassData
    {
        internal Material material;
        internal TextureHandle source;
        internal TextureHandle destination;
    }
}