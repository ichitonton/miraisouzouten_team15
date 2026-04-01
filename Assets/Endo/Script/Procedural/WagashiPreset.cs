using UnityEngine;

public enum WagashiDeformMode
{
    None,
    Mochi,
    Prupru
}

/// <summary>
/// 和菓子1種類の「プロシージャル設定」をまとめるScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "WagashiPreset", menuName = "Scriptable Objects/WagashiPreset")]
public class WagashiPreset : ScriptableObject
{


    [Header("基本情報")]
    public string wagashiName;

    [Header("変形モード（併用しない）")]
    public WagashiDeformMode deformMode = WagashiDeformMode.None;

    // -------------------------
    // 便利：Inspectorでレンジを扱う用
    // -------------------------
    [System.Serializable]
    public struct FloatRange
    {
        public float min;
        public float max;

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float ClampMinMax()
        {
            if (max < min) (min, max) = (max, min);
            return max - min;
        }
    }

    [System.Serializable]
    public struct IntRange
    {
        public int min;
        public int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }
    }

    // -------------------------
    // 見た目
    // -------------------------
    [Header("見た目：色")]
    public Color[] possibleColors;

    [Tooltip("true: 1つのマテリアルインスタンスを全Rendererで共有 / false: Rendererごとに別インスタンス")]
    public bool useSingleMaterialInstance = true;

    [Header("見た目：スケール")]
    public Vector3 minScale = Vector3.one * 0.9f;
    public Vector3 maxScale = Vector3.one * 1.1f;

    // -------------------------
    // 物理（Rigidbody）
    // -------------------------
    [Header("物理：質量")]
    public FloatRange mass = new FloatRange(3f, 4f);

    [Header("物理：減衰（Unity6：Linear/Angular Damping）")]
    public FloatRange linearDamping = new FloatRange(1f, 3f);
    public FloatRange angularDamping = new FloatRange(2f, 6f);

    // -------------------------
    // 物理（Collider Material）
    // -------------------------
    [Header("物理：弾力（Bounciness）")]
    public FloatRange bounciness = new FloatRange(0.0f, 0.5f);

    [Header("物理：摩擦（Friction）")]
    public FloatRange dynamicFriction = new FloatRange(0.7f, 1.2f);
    public FloatRange staticFriction = new FloatRange(0.9f, 1.4f);

    [Header("物理：Combine")]
    public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Maximum;
    public PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Maximum;

    [Header("弾力/摩擦：バケット（軽量化）")]
    public bool useBucketForPhysicsMaterial = true;

    [Tooltip("min max を何分割するか（例：10なら 0-10 の 11段階）")]
    [Range(1, 30)]
    public int bucketDivisions = 10;

    // -------------------------
    // モチモチ（Mochi）用
    // -------------------------
    [Header("モチモチ度（Mochi）：個体差")]
    public FloatRange mochiStretchFactor = new FloatRange(0.05f, 0.25f);

    // -------------------------
    // プルプル（Prupru）用：プロシージャルレンジ
    // -------------------------
    [System.Serializable]
    public class PrupruRange
    {
        [Header("衝突の反応範囲")]
        public FloatRange minImpactVelocity = new FloatRange(0.35f, 0.6f);
        public FloatRange maxImpactVelocity = new FloatRange(4.0f, 6.0f);

        [Header("ぷるぷる（回転）")]
        public FloatRange frequencyHz = new FloatRange(2.0f, 3.0f);
        public FloatRange damping01 = new FloatRange(0.18f, 0.35f);
        public FloatRange impulse = new FloatRange(20f, 40f);
        public FloatRange maxTiltDeg = new FloatRange(8f, 16f);

        [Header("方向の自然さ")]
        public FloatRange normalInfluence = new FloatRange(0.8f, 1.2f);
        public FloatRange verticalResponse = new FloatRange(0.2f, 0.5f);

        [Header("追加減衰（停止を早める）")]
        public FloatRange extraDecay = new FloatRange(0.97f, 0.995f);
    }

    [Header("Prupru（プリンぷるぷる）レンジ")]
    public PrupruRange prupru = new PrupruRange();

}
