using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
internal static class RendererFeatureWizardAutoAdd
{
    private const string PendingKey = "RendererFeatureWizard.AutoAdd.v1";
    private const string DefaultSearchRoot = "Assets/Settings";
    private const string DefaultRendererName = "PC_Renderer";

    [Serializable]
    private sealed class PendingRequest
    {
        public string rendererAssetPath;
        public string featureClassName;
    }

    static RendererFeatureWizardAutoAdd()
    {
        // Run on next editor tick to ensure AssetDatabase is ready.
        EditorApplication.delayCall += TryProcessPending;
    }

    internal static bool TryFindDefaultRenderer(out ScriptableRendererData rendererData, out string rendererAssetPath)
    {
        rendererData = null;
        rendererAssetPath = null;

        if (!AssetDatabase.IsValidFolder(DefaultSearchRoot))
            return false;

        var guids = AssetDatabase.FindAssets($"{DefaultRendererName} t:UniversalRendererData", new[] { DefaultSearchRoot });
        if (guids == null || guids.Length == 0)
            return false;

        rendererAssetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);
        return rendererData != null;
    }

    internal static void Request(string featureClassName, string rendererAssetPath)
    {
        if (string.IsNullOrWhiteSpace(featureClassName) || string.IsNullOrWhiteSpace(rendererAssetPath))
            return;

        var pending = new PendingRequest
        {
            featureClassName = featureClassName,
            rendererAssetPath = rendererAssetPath,
        };

        EditorPrefs.SetString(PendingKey, JsonUtility.ToJson(pending));
    }

    private static void TryProcessPending()
    {
        if (!EditorPrefs.HasKey(PendingKey))
            return;

        if (EditorApplication.isCompiling)
        {
            // Try again after compilation completes.
            EditorApplication.delayCall += TryProcessPending;
            return;
        }

        PendingRequest pending = null;
        try
        {
            pending = JsonUtility.FromJson<PendingRequest>(EditorPrefs.GetString(PendingKey, ""));
        }
        catch
        {
            // ignore
        }

        if (pending == null || string.IsNullOrWhiteSpace(pending.rendererAssetPath) || string.IsNullOrWhiteSpace(pending.featureClassName))
        {
            EditorPrefs.DeleteKey(PendingKey);
            return;
        }

        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(pending.rendererAssetPath);
        if (rendererData == null)
        {
            Debug.LogWarning($"RendererFeatureWizard: Could not load renderer at '{pending.rendererAssetPath}'.");
            EditorPrefs.DeleteKey(PendingKey);
            return;
        }

        var featureType = TypeCache.GetTypesDerivedFrom<ScriptableRendererFeature>()
            .FirstOrDefault(t => t.Name == pending.featureClassName);

        if (featureType == null)
        {
            Debug.LogWarning($"RendererFeatureWizard: Could not find feature type '{pending.featureClassName}'. Did the scripts compile?");
            // Keep pending so a future domain reload can retry.
            return;
        }

        if (rendererData.rendererFeatures != null && rendererData.rendererFeatures.Any(f => f != null && f.GetType() == featureType))
        {
            Debug.Log($"RendererFeatureWizard: '{featureType.Name}' is already present on '{rendererData.name}'.");
            EditorPrefs.DeleteKey(PendingKey);
            return;
        }

        var feature = ScriptableObject.CreateInstance(featureType) as ScriptableRendererFeature;
        if (feature == null)
        {
            Debug.LogWarning($"RendererFeatureWizard: Failed to create instance of '{featureType.Name}'.");
            EditorPrefs.DeleteKey(PendingKey);
            return;
        }

        feature.name = featureType.Name;

        AssetDatabase.AddObjectToAsset(feature, rendererData);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

        var so = new SerializedObject(rendererData);
        var featuresProp = so.FindProperty("m_RendererFeatures");
        var mapProp = so.FindProperty("m_RendererFeatureMap");

        if (featuresProp == null || mapProp == null || !featuresProp.isArray || !mapProp.isArray)
        {
            Debug.LogWarning($"RendererFeatureWizard: Could not access renderer feature list on '{rendererData.name}'.");
            EditorPrefs.DeleteKey(PendingKey);
            return;
        }

        featuresProp.arraySize++;
        featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;

        mapProp.arraySize++;
        mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = localId;

        so.ApplyModifiedProperties();
        rendererData.SetDirty();
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();

        Debug.Log($"RendererFeatureWizard: Added '{featureType.Name}' to '{pending.rendererAssetPath}'.");
        EditorPrefs.DeleteKey(PendingKey);
    }
}
