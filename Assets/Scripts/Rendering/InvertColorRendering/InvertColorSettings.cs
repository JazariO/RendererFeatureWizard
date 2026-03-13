using UnityEngine;

[CreateAssetMenu(fileName = "InvertColorSettings", menuName = "Rendering/InvertColorSettings")]
public class InvertColorSettings : ScriptableObject
{
    // <gen:so-properties>
    [SerializeField] private float intensity = 1f;
    public float Intensity => intensity;

    // </gen:so-properties>
}
