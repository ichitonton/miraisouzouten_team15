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

    [Header("モチモチ（のび率）")]
    public float minStretchFactor = 0.05f;
    public float maxStretchFactor = 0.2f;
}
