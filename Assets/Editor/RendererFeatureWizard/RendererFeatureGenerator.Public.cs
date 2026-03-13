using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static partial class RendererFeatureGenerator
{
    private const string ScriptsRoot = "Assets/Scripts/Rendering";

    public static string GetOutputDirectory(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            featureName = "NewFeature";

        return $"{ScriptsRoot}/{featureName}Rendering";
    }

    public static bool HasReentrySentinels(string featureName)
    {
        var outDir = GetOutputDirectory(featureName);
        if (!Directory.Exists(outDir))
            return false;

        var featureFile = Path.Combine(outDir, $"{featureName}RendererFeature.cs");
        if (!File.Exists(featureFile))
            return false;

        var text = File.ReadAllText(featureFile);
        return text.Contains("// <gen:", StringComparison.Ordinal);
    }

    public static bool TryLoadExisting(string featureName, out RendererFeatureWizardData data, out string error)
    {
        data = new RendererFeatureWizardData();
        error = null;

        var outDir = GetOutputDirectory(featureName);
        if (!Directory.Exists(outDir))
        {
            error = $"Directory does not exist: {outDir}";
            return false;
        }

        var settingsFiles = Directory.GetFiles(outDir, "*Settings.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (settingsFiles.Length == 0)
        {
            error = $"No Settings files found in: {outDir}";
            return false;
        }

        data.featureName = featureName;
        data.desiredPassCount = settingsFiles.Length;
        data.passes.Clear();

        foreach (var settingsPath in settingsFiles)
        {
            var fileName = Path.GetFileName(settingsPath);
            var passName = fileName.Substring(0, fileName.Length - "Settings.cs".Length);

            var passConfig = new RendererFeatureWizardData.PassConfig
            {
                passName = passName,
                archetype = PassArchetype.Raster,
                renderPassEvent = TryLoadRenderPassEvent(outDir, passName, out var evt) ? evt : RenderPassEvent.AfterRenderingOpaques,
            };

            var settingsText = File.ReadAllText(settingsPath);
            if (TryGetSentinelBlock(settingsText, "gen:so-properties", out var soBlock))
                passConfig.properties = ParsePropertiesFromSettingsBlock(soBlock);

            data.passes.Add(passConfig);
        }

        data.currentPanel = 1;
        data.selectedPassTab = 0;
        data.reentryLocked = true;
        return true;
    }

    public static void GenerateOrUpdate(RendererFeatureWizardData data, bool updateExisting)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ValidateWizardDataOrThrow(data);

        var outDir = GetOutputDirectory(data.featureName);
        Directory.CreateDirectory(outDir);

        var featurePath = Path.Combine(outDir, $"{data.featureName}RendererFeature.cs");
        WriteFile(featurePath, GenerateOrInject(featurePath, updateExisting, GenerateRendererFeatureFile(data)));

        foreach (var pass in data.passes)
        {
            var settingsPath = Path.Combine(outDir, $"{pass.passName}Settings.cs");
            var legacyPassPath = Path.Combine(outDir, $"{pass.passName}RenderPass.cs");
            var shaderPath = Path.Combine(outDir, $"{pass.passName}.shader");

            WriteFile(settingsPath, GenerateOrInject(settingsPath, updateExisting, GenerateSettingsFile(pass)));
            if (updateExisting && File.Exists(legacyPassPath))
                WriteFile(legacyPassPath, GenerateOrInject(legacyPassPath, updateExisting, GenerateLegacyRenderPassFile(pass)));
            WriteFile(shaderPath, GenerateOrInject(shaderPath, updateExisting, GenerateShaderFile(pass)));
        }

        AssetDatabase.Refresh();
    }

    private static void WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }

    private static string GenerateOrInject(string path, bool updateExisting, string newFileContents)
    {
        if (!updateExisting)
            return newFileContents;

        if (!File.Exists(path))
            return newFileContents;

        var existing = File.ReadAllText(path);
        if (!existing.Contains("// <gen:", StringComparison.Ordinal))
            return existing;

        var injected = existing;
        foreach (var tag in GetSentinelTagsForFile(path))
        {
            if (!TryGetSentinelBlock(newFileContents, tag, out var replacement))
                continue;

            injected = ReplaceSentinelBlock(injected, tag, replacement);
        }

        return injected;
    }

    private static IEnumerable<string> GetSentinelTagsForFile(string path)
    {
        var ext = Path.GetExtension(path);
        var name = Path.GetFileName(path);

        if (ext.Equals(".shader", StringComparison.OrdinalIgnoreCase))
            return new[] { "gen:hlsl-includes", "gen:shader-properties", "gen:cbuffer-properties", "gen:texture-declarations" };

        if (name.EndsWith("Settings.cs", StringComparison.OrdinalIgnoreCase))
            return new[] { "gen:so-properties" };

        if (name.EndsWith("RenderPass.cs", StringComparison.OrdinalIgnoreCase))
            return new[] { "gen:pass-data-class", "gen:record-body", "gen:execute-body" };

        if (name.EndsWith("RendererFeature.cs", StringComparison.OrdinalIgnoreCase))
            return new[] { "gen:pass-fields", "gen:create-body", "gen:addrendererpasses-body", "gen:dispose-body", "gen:pass-classes" };

        return Array.Empty<string>();
    }

    private static bool TryLoadRenderPassEvent(string outDir, string passName, out RenderPassEvent evt)
    {
        evt = RenderPassEvent.AfterRenderingOpaques;
        // Legacy structure: pass is in its own file.
        var legacyPassPath = Path.Combine(outDir, $"{passName}RenderPass.cs");
        if (File.Exists(legacyPassPath))
        {
            var legacyText = File.ReadAllText(legacyPassPath);
            var legacyMatch = Regex.Match(legacyText, @"renderPassEvent\s*=\s*RenderPassEvent\.(\w+)\s*;", RegexOptions.Multiline);
            if (legacyMatch.Success && Enum.TryParse(legacyMatch.Groups[1].Value, out RenderPassEvent legacyParsed))
            {
                evt = legacyParsed;
                return true;
            }
        }

        // New structure: pass class lives in the feature file.
        var featureFile = Directory.GetFiles(outDir, "*RendererFeature.cs", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (string.IsNullOrEmpty(featureFile) || !File.Exists(featureFile))
            return false;

        var text = File.ReadAllText(featureFile);

        var classIdx = text.IndexOf($"class {passName}RenderPass", StringComparison.Ordinal);
        if (classIdx < 0)
            return false;

        var slice = text.Substring(classIdx, Math.Min(5000, text.Length - classIdx));
        var m = Regex.Match(slice, @"renderPassEvent\s*=\s*RenderPassEvent\.(\w+)\s*;", RegexOptions.Multiline);
        if (!m.Success)
            return false;

        var name = m.Groups[1].Value;
        if (!Enum.TryParse(name, out RenderPassEvent parsed))
            return false;

        evt = parsed;
        return true;
    }
}
