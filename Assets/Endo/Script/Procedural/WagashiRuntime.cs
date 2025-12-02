using UnityEngine;

public class WagashiRuntime : MonoBehaviour
{
    [HideInInspector] public WagashiPreset preset;

    [Header("生成時に決まった値（デバッグ用に見えるようにしておく）")]
    public Color currentColor;
    public float currentMass;
    public float currentBounciness;
    public float currentStretchFactor;
}
