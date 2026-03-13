using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static partial class RendererFeatureGenerator
{
    private static void ValidateWizardDataOrThrow(RendererFeatureWizardData data)
    {
        if (!IsValidIdentifier(data.featureName, requirePascalCase: true))
            throw new InvalidOperationException($"Invalid feature name: '{data.featureName}'");

        if (data.passes == null || data.passes.Count <= 0)
            throw new InvalidOperationException("No passes configured.");

        var passNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pass in data.passes)
        {
            if (!IsValidIdentifier(pass.passName, requirePascalCase: true))
                throw new InvalidOperationException($"Invalid pass name: '{pass.passName}'");

            if (!passNames.Add(pass.passName))
                throw new InvalidOperationException($"Duplicate pass name: '{pass.passName}'");

            var propNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in pass.properties)
            {
                if (!IsValidIdentifier(prop.name, requirePascalCase: false))
                    throw new InvalidOperationException($"Invalid property name: '{prop.name}' in pass '{pass.passName}'");

                if (IsReservedHlslWord(prop.name))
                    throw new InvalidOperationException($"Reserved HLSL keyword: '{prop.name}' in pass '{pass.passName}'");

                if (!propNames.Add(prop.name))
                    throw new InvalidOperationException($"Duplicate property name: '{prop.name}' in pass '{pass.passName}'");
            }
        }
    }

    private static bool IsReservedHlslWord(string name)
    {
        // Minimal set; expand as needed.
        switch (name)
        {
            case "cbuffer":
            case "struct":
            case "Texture2D":
            case "SamplerState":
            case "sampler":
            case "float":
            case "int":
            case "half":
            case "bool":
            case "return":
            case "if":
            case "else":
            case "for":
            case "while":
            case "do":
            case "break":
            case "continue":
                return true;
            default:
                return false;
        }
    }

    private static bool IsValidIdentifier(string text, bool requirePascalCase)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!Regex.IsMatch(text, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            return false;

        if (requirePascalCase && !char.IsUpper(text[0]))
            return false;

        return true;
    }

    private static string GetCSharpTypeName(PropertyType type)
    {
        switch (type)
        {
            case PropertyType.Float: return "float";
            case PropertyType.Int: return "int";
            case PropertyType.Vector4: return "Vector4";
            case PropertyType.Color: return "Color";
            case PropertyType.Texture2D: return "Texture2D";
            default: return "float";
        }
    }

    private static string GetMaterialSetter(PropertyType type)
    {
        switch (type)
        {
            case PropertyType.Float: return "SetFloat";
            case PropertyType.Int: return "SetInteger";
            case PropertyType.Vector4: return "SetVector";
            case PropertyType.Color: return "SetColor";
            case PropertyType.Texture2D: return "SetTexture";
            default: return "SetFloat";
        }
    }

    private static string GetCSharpDefaultLiteral(RendererFeatureWizardData.PropertyConfig prop)
    {
        var raw = (prop.defaultValue ?? "").Trim();

        switch (prop.type)
        {
            case PropertyType.Float:
                if (TryParseSingle(raw, out var f))
                    return f.ToString("0.###", CultureInfo.InvariantCulture) + "f";
                return "0f";
            case PropertyType.Int:
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return i.ToString(CultureInfo.InvariantCulture);
                return "0";
            case PropertyType.Vector4:
                if (TryParseVector4(raw, out var v))
                    return $"new Vector4({Fmt(v.x)}f, {Fmt(v.y)}f, {Fmt(v.z)}f, {Fmt(v.w)}f)";
                return "new Vector4(0f, 0f, 0f, 0f)";
            case PropertyType.Color:
                if (TryParseVector4(raw, out var c))
                    return $"new Color({Fmt(c.x)}f, {Fmt(c.y)}f, {Fmt(c.z)}f, {Fmt(c.w)}f)";
                return "new Color(1f, 1f, 1f, 1f)";
            case PropertyType.Texture2D:
                return "";
            default:
                return "";
        }
    }

    private static bool TryParseSingle(string raw, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseVector4(string raw, out Vector4 value)
    {
        value = Vector4.zero;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Trim();
        raw = raw.Trim('(', ')');
        var parts = raw.Split(',');
        if (parts.Length != 4)
            return false;

        if (!TryParseSingle(parts[0].Trim(), out var x)) return false;
        if (!TryParseSingle(parts[1].Trim(), out var y)) return false;
        if (!TryParseSingle(parts[2].Trim(), out var z)) return false;
        if (!TryParseSingle(parts[3].Trim(), out var w)) return false;

        value = new Vector4(x, y, z, w);
        return true;
    }

    private static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetShaderLabPropertyLine(RendererFeatureWizardData.PropertyConfig prop)
    {
        var shaderName = ToShaderPropertyName(prop.name);
        var displayName = ToDisplayName(prop.name);

        switch (prop.type)
        {
            case PropertyType.Float:
                return $"{shaderName} (\"{displayName}\", Float) = {GetShaderDefaultNumber(prop.defaultValue, 0f)}";
            case PropertyType.Int:
                return $"{shaderName} (\"{displayName}\", Int) = {GetShaderDefaultInt(prop.defaultValue, 0)}";
            case PropertyType.Vector4:
                return $"{shaderName} (\"{displayName}\", Vector) = {GetShaderDefaultVector4(prop.defaultValue, new Vector4(0f, 0f, 0f, 0f))}";
            case PropertyType.Color:
                return $"{shaderName} (\"{displayName}\", Color) = {GetShaderDefaultVector4(prop.defaultValue, new Vector4(1f, 1f, 1f, 1f))}";
            case PropertyType.Texture2D:
                return $"{shaderName} (\"{displayName}\", 2D) = \"white\" {{}}";
            default:
                return $"{shaderName} (\"{displayName}\", Float) = 0";
        }
    }

    private static string GetHlslCbufferLine(RendererFeatureWizardData.PropertyConfig prop)
    {
        var shaderName = ToShaderPropertyName(prop.name);
        switch (prop.type)
        {
            case PropertyType.Float:
                return $"float {shaderName};";
            case PropertyType.Int:
                return $"int {shaderName};";
            case PropertyType.Vector4:
            case PropertyType.Color:
                return $"float4 {shaderName};";
            default:
                return $"float {shaderName};";
        }
    }

    private static string GetShaderDefaultNumber(string raw, float fallback)
    {
        if (TryParseSingle((raw ?? "").Trim(), out var v))
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        return fallback.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string GetShaderDefaultInt(string raw, int fallback)
    {
        if (int.TryParse((raw ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return v.ToString(CultureInfo.InvariantCulture);
        return fallback.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetShaderDefaultVector4(string raw, Vector4 fallback)
    {
        if (TryParseVector4((raw ?? "").Trim(), out var v))
            return $"({Fmt(v.x)}, {Fmt(v.y)}, {Fmt(v.z)}, {Fmt(v.w)})";

        return $"({Fmt(fallback.x)}, {Fmt(fallback.y)}, {Fmt(fallback.z)}, {Fmt(fallback.w)})";
    }

    private static string ToPascal(string camel)
    {
        if (string.IsNullOrEmpty(camel))
            return camel;
        if (camel.Length == 1)
            return camel.ToUpperInvariant();
        return char.ToUpperInvariant(camel[0]) + camel.Substring(1);
    }

    private static string ToShaderPropertyName(string camel)
    {
        // Auto-prefix with '_' and convert to PascalCase (simple leading-char conversion).
        return "_" + ToPascal(camel);
    }

    private static string ToDisplayName(string camel)
    {
        if (string.IsNullOrEmpty(camel))
            return camel;

        var sb = new StringBuilder(camel.Length + 8);
        sb.Append(char.ToUpperInvariant(camel[0]));
        for (int i = 1; i < camel.Length; i++)
        {
            var c = camel[i];
            if (char.IsUpper(c) && char.IsLetterOrDigit(camel[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}

