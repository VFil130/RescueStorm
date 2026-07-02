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
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData == null)
                return;

            TextureHandle inputHandle = resourceData.activeColorTexture;

            // Временная текстура для обработки
            var desc = renderGraph.GetTextureDesc(inputHandle);
            desc.name = "_TempGrainRT";
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;

            TextureHandle tempHandle = renderGraph.CreateTexture(desc);

            // Проход 1: рендерим зерно во временную текстуру
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
                    // Устанавливаем временную текстуру как цель рендеринга
                    cmd.SetRenderTarget(data.destination);
                    // Передаём исходную картинку в шейдер через глобальную текстуру _BlitTexture
                    cmd.SetGlobalTexture("_BlitTexture", data.source);
                    // Рисуем полноэкранный треугольник (3 вершины)
                    cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1);
                });
            }

            // Проход 2: копируем результат обратно в кадровый буфер
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
                    // Простое копирование без материала
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