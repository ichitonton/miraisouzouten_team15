using UnityEngine;

[CreateAssetMenu(fileName = "WagashiPreset", menuName = "Scriptable Objects/WagashiPreset")]
public class WagashiPreset : ScriptableObject
{
    [Header("基本情報")]
    public string wagashiName;

    [Header("使用するPrefab")]
    public GameObject wagashiPrefab;   // ここに既存の和菓子Prefabを割り当てる

    [Header("見た目設定")]
    public Color[] possibleColors;     // ベース色の候補
    public bool useSingleMaterialInstance = true; // trueなら全Renderer共通マテリアルにする

    [Header("サイズ")]
    public Vector3 minScale = Vector3.one * 0.9f;
    public Vector3 maxScale = Vector3.one * 1.1f;

    [Header("物理: 重量・弾力")]
    public float minMass = 0.2f;
    public float maxMass = 1.0f;

    public float minBounciness = 0.0f;
    public float maxBounciness = 0.8f;


    [Header("弾力バケット設定")]
    public bool useBucketForBounciness = true;

    // min- max の間を何分割するか（例：10なら minmax を 0-10 段階）
    [Header("バケットの分割数")]
    [Range(1, 20)]
    public int bucketDivisions = 10;

    [Header("摩擦（滑りにくさ）")]
    [Range(0f, 2f)] public float minDynamicFriction = 0.7f;
    [Range(0f, 2f)] public float maxDynamicFriction = 1.2f;
    [Range(0f, 2f)] public float minStaticFriction = 0.9f;
    [Range(0f, 2f)] public float maxStaticFriction = 1.4f;

    [Header("Combine（当たり方の合成）")]
    public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Maximum;
    public PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Maximum;



    [Header("モチモチ（のび率）")]
    public float minStretchFactor = 0.05f;
    public float maxStretchFactor = 0.2f;
}
