using UnityEngine;

/// <summary>
/// 生成時に決まった「確定値」を保持する（デバッグ・ネット同期にも使える）
/// </summary>
public class WagashiRuntime : MonoBehaviour
{
    [HideInInspector] public WagashiPreset preset;

    [Header("確定：見た目")]
    public Color currentColor;
    public Vector3 currentScale;

    [Header("確定：Rigidbody")]
    public float currentMass;
    public float currentLinearDamping;
    public float currentAngularDamping;

    [Header("確定：PhysicsMaterial")]
    public float currentBounciness;
    public float currentDynamicFriction;
    public float currentStaticFriction;

    [Header("確定：モチモチ（Mochi）")]
    public float currentStretchFactor;

    [Header("確定：プルプル（Prupru）")]
    public float prupruMinImpactVelocity;
    public float prupruMaxImpactVelocity;
    public float prupruFrequencyHz;
    public float prupruDamping;
    public float prupruImpulse;
    public float prupruMaxTiltDeg;
    public float prupruNormalInfluence;
    public float prupruVerticalResponse;
    public float prupruExtraDecay;

}
