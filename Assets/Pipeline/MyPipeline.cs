using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

public class MyPipeline : RenderPipeline
{
    // Lighting
    const int MAX_VISIBLE_LIGHTS = 16;
    static int lightIndicesOffsetAndCountID = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");
    static int visibleLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    static int visibleLightDirectionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int visibleLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    static int visibleLightSpotDirectionsId = Shader.PropertyToID("_VisibleLightSpotDirections");
    Vector4[] visibleLightColors = new Vector4[MAX_VISIBLE_LIGHTS];
    Vector4[] VisibleLightDirectionsOrPositions = new Vector4[MAX_VISIBLE_LIGHTS];
    Vector4[] visibleLightAttenuations = new Vector4[MAX_VISIBLE_LIGHTS];
    Vector4[] visibleLightSpotDirections = new Vector4[MAX_VISIBLE_LIGHTS];
    bool mainLightExists;

    // shadow
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
	static int shadowDataId = Shader.PropertyToID("_ShadowData");
    static int shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");
    static int cascadedShadowMapSizeId = Shader.PropertyToID("_CascadeShadowMapSize");
    static int cascadedShadowStrengthId = Shader.PropertyToID("_CascadedShadowStrength");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    static int worldToShadowMatricesId = Shader.PropertyToID("_WorldToShadowMatrices");
    static int worldToShadowCascadeMatricesId = Shader.PropertyToID("_WorldToShadowCascadeMatrices");
    static int globalShadowDataId = Shader.PropertyToID("_GlobalShadowData");
    const string shadowsSoftKeyword = "_SHADOWS_SOFT";
    const string shadowsHardKeyword = "_SHADOWS_HARD";
    const string cascadedShadowsSoftKeyword = "_CASCADED_SHADOWS_SOFT";
    const string cascadedShadowsHardKeyword = "_CASCADED_SHADOWS_HARD";
    Vector4[] shadowData = new Vector4[MAX_VISIBLE_LIGHTS];     //x: shadow strength y: soft shadow z: light type flag, 0 for spot 1 for directional
    Matrix4x4[] worldToShadowMatrices = new Matrix4x4[MAX_VISIBLE_LIGHTS];
    Matrix4x4[] worldToShadowCascadeMatrices = new Matrix4x4[5];
    int shadowTileCount;    // shadowmap需要分成多少块，也就是有多少个光源需要shadowmap
    float shadowDistance;
    int shadowCascades;
    Vector3 shadowCascadeSplit;
    Vector4[] cascadeCullingSpheres = new Vector4[4];

    // RT
    RenderTexture shadowMap, cascadedShadowMap;
    int shadowMapSize;
    static int shadowMapId = Shader.PropertyToID("_ShadowMap");
    static int cascadedShadowMapId = Shader.PropertyToID("_CascadedShadowMap");
    static int cameraColorTextureId = Shader.PropertyToID("_CameraColorTexture");
    static int cameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    float renderScale;

    // Command Buffer
    CommandBuffer cameraBuffer = new CommandBuffer
    {
        name = "Render Camera"
    };
    CommandBuffer shadowBuffer = new CommandBuffer
    {
        name = "Render Shadow"
    };
    CommandBuffer postProcessingBuffer = new CommandBuffer
    {
        name = "Post Processing"
    };

    CullResults cull;
    Material errorMaterial;
    MyPostProcessingStack defaultStack;
    DrawRendererFlags drawFlags;

    public MyPipeline  (bool dynamicBatching, bool instancing, 
                        int shadowMapSize, float shadowDistance, 
                        int shadowCascades, Vector3 shadowCascadeSplit,
                        MyPostProcessingStack defaultStack, float renderScale)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        if (SystemInfo.usesReversedZBuffer)
            worldToShadowCascadeMatrices[4].m33 = 1f;

        if (dynamicBatching)
            drawFlags = DrawRendererFlags.EnableDynamicBatching;
        if (instancing)
            drawFlags = DrawRendererFlags.EnableInstancing;
        this.shadowMapSize = shadowMapSize;
        this.shadowDistance = shadowDistance;
        this.shadowCascades = shadowCascades;
        this.shadowCascadeSplit = shadowCascadeSplit;
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
        mainLightExists = false;
        shadowTileCount = 0;

