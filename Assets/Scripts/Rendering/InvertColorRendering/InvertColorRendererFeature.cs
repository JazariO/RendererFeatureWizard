using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleRendererFeature]
public class InvertColorRendererFeature : ScriptableRendererFeature
{
    // <gen:pass-fields>
    [SerializeField] private InvertColorSettings m_InvertColorSettings;
    [SerializeField] private Shader m_InvertColorShader;
    [SerializeField] private RenderPassEvent m_InvertColorRenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    [SerializeField] private int m_InvertColorPassEventOffset = 0;
    private Material m_InvertColorMaterial;
    private InvertColorRenderPass m_InvertColorPass;

    // </gen:pass-fields>

    public override void Create()
    {
        // <gen:create-body>
        if (m_InvertColorShader == null)
            m_InvertColorShader = Shader.Find("Custom/InvertColor");

        CoreUtils.Destroy(m_InvertColorMaterial);
        m_InvertColorMaterial = m_InvertColorShader != null ? CoreUtils.CreateEngineMaterial(m_InvertColorShader) : null;

        m_InvertColorPass = (m_InvertColorMaterial != null && m_InvertColorSettings != null)
            ? new InvertColorRenderPass(m_InvertColorMaterial, m_InvertColorSettings)
            : null;

        if (m_InvertColorPass != null)
            m_InvertColorPass.renderPassEvent = (RenderPassEvent)((int)m_InvertColorRenderPassEvent + m_InvertColorPassEventOffset);

        // </gen:create-body>
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // <gen:addrendererpasses-body>
        if (m_InvertColorPass != null)
            renderer.EnqueuePass(m_InvertColorPass);

        // </gen:addrendererpasses-body>
    }

    protected override void Dispose(bool disposing)
    {
        // <gen:dispose-body>
        CoreUtils.Destroy(m_InvertColorMaterial);
        m_InvertColorMaterial = null;
        m_InvertColorPass = null;

        // </gen:dispose-body>
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        var root = "Assets/Scripts/Rendering/InvertColorRendering";
        var anyChange = false;

        if (m_InvertColorSettings == null)
        {
            var settingsPath = $"{root}/InvertColorSettings.asset";
            m_InvertColorSettings = AssetDatabase.LoadAssetAtPath<InvertColorSettings>(settingsPath);
            if (m_InvertColorSettings == null)
            {
                m_InvertColorSettings = ScriptableObject.CreateInstance<InvertColorSettings>();
                AssetDatabase.CreateAsset(m_InvertColorSettings, settingsPath);
            }
            anyChange = true;
        }

        if (m_InvertColorShader == null)
        {
            var shaderPath = $"{root}/InvertColor.shader";
            m_InvertColorShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            anyChange = anyChange || (m_InvertColorShader != null);
        }

        if (anyChange)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
#endif
}

// <gen:pass-classes>
public class InvertColorRenderPass : ScriptableRenderPass
{
    private static readonly Vector4 k_ScaleBias = new Vector4(1f, 1f, 0f, 0f);

    private Material m_Material;
    private InvertColorSettings m_Settings;

    // <gen:pass-data-class>
    private class PassData
    {
        public TextureHandle source;
        public TextureHandle destination;
        public Material material;
        public float intensity;
    }
    // </gen:pass-data-class>

    public InvertColorRenderPass(Material material, InvertColorSettings settings)
    {
        m_Material = material;
        m_Settings = settings;
        requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (m_Material == null || m_Settings == null)
            return;

        var resourcesData = frameData.Get<UniversalResourceData>();

        using (var builder = renderGraph.AddRasterRenderPass<PassData>("InvertColor", out var passData))
        {
            // <gen:record-body>
            passData.material = m_Material;
            passData.source = resourcesData.activeColorTexture;

            var descriptor = passData.source.GetDescriptor(renderGraph);
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.name = "InvertColor_Color";
            descriptor.clearBuffer = false;

            passData.destination = renderGraph.CreateTexture(descriptor);

            passData.intensity = m_Settings.Intensity;

            builder.UseTexture(passData.source, AccessFlags.Read);
            builder.SetRenderAttachment(passData.destination, 0, AccessFlags.Write);

            resourcesData.cameraColor = passData.destination;
            // </gen:record-body>

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                ExecutePass(data, context);
            });
        }
    }

    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        // <gen:execute-body>
        data.material.SetFloat("_Intensity", data.intensity);

        Blitter.BlitTexture(context.cmd, data.source, k_ScaleBias, data.material, 0);
        // </gen:execute-body>
    }
}


// </gen:pass-classes>
