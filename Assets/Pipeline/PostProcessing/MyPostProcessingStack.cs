using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/My Post-Processing Stack")]
public class MyPostProcessingStack : ScriptableObject
{
    static Mesh fullScreenTriangle;
    static Material material;
    static int mainTexId = Shader.PropertyToID("_MainTex");

    enum Pass
    {
        Copy,
        Blur
    }

    static void InitializeStatic()
    {
        // 初始化fullscreen triangle
        if (fullScreenTriangle)
            return;
        fullScreenTriangle = new Mesh
        {
            name = "My Post-Processing Stack Full-Screen Triangle",
            vertices = new Vector3[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(-1f, 3f, 0f),
                new Vector3(3f, -1f, 0f),
            },
            triangles = new int[] { 0, 1, 2 },
        };
        fullScreenTriangle.UploadMeshData(true);
        // 初始化材质
        material = new Material(Shader.Find("Hidden/My Pipeline/PostEffectStack"))
        {
            name = "My Post-Processing Stack Material",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    public void Render(CommandBuffer cb, int cameraColorId, int cameraDepthId)
    {
        InitializeStatic();

        cb.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

        cb.SetGlobalTexture(mainTexId, cameraColorId);

        // blit时用triangle代替quad的方式，不仅可以省一次draw，而且可以省掉对角线上某些像素的重复绘制
        // cb.Blit(cameraColorId, BuiltinRenderTextureType.CameraTarget);
        cb.DrawMesh(fullScreenTriangle, Matrix4x4.identity, material, 0, (int)Pass.Blur);

    }
}