        // 同时最多只有数量为maxVisibleLights的visible lights
        // int i = 0;
        for (int i = 0; i < cull.visibleLights.Count; i++)
        {
            if (i == MAX_VISIBLE_LIGHTS)
                break;

            VisibleLight light = cull.visibleLights[i];
            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1;
            Vector4 shadow = Vector4.zero;

            visibleLightColors[i] = light.finalColor;

            if (light.lightType == LightType.Directional)
            {
                Vector4 v = light.localToWorld.GetColumn(2);
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                VisibleLightDirectionsOrPositions[i] = v;

                shadow = ConfigureShadows(i, light.light);
                shadow.z = 1f;
                if (i == 0 && shadow.x > 0f && shadowCascades > 0)
                {
                    mainLightExists = true;
                    shadowTileCount -= 1;
                }
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

                    shadow = ConfigureShadows(i, light.light);
                }
            }

            visibleLightAttenuations[i] = attenuation;
            shadowData[i] = shadow;
        }
        // 多于MAX_VISIBLE_LIGHTS的灯光置为-1就会被unity跳过
        if (mainLightExists || cull.visibleLights.Count > MAX_VISIBLE_LIGHTS)
        {
            int[] lightIndices = cull.GetLightIndexMap();

            if (mainLightExists)
            {
                lightIndices[0] = -1;
            }
            for (int i = MAX_VISIBLE_LIGHTS; i < cull.visibleLights.Count; i++)
            {
                lightIndices[i] = -1;
            }
            cull.SetLightIndexMap(lightIndices);
        }
    }

    Vector4 ConfigureShadows(int lightIndex, Light shadowLight)
    {
        Vector4 shadow = Vector4.zero;
        Bounds shadowBounds;
        if (shadowLight.shadows != LightShadows.None &&
            cull.GetShadowCasterBounds(lightIndex, out shadowBounds))
        {
            shadowTileCount += 1;
            shadow.x = shadowLight.shadowStrength;
            shadow.y = shadowLight.shadows == LightShadows.Soft ? 1f : 0f;
        }
        return shadow;
    }

    RenderTexture SetShadowRenderTarget()
    {
        RenderTexture shadowMap = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowMap.filterMode = FilterMode.Bilinear;
        shadowMap.wrapMode = TextureWrapMode.Clamp;

        CoreUtils.SetRenderTarget(shadowBuffer, shadowMap, ClearFlag.Depth);
        return shadowMap;
    }

    Vector2 ConfigureShadowTile(int tileIndex, int split, float tileSize)
    {
        Vector2 tileOffset;
        tileOffset.x = tileIndex % split;
        tileOffset.y = tileIndex / split;
        Rect tileViewport = new Rect(tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize);
        shadowBuffer.SetViewport(tileViewport);
        shadowBuffer.EnableScissorRect(new Rect(tileViewport.x + 4f, tileViewport.y + 4f,
                                                tileSize - 8f,       tileSize - 8f));
        return tileOffset;
    }

    void CalculateWorldToShadowMatrix(ref Matrix4x4 viewMatrix, ref Matrix4x4 projectionMatrix, out Matrix4x4 worldToShadowMatrix)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            projectionMatrix.m20 = -projectionMatrix.m20;
            projectionMatrix.m21 = -projectionMatrix.m21;
            projectionMatrix.m22 = -projectionMatrix.m22;
            projectionMatrix.m23 = -projectionMatrix.m23;
        }

        var scaleOffset = Matrix4x4.identity;
        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
        worldToShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);
    }

    void RenderShadows(ScriptableRenderContext context)
    {
        int split;
        if (shadowTileCount <= 1)
            split = 1;
        else if (shadowTileCount <= 4)
            split = 2;
        else if (shadowTileCount <= 9)
            split = 3;
        else
            split = 4;
        float tileSize = shadowMapSize / split;
        float tileScale = 1f / split;

        shadowMap = SetShadowRenderTarget();
        shadowBuffer.BeginSample("Render Shadows");
        shadowBuffer.SetGlobalVector(globalShadowDataId, new Vector4(tileScale, shadowDistance * shadowDistance));
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        bool softShadows = false;
        bool hardShadows = false;
        int tileIndex = 0;
        for (int i = mainLightExists ? 1 : 0; i < cull.visibleLights.Count; i++)
        {
            if (i == MAX_VISIBLE_LIGHTS)
                break;

            if (shadowData[i].x <= 0f)
                continue;

            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;

            bool validShadows = false;
            if (shadowData[i].z > 0f)
                validShadows = cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.right, (int)tileSize, cull.visibleLights[i].light.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);
            else
                validShadows = cull.ComputeSpotShadowMatricesAndCullingPrimitives(i, out viewMatrix, out projectionMatrix, out splitData);
            if (!validShadows)
            {
                shadowData[i].x = 0f;
                continue;
            }

            Vector2 tileOffset = ConfigureShadowTile(tileIndex, split, tileSize);
            shadowData[i].z = tileOffset.x * tileScale;
            shadowData[i].w = tileOffset.x * tileScale;

            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            var shadowSettings = new DrawShadowsSettings(cull, i);
            shadowSettings.splitData.cullingSphere = splitData.cullingSphere;
            context.DrawShadows(ref shadowSettings);

            CalculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out worldToShadowMatrices[i]);

            if (shadowData[i].y <= 0f)
                hardShadows = true;
            else
                softShadows = true;

            shadowBuffer.SetGlobalFloat(shadowBiasId, cull.visibleLights[i].light.shadowBias);

            tileIndex += 1;
        }

        CoreUtils.SetKeyword(shadowBuffer, shadowsHardKeyword, hardShadows);
        CoreUtils.SetKeyword(shadowBuffer, shadowsSoftKeyword, softShadows);

        shadowBuffer.SetGlobalMatrixArray(worldToShadowMatricesId, worldToShadowMatrices);

        shadowBuffer.SetGlobalVectorArray(shadowDataId, shadowData);

        float invShadowMapSize = 1f / shadowMapSize;
        shadowBuffer.SetGlobalVector(shadowMapSizeId, new Vector4(invShadowMapSize, invShadowMapSize, shadowMapSize, shadowMapSize));

        shadowBuffer.DisableScissorRect();

        shadowBuffer.SetGlobalTexture(shadowMapId, shadowMap);

        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }

    void RenderCascadedShadows(ScriptableRenderContext context)
    {
        float tileSize = shadowMapSize / 2;

        cascadedShadowMap = SetShadowRenderTarget();
        shadowBuffer.BeginSample("Render Shadows");
        shadowBuffer.SetGlobalVector(globalShadowDataId, new Vector4(0f, shadowDistance * shadowDistance));
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        Light shadowLight = cull.visibleLights[0].light;
        shadowBuffer.SetGlobalFloat(shadowBiasId, shadowLight.shadowBias);
        var shadowSettings = new DrawShadowsSettings(cull, 0);
        var tileMatrix = Matrix4x4.identity;
        tileMatrix.m00 = tileMatrix.m11 = 0.5f;

        for (int i = 0; i < shadowCascades; i++)
        {
            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;

            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives  (0, i, shadowCascades, shadowCascadeSplit, (int)tileSize, 
                                                                        shadowLight.shadowNearPlane, 
                                                                        out viewMatrix, out projectionMatrix, out splitData);

            Vector2 tileOffset = ConfigureShadowTile(i, 2, tileSize);
            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            cascadeCullingSpheres[i] = 
                shadowSettings.splitData.cullingSphere = splitData.cullingSphere;
            cascadeCullingSpheres[i].w *= splitData.cullingSphere.w;
            context.DrawShadows(ref shadowSettings);
            CalculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out worldToShadowCascadeMatrices[i]);
            tileMatrix.m03 = tileOffset.x * 0.5f;
            tileMatrix.m13 = tileOffset.y * 0.5f;
            worldToShadowCascadeMatrices[i] = tileMatrix * worldToShadowCascadeMatrices[i];
        }

        shadowBuffer.DisableScissorRect();
        shadowBuffer.SetGlobalTexture(cascadedShadowMapId, cascadedShadowMap);
        shadowBuffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        shadowBuffer.SetGlobalMatrixArray(worldToShadowCascadeMatricesId, worldToShadowCascadeMatrices);
        float invShadowMapSize = 1f / shadowMapSize;
        shadowBuffer.SetGlobalVector(cascadedShadowMapSizeId, new Vector4(invShadowMapSize, invShadowMapSize, shadowMapSize, shadowMapSize));
        shadowBuffer.SetGlobalFloat(cascadedShadowStrengthId, shadowLight.shadowStrength);
        bool hard = shadowLight.shadows == LightShadows.Hard;
        CoreUtils.SetKeyword(shadowBuffer, cascadedShadowsHardKeyword, hard);
        CoreUtils.SetKeyword(shadowBuffer, cascadedShadowsSoftKeyword, !hard);
        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }

    void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
    {
        // culling 
        ScriptableCullingParameters cullingParameters;
        if (!CullResults.GetCullingParameters(camera, out cullingParameters))
        {
            return;
        }

        cullingParameters.shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane);

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);  // 使UI在sceneview也可见
#endif

        CullResults.Cull(ref cullingParameters, context, ref cull);

        if (cull.visibleLights.Count > 0)
        {
            ConfigureLights();

            if (mainLightExists)
            {
                RenderCascadedShadows(context);
            }
            else
            {
                cameraBuffer.DisableShaderKeyword(cascadedShadowsHardKeyword);
                cameraBuffer.DisableShaderKeyword(cascadedShadowsSoftKeyword);
            }

            if (shadowTileCount > 0)
            {
                RenderShadows(context);
            }
            else
            {
                cameraBuffer.DisableShaderKeyword(shadowsSoftKeyword);
                cameraBuffer.DisableShaderKeyword(shadowsHardKeyword);
            }
        }
        else
        {
            cameraBuffer.SetGlobalVector(lightIndicesOffsetAndCountID, Vector4.zero);

            cameraBuffer.DisableShaderKeyword(cascadedShadowsHardKeyword);
            cameraBuffer.DisableShaderKeyword(cascadedShadowsSoftKeyword);
            cameraBuffer.DisableShaderKeyword(shadowsSoftKeyword);
            cameraBuffer.DisableShaderKeyword(shadowsHardKeyword);
        }

        context.SetupCameraProperties(camera);  //  传递摄像机属性给上下文，包括unity_MatrixVP矩阵等

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

        cameraBuffer.BeginSample("Render Camera");
        cameraBuffer.SetGlobalVectorArray(visibleLightColorsId, visibleLightColors);
        cameraBuffer.SetGlobalVectorArray(visibleLightDirectionsId, VisibleLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationsId, visibleLightAttenuations);
        cameraBuffer.SetGlobalVectorArray(visibleLightSpotDirectionsId, visibleLightSpotDirections);
        context.ExecuteCommandBuffer(cameraBuffer);     // 执行是拷贝到中间buffer
        cameraBuffer.Clear();

        // 渲染物体
        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"))
        {
            flags = drawFlags,
        };
        // 传递灯光参数
        if (cull.visibleLights.Count > 0)
            drawSettings.rendererConfiguration = RendererConfiguration.PerObjectLightIndices8;

        // 绑定环境贴图
        drawSettings.rendererConfiguration |= RendererConfiguration.PerObjectReflectionProbes;

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

        if (shadowMap)
        {
            RenderTexture.ReleaseTemporary(shadowMap);
            shadowMap = null;
        }
        if (cascadedShadowMap)
        {
            RenderTexture.ReleaseTemporary(cascadedShadowMap);
            cascadedShadowMap = null;
        }
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
