using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

public class MyPipeline : RenderPipeline
{
    // Lighting
    const int maxVisibleLights = 4;
    static int visibleLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    static int visibleLightDirectionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int visibleLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    static int visibleLightSpotDirectionsId = Shader.PropertyToID("_VisibleLightSpotDirections");
    Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    Vector4[] VisibleLightDirectionsOrPositions = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    Vector4[] visibleLightSpotDirections = new Vector4[maxVisibleLights];

    // RT
    static int cameraColorTextureId = Shader.PropertyToID("_CameraColorTexture");
    static int cameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    float renderScale;

    // Command Buffer
    CommandBuffer cameraBuffer = new CommandBuffer
    {
        name = "Render Camera"
    };
    CommandBuffer postProcessingBuffer = new CommandBuffer
    {
        name = "Post Processing"
    };

    CullResults cull;
    Material errorMaterial;
    MyPostProcessingStack defaultStack;
    DrawRendererFlags drawFlags;

    public MyPipeline(bool dynamicBatching, bool instancing, MyPostProcessingStack defaultStack, float renderScale)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        if (dynamicBatching)
            drawFlags = DrawRendererFlags.EnableDynamicBatching;
        if (instancing)
            drawFlags = DrawRendererFlags.EnableInstancing;
        this.defaultStack = defaultStack;
        this.renderScale = renderScale;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);

        foreach (var camera in cameras)
        {
            // var myPipelineCamera = camera.GetComponent<MyPipelineCamera>();

            bool scaledRendering = renderScale < 1f && camera.cameraType == CameraType.Game;

            int renderWidth = camera.pixelWidth;
            int renderHeight = camera.pixelHeight;
            if (scaledRendering)
            {
                renderWidth = (int)(renderWidth * renderScale);
                renderHeight = (int)(renderHeight * renderScale);
            }

            RenderSingleCamera(renderContext, camera);
        }
    }

    void ConfigureLights()
    {
        // 同时最多只有数量为maxVisibleLights的visible lights
        int i = 0;
        for (; i < cull.visibleLights.Count; i++)
        {
            if (i == maxVisibleLights)
                break;

            VisibleLight light = cull.visibleLights[i];
            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1; 

            visibleLightColors[i] = light.finalColor;

            if (light.lightType == LightType.Directional)
            {
                Vector4 v = light.localToWorld.GetColumn(2);
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                VisibleLightDirectionsOrPositions[i] = v;
            }
            else
            {
                VisibleLightDirectionsOrPositions[i] = light.localToWorld.GetColumn(3);
                attenuation.x = 1f / Mathf.Max(light.range * light.range, 0.00001f);

                if (light.lightType == LightType.Spot)
                {
                    Vector4 v = light.localToWorld.GetColumn(2);
                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;
                    visibleLightSpotDirections[i] = v;

                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos = Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;
                }
            }

            visibleLightAttenuations[i] = attenuation;
        }
        for (; i < maxVisibleLights; i++)
        {
            visibleLightColors[i] = Color.clear;
        }
    }

    void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
    {
        context.SetupCameraProperties(camera);  //  传递摄像机属性给上下文，包括unity_MatrixVP矩阵等

        // culling 
        ScriptableCullingParameters cullingParameters;
        if (!CullResults.GetCullingParameters(camera, out cullingParameters))
        {
            return;
        }
#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);  // 使UI在sceneview也可见
        }
#endif
        CullResults.Cull(ref cullingParameters, context, ref cull);

        // 该相机的commandbuffer
        if (defaultStack)
        {
            // 将color和depth渲染到自定义的_CameraColorTexture和_CameraDepthTexture RT上
            cameraBuffer.GetTemporaryRT(cameraColorTextureId, camera.pixelWidth, camera.pixelHeight, 0);
            cameraBuffer.GetTemporaryRT(cameraDepthTextureId, camera.pixelWidth, camera.pixelHeight, 24,
                                        FilterMode.Point, RenderTextureFormat.Depth);
            cameraBuffer.SetRenderTarget(cameraColorTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                         cameraDepthTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        CameraClearFlags clearFlags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0,
                                       (clearFlags & CameraClearFlags.Color) != 0,
                                        camera.backgroundColor);

        ConfigureLights();

        cameraBuffer.BeginSample("Render Camera");
        cameraBuffer.SetGlobalVectorArray(visibleLightColorsId, visibleLightColors);
        cameraBuffer.SetGlobalVectorArray(visibleLightDirectionsId, VisibleLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationsId, visibleLightAttenuations);
        cameraBuffer.SetGlobalVectorArray(visibleLightSpotDirectionsId, visibleLightSpotDirections);
        context.ExecuteCommandBuffer(cameraBuffer);     // 执行是拷贝到中间buffer
        cameraBuffer.Clear();

        // 渲染物体
        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"));
        // 动态批处理
        drawSettings.flags = drawFlags;
        // 不透明物体
        drawSettings.sorting.flags = SortFlags.CommonOpaque;    // 不透明物体从近到远排序
        var filterSettings = new FilterRenderersSettings(true)
        {
            renderQueueRange = RenderQueueRange.opaque      // 不透明物体 0-2500
        };
        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

        // skybox
        context.DrawSkybox(camera);

        // 渲染透明物体
        drawSettings.sorting.flags = SortFlags.CommonOpaque;    // 透明物体从远到近排序
        filterSettings.renderQueueRange = RenderQueueRange.transparent;     // 透明物体 2501-5000
        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

        DrawDefaultPipeline(context, camera);   // 渲染材质shader不被管线支持的物体

        // 后处理
        if (defaultStack)
        {
            defaultStack.Render(postProcessingBuffer, cameraColorTextureId, cameraDepthTextureId);
            context.ExecuteCommandBuffer(postProcessingBuffer);
            postProcessingBuffer.Clear();
            cameraBuffer.ReleaseTemporaryRT(cameraColorTextureId);
            cameraBuffer.ReleaseTemporaryRT(cameraDepthTextureId);
        }

        cameraBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        context.Submit();   // 需要通过submit把缓冲拿出来
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]     // 仅在development build和editor中显示错误材质
    void DrawDefaultPipeline(ScriptableRenderContext context, Camera camera)
    {
        if (errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
        drawSettings.SetOverrideMaterial(errorMaterial, 0);
        var filterSettings = new FilterRenderersSettings(true);
        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    }
}
