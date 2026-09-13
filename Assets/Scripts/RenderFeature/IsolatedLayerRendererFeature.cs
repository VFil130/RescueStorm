using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class IsolatedLayerRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private LayerMask layerMask;

    private IsolatedLayerPass _pass;

    public override void Create()
    {
        _pass = new IsolatedLayerPass(layerMask)
        {
            // Выполняем ПОСЛЕ непрозрачных объектов, но ДО постобработки (TAA)
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_pass);
    }

    private sealed class IsolatedLayerPass : ScriptableRenderPass
    {
        private readonly LayerMask _layerMask;

        // Тэги шейдерных проходов, которые будут отрисовываться
        private static readonly ShaderTagId UniversalForward = new("UniversalForward");
        private static readonly ShaderTagId UniversalForwardOnly = new("UniversalForwardOnly");
        private static readonly ShaderTagId SRPDefaultUnlit = new("SRPDefaultUnlit");

        // Класс для передачи данных в функцию отрисовки
        private sealed class PassData
        {
            public RendererListHandle RendererList;
        }

        public IsolatedLayerPass(LayerMask layerMask)
        {
            _layerMask = layerMask;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // 1. Получаем доступ к данным кадра
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            // 2. Создаём дескриптор для отдельного буфера глубины
            RenderTextureDescriptor depthDescriptor = cameraData.cameraTargetDescriptor;
            depthDescriptor.graphicsFormat = GraphicsFormat.None;
            depthDescriptor.depthStencilFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil);

            // 3. Создаём временную текстуру для глубины
            TextureHandle isolatedDepth = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, depthDescriptor, "_IsolatedLayerDepth", true);

            // 4. Добавляем проход рендеринга
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Isolated Layer", out var passData))
            {
                // 5. Создаём настройки отрисовки
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    UniversalForward, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);

                // Добавляем дополнительные шейдерные проходы
                drawingSettings.SetShaderPassName(1, UniversalForwardOnly);
                drawingSettings.SetShaderPassName(2, SRPDefaultUnlit);

                // 6. Настраиваем фильтрацию по слою
                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, _layerMask);

                // 7. Создаём список рендереров
                RendererListParams rendererListParams = new RendererListParams(
                    renderingData.cullResults, drawingSettings, filteringSettings);

                passData.RendererList = renderGraph.CreateRendererList(rendererListParams);

                // 8. Указываем, что рисуем в цвет основной камеры, но используем НАШУ глубину
                builder.UseRendererList(passData.RendererList);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(isolatedDepth, AccessFlags.Write);

                // 9. Выполняем отрисовку
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.RendererList);
                });
            }
        }
    }
}