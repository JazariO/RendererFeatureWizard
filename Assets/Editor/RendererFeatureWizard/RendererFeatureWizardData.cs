using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[Serializable]
public sealed class RendererFeatureWizardData
{
    public int currentPanel = 0;
    public bool reentryLocked;
    public bool autoAddToPCRenderer = true;

    public string featureName = "NewFeature";

    public int desiredPassCount = 1;
    public int selectedPassTab = 0;

    public List<PassConfig> passes = new List<PassConfig>();

    [Serializable]
    public sealed class PassConfig
    {
        public string passName = "NewPass";
        public PassArchetype archetype = PassArchetype.Raster;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public List<PropertyConfig> properties = new List<PropertyConfig>();
    }

    [Serializable]
    public sealed class PropertyConfig
    {
        public bool selected;
        public PropertyType type = PropertyType.Float;
        public string name = "newProperty";
        public string defaultValue = "";
    }
}

public enum PassArchetype
{
    Raster = 0,
    Compute = 1,
    FullscreenBlit = 2,
}

public enum PropertyType
{
    Float = 0,
    Int = 1,
    Vector4 = 2,
    Color = 3,
    Texture2D = 4,
}
