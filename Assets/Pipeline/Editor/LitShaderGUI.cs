using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class LitShaderGUI : ShaderGUI
{
    MaterialEditor materialEditor;
    MaterialProperty[] properties;
    Object[] materials;

    CullMode Cull { set { FindProperty("_Cull", properties).floatValue = (float)value; } }
    BlendMode SrcBlend { set { FindProperty("_SrcBlend", properties).floatValue = (float)value; } }
    BlendMode DstBlend { set { FindProperty("_DstBlend", properties).floatValue = (float)value; } }
    bool ZWrite { set { FindProperty("_ZWrite", properties).floatValue = value ? 1 : 0; } }
    enum ClipMode
    {
        Off,
        On,
        Shadows
    }
    ClipMode Clipping
    {
        set
        {
            FindProperty("_Clipping", properties).floatValue = (float)value;
            SetKeywordEnabled("_CLIPPING_OFF", value == ClipMode.Off);
            SetKeywordEnabled("_CLIPPING_ON", value == ClipMode.On);
            SetKeywordEnabled("_CLIPPING_SHADOWS", value == ClipMode.Shadows);
        }
    }
    bool ReceiveShadows
    {
        set
        {
            FindProperty("_ReceiveShadows", properties).floatValue = value ? 1 : 0;
            SetKeywordEnabled("_RECEIVE_SHADOWS", value);
        }
    }
    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    bool showPresets;

    void OpaquePreset()
    {
        if (!GUILayout.Button("Opaque"))
            return;

        materialEditor.RegisterPropertyChangeUndo("Opaque Preset");
        Clipping = ClipMode.Off;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.Geometry;
    }

    void ClipPreset()
    {
        if (!GUILayout.Button("Clip"))
            return;

        materialEditor.RegisterPropertyChangeUndo("Clip Preset");
        Clipping = ClipMode.On;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.AlphaTest;
    }

    void ClipDoublePreset()
    {
        if (!GUILayout.Button("Clip Double-Sided"))
            return;

        materialEditor.RegisterPropertyChangeUndo("Clip Double-Sided Preset");
        Clipping = ClipMode.On;
        Cull = CullMode.Off;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.AlphaTest;
    }

    void FadePreset()
    {
        if (!GUILayout.Button("Fade"))
            return;

        materialEditor.RegisterPropertyChangeUndo("Fade Preset");
        Clipping = ClipMode.On;
        Cull = CullMode.Off;
        SrcBlend = BlendMode.SrcAlpha;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = false;
        SetPassEnabled("ShadowCaster", false);
        RenderQueue = RenderQueue.Transparent;
    }

    void FadeWithShadowsPreset()
    {
        if (!GUILayout.Button("Fade with Shadows"))
            return;

        materialEditor.RegisterPropertyChangeUndo("Fade with Shadows Preset");
        Clipping = ClipMode.Shadows;
        Cull = CullMode.Off;
        SrcBlend = BlendMode.SrcAlpha;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = true;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.Transparent;
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties) 
    {
        base.OnGUI(materialEditor, properties);

        this.materialEditor = materialEditor;
        this.properties = properties;
        materials = materialEditor.targets;

        CastShadowToggle();

        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            ClipDoublePreset();
            FadePreset();
            FadeWithShadowsPreset();
        }
    }

    void SetPassEnabled(string pass, bool enabled)
    {
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled(pass, enabled);
        }
    }

    bool? IsPassEnabled(string pass)
    {
        bool enabled = ((Material)materials[0]).GetShaderPassEnabled(pass);
        for (int i = 1; i < materials.Length; i++)
        {
            if (enabled != ((Material)materials[i]).GetShaderPassEnabled(pass))
            {
                return null;
            }
        }
        return enabled;
    }

    void CastShadowToggle()
    {
        bool? enabled = IsPassEnabled("ShadowCaster");
        if (!enabled.HasValue)
        {
            EditorGUI.showMixedValue = true;
            enabled = false;
        }
        EditorGUI.BeginChangeCheck();
        enabled = EditorGUILayout.Toggle("Cast Shadows", enabled.Value);
        if (EditorGUI.EndChangeCheck())
        {
            materialEditor.RegisterPropertyChangeUndo("Cast Shadows");
            SetPassEnabled("ShadowCaster", enabled.Value);
        }
        EditorGUI.showMixedValue = false;
    }

    void SetKeywordEnabled(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }
}
