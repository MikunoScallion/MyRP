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

    protected override IRenderPipeline InternalCreatePipeline ()
    {
        return new MyPipeline(dynamicBatching, instancing, defaultStack, renderScale);
    }
}
