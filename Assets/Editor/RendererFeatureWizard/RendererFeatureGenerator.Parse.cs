using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static partial class RendererFeatureGenerator
{
    private static List<RendererFeatureWizardData.PropertyConfig> ParsePropertiesFromSettingsBlock(string soBlock)
    {
        var props = new List<RendererFeatureWizardData.PropertyConfig>();
        var lines = soBlock.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var fieldRegex = new Regex(@"\[SerializeField\]\s*private\s+(?<type>\w+)\s+(?<name>\w+)(\s*=\s*(?<def>[^;]+))?;",
            RegexOptions.Compiled);

        foreach (var line in lines)
        {
            var m = fieldRegex.Match(line);
            if (!m.Success)
                continue;

            var typeName = m.Groups["type"].Value;
            var name = m.Groups["name"].Value;
            var def = m.Groups["def"].Success ? m.Groups["def"].Value.Trim() : "";

            if (!TryMapCSharpType(typeName, out var propType))
                continue;

            props.Add(new RendererFeatureWizardData.PropertyConfig
            {
                selected = false,
                type = propType,
                name = name,
                defaultValue = CSharpDefaultToUiString(propType, def),
            });
        }

        return props;
    }

    private static bool TryMapCSharpType(string typeName, out PropertyType propType)
    {
        propType = PropertyType.Float;
        switch (typeName)
        {
            case "float":
                propType = PropertyType.Float;
                return true;
            case "int":
                propType = PropertyType.Int;
                return true;
            case "Vector4":
                propType = PropertyType.Vector4;
                return true;
            case "Color":
                propType = PropertyType.Color;
                return true;
            case "Texture2D":
                propType = PropertyType.Texture2D;
                return true;
            default:
                return false;
        }
    }

    private static string CSharpDefaultToUiString(PropertyType type, string def)
    {
        if (string.IsNullOrWhiteSpace(def))
            return "";

        def = def.Trim();

        switch (type)
        {
            case PropertyType.Float:
                return def.TrimEnd('f', 'F');
            case PropertyType.Int:
                return def;
            case PropertyType.Vector4:
                return ParseCtorArgs(def, "Vector4");
            case PropertyType.Color:
                return ParseCtorArgs(def, "Color");
            default:
                return "";
        }
    }

    private static string ParseCtorArgs(string def, string ctorName)
    {
        // Expected: new Vector4(1f, 2f, 3f, 4f)
        var m = Regex.Match(def, @"new\s+" + Regex.Escape(ctorName) + @"\s*\(\s*([^)]+)\s*\)");
        if (!m.Success)
            return "";

        var args = m.Groups[1].Value.Split(',')
            .Select(x => x.Trim().TrimEnd('f', 'F'))
            .ToArray();

        return string.Join(", ", args);
    }
}

