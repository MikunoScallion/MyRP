using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(menuName = "Rendering/My Pipeline")]
public class MyPipelineAsset : RenderPipelineAsset
{
    // 渲染尺寸
    [SerializeField, Range(0.25f, 1f)]
    float renderScale = 1f;

    // 自定义是否开启动态批处理
    [SerializeField]
    bool dynamicBatching;

    // 自定义是否开启GPU Instancing
    [SerializeField]
    bool instancing;

    // 自定义后处理
    [SerializeField]
    MyPostProcessingStack defaultStack;

    //阴影相关
    public enum ShadowMapSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }
    [SerializeField]
    ShadowMapSize shadowMapSize = ShadowMapSize._1024;

    [SerializeField]
    float shadowDistance = 100f;

    public enum ShadowCascades
    {
        _0 = 0,
        _2 = 2,
        _4 = 4
    }
    [SerializeField]
    ShadowCascades shadowCascades = ShadowCascades._4;

    [SerializeField, HideInInspector]
    float twoCascadesSplit = 0.25f;

    [SerializeField, HideInInspector]
    Vector3 fourCascadesSplit = new Vector3(0.067f, 0.2f, 0.467f);

    protected override IRenderPipeline InternalCreatePipeline()
    {
        Vector3 shadowCascadeSplit = shadowCascades == ShadowCascades._4 ? fourCascadesSplit : new Vector3(twoCascadesSplit, 0f);
        return new MyPipeline  (dynamicBatching, instancing, 
                                (int) shadowMapSize, shadowDistance, 
                                (int) shadowCascades, shadowCascadeSplit, 
                                defaultStack, renderScale);
    }
}
