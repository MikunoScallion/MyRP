using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(menuName = "Rendering/My Pipeline")]
public class MyPipelineAsset : RenderPipelineAsset
{
    [SerializeField, Range(0.25f, 1f)]
    float renderScale = 1f;

    [SerializeField]
    MyPostProcessingStack defaultStack;

    protected override IRenderPipeline InternalCreatePipeline ()
    {
        return new MyPipeline(defaultStack, renderScale);
    }
}
