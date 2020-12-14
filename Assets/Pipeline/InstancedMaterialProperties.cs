using UnityEngine;

public class InstancedMaterialProperties : MonoBehaviour
{
    [SerializeField]
    Color color = Color.white;

    [SerializeField, Range(0f, 1f)]
    float smoothness = 0.5f;

    [SerializeField, Range(0f, 1f)]
    float metallic = 0f;

    static MaterialPropertyBlock propertyBlock;
    static int colorID = Shader.PropertyToID("_Color");     // 用int取比string速度更快
    static int smoothnessID = Shader.PropertyToID("_Smoothness");
    static int metallicID = Shader.PropertyToID("_Metallic");

    private void Awake()
    {
        OnValidate();
    }

    // 在编辑模式，当组件被加载或改变时被调用
    private void OnValidate()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        propertyBlock.SetColor(colorID, color);
        propertyBlock.SetFloat(smoothnessID, smoothness);
        propertyBlock.SetFloat(metallicID, metallic);
        GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }
}
