using System.Text;
using System.Linq;

public static partial class RendererFeatureGenerator
{
    private static string GenerateRendererFeatureFile(RendererFeatureWizardData data)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.Rendering;");
        sb.AppendLine("using UnityEngine.Rendering.Universal;");
        sb.AppendLine("#if UNITY_EDITOR");
        sb.AppendLine("using UnityEditor;");
        sb.AppendLine("#endif");
        sb.AppendLine();
        sb.AppendLine("[DisallowMultipleRendererFeature]");
        sb.AppendLine($"public class {data.featureName}RendererFeature : ScriptableRendererFeature");
        sb.AppendLine("{");
        sb.AppendLine("    // <gen:pass-fields>");
        foreach (var pass in data.passes)
        {
            sb.AppendLine($"    [SerializeField] private {pass.passName}Settings m_{pass.passName}Settings;");
            sb.AppendLine($"    [SerializeField] private Shader m_{pass.passName}Shader;");
            sb.AppendLine($"    private Material m_{pass.passName}Material;");
            sb.AppendLine($"    private {pass.passName}RenderPass m_{pass.passName}Pass;");
            sb.AppendLine();
        }
        sb.AppendLine("    // </gen:pass-fields>");
        sb.AppendLine();
        sb.AppendLine("    public override void Create()");
        sb.AppendLine("    {");
        sb.AppendLine("        // <gen:create-body>");
        foreach (var pass in data.passes)
        {
            sb.AppendLine($"        if (m_{pass.passName}Shader == null)");
            sb.AppendLine($"            m_{pass.passName}Shader = Shader.Find(\"Custom/{pass.passName}\");");
            sb.AppendLine();
            sb.AppendLine($"        CoreUtils.Destroy(m_{pass.passName}Material);");
            sb.AppendLine($"        m_{pass.passName}Material = m_{pass.passName}Shader != null ? CoreUtils.CreateEngineMaterial(m_{pass.passName}Shader) : null;");
            sb.AppendLine();
            sb.AppendLine($"        m_{pass.passName}Pass = (m_{pass.passName}Material != null && m_{pass.passName}Settings != null)");
            sb.AppendLine($"            ? new {pass.passName}RenderPass(m_{pass.passName}Material, m_{pass.passName}Settings)");
            sb.AppendLine("            : null;");
            sb.AppendLine();
        }
        sb.AppendLine("        // </gen:create-body>");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
        sb.AppendLine("    {");
        sb.AppendLine("        // <gen:addrendererpasses-body>");
        foreach (var pass in data.passes)
        {
            sb.AppendLine($"        if (m_{pass.passName}Pass != null)");
            sb.AppendLine($"            renderer.EnqueuePass(m_{pass.passName}Pass);");
            sb.AppendLine();
        }
        sb.AppendLine("        // </gen:addrendererpasses-body>");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)");
        sb.AppendLine("    {");
        sb.AppendLine("        // <gen:setuprenderpasses-body>");
        sb.AppendLine("        // Intentionally empty for the boilerplate.");
        sb.AppendLine("        // </gen:setuprenderpasses-body>");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected override void Dispose(bool disposing)");
        sb.AppendLine("    {");
        sb.AppendLine("        // <gen:dispose-body>");
        foreach (var pass in data.passes)
        {
            sb.AppendLine($"        CoreUtils.Destroy(m_{pass.passName}Material);");
            sb.AppendLine($"        m_{pass.passName}Material = null;");
            sb.AppendLine($"        m_{pass.passName}Pass = null;");
            sb.AppendLine();
        }
        sb.AppendLine("        // </gen:dispose-body>");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("#if UNITY_EDITOR");
        sb.AppendLine("    private void OnEnable()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var root = \"{GetOutputDirectory(data.featureName)}\";");
        sb.AppendLine("        var anyChange = false;");
        sb.AppendLine();
        foreach (var pass in data.passes)
        {
            sb.AppendLine($"        if (m_{pass.passName}Settings == null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var settingsPath = $\"{root}/{pass.passName}Settings.asset\";");
            sb.AppendLine($"            m_{pass.passName}Settings = AssetDatabase.LoadAssetAtPath<{pass.passName}Settings>(settingsPath);");
            sb.AppendLine($"            if (m_{pass.passName}Settings == null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                m_{pass.passName}Settings = ScriptableObject.CreateInstance<{pass.passName}Settings>();");
            sb.AppendLine($"                AssetDatabase.CreateAsset(m_{pass.passName}Settings, settingsPath);");
            sb.AppendLine("            }");
            sb.AppendLine("            anyChange = true;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        if (m_{pass.passName}Shader == null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var shaderPath = $\"{root}/{pass.passName}.shader\";");
            sb.AppendLine($"            m_{pass.passName}Shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);");
            sb.AppendLine($"            anyChange = anyChange || (m_{pass.passName}Shader != null);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        sb.AppendLine("        if (anyChange)");
        sb.AppendLine("        {");
        sb.AppendLine("            EditorUtility.SetDirty(this);");
        sb.AppendLine("            AssetDatabase.SaveAssets();");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("#endif");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateRenderPassFile(RendererFeatureWizardData.PassConfig pass)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.Rendering;");
        sb.AppendLine("using UnityEngine.Rendering.RenderGraphModule;");
        sb.AppendLine("using UnityEngine.Rendering.Universal;");
        sb.AppendLine();
        sb.AppendLine($"public class {pass.passName}RenderPass : ScriptableRenderPass");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly Vector4 k_ScaleBias = new Vector4(1f, 1f, 0f, 0f);");
        sb.AppendLine();
        sb.AppendLine("    private Material m_Material;");
        sb.AppendLine($"    private {pass.passName}Settings m_Settings;");
        sb.AppendLine();
        sb.AppendLine("    // <gen:pass-data-class>");
        sb.AppendLine("    private class PassData");
        sb.AppendLine("    {");
        sb.AppendLine("        public TextureHandle source;");
        sb.AppendLine("        public TextureHandle destination;");
        sb.AppendLine("        public Material material;");
        foreach (var prop in pass.properties)
            sb.AppendLine($"        public {GetCSharpTypeName(prop.type)} {prop.name};");
        sb.AppendLine("    }");
        sb.AppendLine("    // </gen:pass-data-class>");
        sb.AppendLine();
        sb.AppendLine($"    public {pass.passName}RenderPass(Material material, {pass.passName}Settings settings)");
        sb.AppendLine("    {");
        sb.AppendLine("        m_Material = material;");
        sb.AppendLine("        m_Settings = settings;");
        sb.AppendLine($"        renderPassEvent = RenderPassEvent.{pass.renderPassEvent};");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (m_Material == null || m_Settings == null)");
        sb.AppendLine("            return;");
        sb.AppendLine();
        sb.AppendLine("        var resourcesData = frameData.Get<UniversalResourceData>();");
        sb.AppendLine();
        sb.AppendLine($"        using (var builder = renderGraph.AddRasterRenderPass<PassData>(\"{pass.passName}\", out var passData))");
        sb.AppendLine("        {");
        sb.AppendLine("            // <gen:record-body>");
        sb.AppendLine("            passData.material = m_Material;");
        sb.AppendLine("            passData.source = resourcesData.cameraColor;");
        sb.AppendLine();
        sb.AppendLine("            var descriptor = passData.source.GetDescriptor(renderGraph);");
        sb.AppendLine("            descriptor.msaaSamples = MSAASamples.None;");
        sb.AppendLine($"            descriptor.name = \"{pass.passName}_Color\";");
        sb.AppendLine("            descriptor.clearBuffer = false;");
        sb.AppendLine();
        sb.AppendLine("            passData.destination = renderGraph.CreateTexture(descriptor);");
        sb.AppendLine();
        foreach (var prop in pass.properties)
            sb.AppendLine($"            passData.{prop.name} = m_Settings.{ToPascal(prop.name)};");
        sb.AppendLine();
        sb.AppendLine("            builder.UseTexture(passData.source);");
        sb.AppendLine("            builder.SetRenderAttachment(passData.destination, 0);");
        sb.AppendLine();
        sb.AppendLine("            resourcesData.cameraColor = passData.destination;");
        sb.AppendLine("            // </gen:record-body>");
        sb.AppendLine();
        sb.AppendLine("            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>");
        sb.AppendLine("            {");
        sb.AppendLine("                ExecutePass(data, context);");
        sb.AppendLine("            });");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void ExecutePass(PassData data, RasterGraphContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        // <gen:execute-body>");
        foreach (var prop in pass.properties)
        {
            var shaderName = ToShaderPropertyName(prop.name);
            sb.AppendLine($"        data.material.{GetMaterialSetter(prop.type)}(\"{shaderName}\", data.{prop.name});");
        }
        sb.AppendLine();
        sb.AppendLine("        Blitter.BlitTexture(context.cmd, data.source, k_ScaleBias, data.material, 0);");
        sb.AppendLine("        // </gen:execute-body>");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateSettingsFile(RendererFeatureWizardData.PassConfig pass)
    {
        var sb = new StringBuilder(2048);
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine($"[CreateAssetMenu(fileName = \"{pass.passName}Settings\", menuName = \"Rendering/{pass.passName}Settings\")]");
        sb.AppendLine($"public class {pass.passName}Settings : ScriptableObject");
        sb.AppendLine("{");
        sb.AppendLine("    // <gen:so-properties>");
        foreach (var prop in pass.properties)
        {
            var csType = GetCSharpTypeName(prop.type);
            var def = GetCSharpDefaultLiteral(prop);
            if (!string.IsNullOrEmpty(def))
                sb.AppendLine($"    [SerializeField] private {csType} {prop.name} = {def};");
            else
                sb.AppendLine($"    [SerializeField] private {csType} {prop.name};");

            sb.AppendLine($"    public {csType} {ToPascal(prop.name)} => {prop.name};");
            sb.AppendLine();
        }
        sb.AppendLine("    // </gen:so-properties>");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateShaderFile(RendererFeatureWizardData.PassConfig pass)
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine($"Shader \"Custom/{pass.passName}\"");
        sb.AppendLine("{");
        sb.AppendLine("    Properties");
        sb.AppendLine("    {");
        sb.AppendLine("        // <gen:shader-properties>");
        foreach (var prop in pass.properties)
            sb.AppendLine($"        {GetShaderLabPropertyLine(prop)}");
        sb.AppendLine("        // </gen:shader-properties>");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    SubShader");
        sb.AppendLine("    {");
        sb.AppendLine("        Tags { \"RenderType\" = \"Opaque\" \"RenderPipeline\" = \"UniversalPipeline\" }");
        sb.AppendLine();
        sb.AppendLine("        HLSLINCLUDE");
        sb.AppendLine("        #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\"");
        sb.AppendLine("        // <gen:hlsl-includes-end>");
        sb.AppendLine();
        sb.AppendLine("        cbuffer UnityPerMaterial");
        sb.AppendLine("        {");
        sb.AppendLine("            // <gen:cbuffer-properties>");
        foreach (var prop in pass.properties.Where(p => p.type != PropertyType.Texture2D))
            sb.AppendLine($"            {GetHlslCbufferLine(prop)}");
        sb.AppendLine("            // </gen:cbuffer-properties>");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        // <gen:texture-declarations>");
        foreach (var prop in pass.properties.Where(p => p.type == PropertyType.Texture2D))
        {
            var name = ToShaderPropertyName(prop.name);
            sb.AppendLine($"        TEXTURE2D({name});");
            sb.AppendLine($"        SAMPLER(sampler{name});");
            sb.AppendLine();
        }
        sb.AppendLine("        // </gen:texture-declarations>");
        sb.AppendLine("        ENDHLSL");
        sb.AppendLine();
        sb.AppendLine("        Pass");
        sb.AppendLine("        {");
        sb.AppendLine($"            Name \"{pass.passName}\"");
        sb.AppendLine("            Tags { \"LightMode\" = \"UniversalForward\" }");
        sb.AppendLine();
        sb.AppendLine("            HLSLPROGRAM");
        sb.AppendLine("            #pragma vertex vert");
        sb.AppendLine("            #pragma fragment frag");
        sb.AppendLine();
        sb.AppendLine("            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };");
        sb.AppendLine("            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };");
        sb.AppendLine();
        sb.AppendLine("            Varyings vert(Attributes IN)");
        sb.AppendLine("            {");
        sb.AppendLine("                Varyings OUT;");
        sb.AppendLine("                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);");
        sb.AppendLine("                OUT.uv = IN.uv;");
        sb.AppendLine("                return OUT;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            half4 frag(Varyings IN) : SV_Target");
        sb.AppendLine("            {");
        sb.AppendLine("                return half4(1, 1, 1, 1);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            ENDHLSL");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
