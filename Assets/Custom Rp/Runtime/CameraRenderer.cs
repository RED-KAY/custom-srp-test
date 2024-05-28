using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string bufferName = "Render Camera";
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

    ScriptableRenderContext context;
    Camera camera;
    CommandBuffer buffer = new CommandBuffer { 
        name = bufferName
    };

    CullingResults cullingResults;

    public void Render(
        ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing
    ){
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull())
            return;

        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        DrawUnsupportedShaders();
        DrawGizmos();
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
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        RendererListParams rendererListParams = new RendererListParams { 
            drawSettings = drawingSettings,
            filteringSettings = filteringSettings,
            cullingResults = cullingResults
        };

        
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