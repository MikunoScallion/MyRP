using UnityEngine;

public class InstancedColor : MonoBehaviour
{
    [SerializeField]
    Color color = Color.white;

    static MaterialPropertyBlock propertyBlock;
    static int colorID = Shader.PropertyToID("_Color");     // 用int取比string速度更快

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
        GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }
}
