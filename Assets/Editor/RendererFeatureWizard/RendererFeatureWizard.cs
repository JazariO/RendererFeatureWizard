using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class RendererFeatureWizard : EditorWindow
{
    private const string PrefKey = "RendererFeatureWizard.State.v1";

    private RendererFeatureWizardData m_Data;
    private Vector2 m_Scroll;

    [MenuItem("Assets/Create/Rendering/New Renderer Feature", false, 110)]
    private static void OpenWindow()
    {
        var w = GetWindow<RendererFeatureWizard>(utility: true, title: "Renderer Feature Wizard");
        w.minSize = new Vector2(720, 480);
        w.Show();
    }

    private void OnEnable()
    {
        m_Data = LoadState() ?? new RendererFeatureWizardData();
        EnsurePassList();
    }

    private void OnDisable()
    {
        SaveState();
    }

    private void OnGUI()
    {
        if (m_Data == null)
            m_Data = new RendererFeatureWizardData();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("RendererFeature Wizard (URP 17+ RenderGraph)", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
        try
        {
            switch (m_Data.currentPanel)
            {
                case 0:
                    DrawPanelFeatureDefinition();
                    break;
                case 1:
                    DrawPanelPassConfig();
                    break;
                default:
                    DrawPanelReview();
                    break;
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }

        if (GUI.changed)
            SaveState();
    }

    private void DrawPanelFeatureDefinition()
    {
        EditorGUILayout.LabelField("Panel 1 - Feature Definition", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        m_Data.featureName = EditorGUILayout.TextField("Feature Name", m_Data.featureName);

        using (new EditorGUI.DisabledScope(m_Data.reentryLocked))
        {
            m_Data.desiredPassCount = EditorGUILayout.IntSlider("Number Of Passes", m_Data.desiredPassCount, 1, 8);
        }

        var outDir = RendererFeatureGenerator.GetOutputDirectory(m_Data.featureName);
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Output Path", outDir);

        var dirExists = Directory.Exists(outDir);
        var hasSentinels = RendererFeatureGenerator.HasReentrySentinels(m_Data.featureName);
        if (m_Data.reentryLocked && !hasSentinels)
            m_Data.reentryLocked = false;
        if (dirExists && !hasSentinels)
        {
            EditorGUILayout.HelpBox("Output directory exists but no sentinels were detected. Generating will overwrite files in that folder.", MessageType.Warning);
        }
        else if (dirExists && hasSentinels)
        {
            EditorGUILayout.HelpBox("Existing generated feature detected (sentinels found). Next will load properties for update.", MessageType.Info);
        }

        EditorGUILayout.Space(12);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Cancel", GUILayout.Width(120)))
            {
                Close();
                return;
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!IsValidPascalIdentifier(m_Data.featureName)))
            {
                if (GUILayout.Button("Next", GUILayout.Width(120)))
                {
                    if (hasSentinels && RendererFeatureGenerator.TryLoadExisting(m_Data.featureName, out var loaded, out var error))
                    {
                        m_Data = loaded;
                    }
                    else if (hasSentinels && !string.IsNullOrEmpty(error))
                    {
                        EditorUtility.DisplayDialog("RendererFeature Wizard", error, "OK");
                        return;
                    }
                    else
                    {
                        m_Data.reentryLocked = false;
                        EnsurePassList();
                        m_Data.currentPanel = 1;
                    }

                    m_Data.currentPanel = 1;
                    m_Data.selectedPassTab = 0;
                    SaveState();
                }
            }
        }

        if (!IsValidPascalIdentifier(m_Data.featureName))
            EditorGUILayout.HelpBox("Feature name must be a valid C# identifier and start with an uppercase letter.", MessageType.Error);
    }

    private void DrawPanelPassConfig()
    {
        EditorGUILayout.LabelField("Panel 2 - Per-Pass Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        EnsurePassList();
        if (m_Data.passes.Count == 0)
        {
            EditorGUILayout.HelpBox("No passes configured.", MessageType.Error);
            return;
        }

        var tabNames = m_Data.passes.Select(p => string.IsNullOrWhiteSpace(p.passName) ? "<Pass>" : p.passName).ToArray();
        m_Data.selectedPassTab = Mathf.Clamp(m_Data.selectedPassTab, 0, m_Data.passes.Count - 1);
        m_Data.selectedPassTab = GUILayout.Toolbar(m_Data.selectedPassTab, tabNames);

        var pass = m_Data.passes[m_Data.selectedPassTab];
        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(m_Data.reentryLocked))
        {
            pass.passName = EditorGUILayout.TextField("Pass Name", pass.passName);

            using (new EditorGUI.DisabledScope(true))
            {
                pass.archetype = (PassArchetype)EditorGUILayout.EnumPopup("Archetype", pass.archetype);
            }

            pass.renderPassEvent = (RenderPassEvent)EditorGUILayout.EnumPopup("Render Pass Event", pass.renderPassEvent);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Sel", GUILayout.Width(28));
            GUILayout.Label("Type", GUILayout.Width(110));
            GUILayout.Label("C# Name", GUILayout.Width(200));
            GUILayout.Label("Default Value", GUILayout.MinWidth(200));
        }

        for (int i = 0; i < pass.properties.Count; i++)
        {
            var prop = pass.properties[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                prop.selected = EditorGUILayout.Toggle(prop.selected, GUILayout.Width(28));
                prop.type = (PropertyType)EditorGUILayout.EnumPopup(prop.type, GUILayout.Width(110));
                prop.name = EditorGUILayout.TextField(prop.name, GUILayout.Width(200));
                prop.defaultValue = EditorGUILayout.TextField(prop.defaultValue, GUILayout.MinWidth(200));
            }
        }

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Property", GUILayout.Width(140)))
            {
                pass.properties.Add(new RendererFeatureWizardData.PropertyConfig
                {
                    selected = false,
                    type = PropertyType.Float,
                    name = GetUniquePropertyName(pass, "newProperty"),
                    defaultValue = "0",
                });
            }

            if (GUILayout.Button("Remove Selected", GUILayout.Width(140)))
            {
                pass.properties.RemoveAll(p => p.selected);
            }

            GUILayout.FlexibleSpace();
        }

        var errors = ValidatePass(pass, showTypeErrors: true);
        if (!string.IsNullOrEmpty(errors))
            EditorGUILayout.HelpBox(errors, MessageType.Error);

        EditorGUILayout.Space(12);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Back", GUILayout.Width(120)))
            {
                m_Data.currentPanel = 0;
                SaveState();
                return;
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!IsPassSetValid()))
            {
                if (GUILayout.Button("Next", GUILayout.Width(120)))
                {
                    m_Data.currentPanel = 2;
                    SaveState();
                }
            }
        }
    }

    private void DrawPanelReview()
    {
        EditorGUILayout.LabelField("Panel 3 - Review And Generate", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        var outDir = RendererFeatureGenerator.GetOutputDirectory(m_Data.featureName);
        EditorGUILayout.LabelField("Feature", m_Data.featureName);
        EditorGUILayout.LabelField("Output Path", outDir);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Passes", EditorStyles.boldLabel);
        foreach (var pass in m_Data.passes)
        {
            EditorGUILayout.LabelField($"{pass.passName} ({pass.properties.Count} properties) - {pass.renderPassEvent}");
        }

        EditorGUILayout.Space(12);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Back", GUILayout.Width(120)))
            {
                m_Data.currentPanel = 1;
                SaveState();
                return;
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(120)))
            {
                Close();
                return;
            }

            GUILayout.FlexibleSpace();

            var buttonLabel = m_Data.reentryLocked ? "Update" : "Generate";
            using (new EditorGUI.DisabledScope(!IsPassSetValid()))
            {
                if (GUILayout.Button(buttonLabel, GUILayout.Width(140), GUILayout.Height(28)))
                {
                    try
                    {
                        RendererFeatureGenerator.GenerateOrUpdate(m_Data, updateExisting: m_Data.reentryLocked);

                        var outObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outDir);
                        if (outObj != null)
                            EditorGUIUtility.PingObject(outObj);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        EditorUtility.DisplayDialog("RendererFeature Wizard", ex.Message, "OK");
                    }
                }
            }
        }
    }

    private void EnsurePassList()
    {
        if (m_Data.passes == null)
            m_Data.passes = new System.Collections.Generic.List<RendererFeatureWizardData.PassConfig>();

        m_Data.desiredPassCount = Mathf.Clamp(m_Data.desiredPassCount, 1, 64);
        while (m_Data.passes.Count < m_Data.desiredPassCount)
        {
            var idx = m_Data.passes.Count + 1;
            m_Data.passes.Add(new RendererFeatureWizardData.PassConfig
            {
                passName = $"{m_Data.featureName}Pass{idx}",
                archetype = PassArchetype.Raster,
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques,
            });
        }

        if (!m_Data.reentryLocked)
        {
            while (m_Data.passes.Count > m_Data.desiredPassCount)
                m_Data.passes.RemoveAt(m_Data.passes.Count - 1);
        }
    }

    private RendererFeatureWizardData LoadState()
    {
        if (!EditorPrefs.HasKey(PrefKey))
            return null;

        var json = EditorPrefs.GetString(PrefKey, "");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonUtility.FromJson<RendererFeatureWizardData>(json);
        }
        catch
        {
            return null;
        }
    }

    private void SaveState()
    {
        if (m_Data == null)
            return;

        var json = JsonUtility.ToJson(m_Data);
        EditorPrefs.SetString(PrefKey, json);
    }

    private static bool IsValidPascalIdentifier(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (!char.IsUpper(text[0]))
            return false;
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z_][A-Za-z0-9_]*$");
    }

    private bool IsPassSetValid()
    {
        if (!IsValidPascalIdentifier(m_Data.featureName))
            return false;

        if (m_Data.passes == null || m_Data.passes.Count == 0)
            return false;

        var passNames = m_Data.passes.Select(p => p.passName).ToArray();
        if (passNames.Any(p => !IsValidPascalIdentifier(p)))
            return false;
        if (passNames.Distinct(StringComparer.Ordinal).Count() != passNames.Length)
            return false;

        foreach (var pass in m_Data.passes)
        {
            if (!string.IsNullOrEmpty(ValidatePass(pass, showTypeErrors: false)))
                return false;
        }

        return true;
    }

    private static string ValidatePass(RendererFeatureWizardData.PassConfig pass, bool showTypeErrors)
    {
        if (!IsValidPascalIdentifier(pass.passName))
            return "Pass name must be a valid C# identifier and start with an uppercase letter.";

        foreach (var prop in pass.properties)
        {
            if (string.IsNullOrWhiteSpace(prop.name))
                return "Property name cannot be empty.";

            if (!System.Text.RegularExpressions.Regex.IsMatch(prop.name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                return $"Invalid property name: '{prop.name}'.";

            if (char.IsUpper(prop.name[0]))
                return $"Property name should be camelCase: '{prop.name}'.";

            // Keep this aligned with generator's reserved keyword list.
            if (prop.name == "cbuffer" || prop.name == "struct" || prop.name == "return" || prop.name == "Texture2D")
                return $"Reserved HLSL keyword: '{prop.name}'.";

            if (showTypeErrors)
            {
                if ((prop.type == PropertyType.Vector4 || prop.type == PropertyType.Color) && !LooksLikeVector4(prop.defaultValue))
                    return $"Default value for {prop.type} should look like: 1, 1, 1, 1";
                if (prop.type == PropertyType.Float && !LooksLikeFloat(prop.defaultValue))
                    return "Default value for Float should be a number (use '.' as decimal separator).";
                if (prop.type == PropertyType.Int && !LooksLikeInt(prop.defaultValue))
                    return "Default value for Int should be an integer.";
            }
        }

        var dup = pass.properties.GroupBy(p => p.name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (dup != null)
            return $"Duplicate property name: '{dup.Key}'.";

        return "";
    }

    private static string GetUniquePropertyName(RendererFeatureWizardData.PassConfig pass, string baseName)
    {
        var name = baseName;
        var i = 1;
        var existing = pass.properties.Select(p => p.name).ToHashSet(StringComparer.Ordinal);
        while (existing.Contains(name))
        {
            i++;
            name = baseName + i;
        }
        return name;
    }

    private static bool LooksLikeFloat(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    private static bool LooksLikeInt(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        return int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    private static bool LooksLikeVector4(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        var parts = s.Split(',');
        if (parts.Length != 4)
            return false;
        return parts.All(p => float.TryParse(p.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _));
    }
}
