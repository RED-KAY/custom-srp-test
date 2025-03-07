using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string bufferName = "Render Camera";

    private static ShaderTagId
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    ScriptableRenderContext context;
    Camera camera;
    CommandBuffer buffer = new CommandBuffer { 
        name = bufferName
    };

    CullingResults cullingResults;

    private Lighting lighting = new Lighting();
    
    public void Render(
        ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing
    ){
        this.context = context;
        this.camera = camera;

        PrepareBuffer(); // Editor
        PrepareForSceneWindow(); // Editor
        if (!Cull())
            return;

        Setup();
        lighting.Setup(context, cullingResults);
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        DrawUnsupportedShaders(); // Editor
        DrawGizmos(); // Editor
        Submit();

    }

    bool Cull()
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color, 
            flags == CameraClearFlags.Color ?camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
    {
        //For the Renderer Objects
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings){
			enableDynamicBatching = useDynamicBatching,
			enableInstancing = useGPUInstancing
		};
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        RendererListParams rendererListParams = new RendererListParams { 
            drawSettings = drawingSettings,
            filteringSettings = filteringSettings,
            cullingResults = cullingResults
        };

        //For all the Opaques
        RendererList rendererList = context.CreateRendererList(ref rendererListParams);
        buffer.DrawRendererList(rendererList);

        //For the Skybox
        rendererList = context.CreateSkyboxRendererList(camera);
        buffer.DrawRendererList(rendererList);

        //For Transparent objects
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        rendererListParams.drawSettings = drawingSettings;
        rendererListParams.filteringSettings = filteringSettings;

        rendererList = context.CreateRendererList(ref rendererListParams);
        buffer.DrawRendererList(rendererList);
    }

    private void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }
}