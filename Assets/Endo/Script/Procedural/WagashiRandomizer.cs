using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WagashiRandomizer : MonoBehaviour
{
    [Header("この和菓子用のプリセット")]
    [SerializeField] private WagashiPreset preset;
    private Renderer[] _renderers;
    private Rigidbody _rb;
    private Collider _col;
    private WagashiRuntime _runtime;

    // 「Presetごとのバケット → PhysicsMaterial」キャッシュ
    private static Dictionary<WagashiPreset, Dictionary<int, PhysicsMaterial>> _materialBucketCache
        = new Dictionary<WagashiPreset, Dictionary<int, PhysicsMaterial>>();

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _runtime = GetComponent<WagashiRuntime>();
        if (_runtime == null)
        {
            _runtime = gameObject.AddComponent<WagashiRuntime>();
        }
    }

    private void Start()
    {
        if (preset != null)
        {
            ApplyRandomParameters(preset);
        }
        else
        {
            Debug.LogError($"{name}: WagashiPreset が設定されていません");
        }
    }

    /// <summary>
    /// 外部から渡されたPresetに従ってランダム化
    /// </summary>
    private void ApplyRandomParameters(WagashiPreset preset)
    {
        _runtime.preset = preset;

        // ① カラー決定
        Color color = Color.white;
        if (preset.possibleColors != null && preset.possibleColors.Length > 0)
        {
            color = preset.possibleColors[Random.Range(0, preset.possibleColors.Length)];
        }

        // Rendererたちのマテリアルをインスタンス化して色を変更
        if (_renderers != null && _renderers.Length > 0)
        {
            if (preset.useSingleMaterialInstance)
            {
                // 1つだけMaterialインスタンスを作って共通で使う
                Material matInstance = Instantiate(_renderers[0].sharedMaterial);
                SetColor(matInstance, color);
                foreach (var r in _renderers)
                {
                    r.sharedMaterial = matInstance;
                }
            }
            else
            {
                // Rendererごとに別インスタンス
                foreach (var r in _renderers)
                {
                    var matInstance = Instantiate(r.sharedMaterial);
                    SetColor(matInstance, color);
                    r.sharedMaterial = matInstance;
                }
            }
        }

        // ② スケールランダム(いらなそう)
        Vector3 scale = new Vector3(
            Random.Range(preset.minScale.x, preset.maxScale.x),
            Random.Range(preset.minScale.y, preset.maxScale.y),
            Random.Range(preset.minScale.z, preset.maxScale.z)
        );
        transform.localScale = scale;


        // 質量ランダム
        float mass = Random.Range(preset.minMass, preset.maxMass);
        _rb.mass = mass;

        // 弾力ランダム（PhysicMaterial）
        if (_col != null)
        {
            //バケット方式にするかどうか
            if (preset.useBucketForBounciness)
            {
                ApplyBucketBounciness(preset);
            }
            else
            {
                ApplyRandomBounciness(preset);
            }
        }

        // ⑤ 伸び率パラメータ（後でスクイッシュに使う）
        float stretchFactor = Random.Range(preset.minStretchFactor, preset.maxStretchFactor);

        // ⑥ WagashiRuntime に保存
        _runtime.currentColor = color;
        _runtime.currentMass = mass;
        _runtime.currentStretchFactor = stretchFactor;
    }

    private void SetColor(Material mat, Color color)
    {
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }

    private void ApplyBucketBounciness(WagashiPreset preset)
    {
        // min == max の場合に0割りを避ける
        float min = preset.minBounciness;
        float max = Mathf.Max(preset.minBounciness + 0.0001f, preset.maxBounciness);

        int divisions = Mathf.Max(1, preset.bucketDivisions);

        // まず min-max の中からランダムで生の値を取得
        float raw = Random.Range(min, max);

        // その値が min-max の中でどの位置か（0-1）
        float t = (raw - min) / (max - min);
        t = Mathf.Clamp01(t);

        // 0-divisions の間でバケット化
        int bucket = Mathf.RoundToInt(t * divisions);
        bucket = Mathf.Clamp(bucket, 0, divisions);

        // Presetごとの内部キャッシュを取得
        if (!_materialBucketCache.TryGetValue(preset, out var bucketDict))
        {
            bucketDict = new Dictionary<int, PhysicsMaterial>();
            _materialBucketCache[preset] = bucketDict;
        }

        PhysicsMaterial pm;
        if (!bucketDict.TryGetValue(bucket, out pm))
        {
            // このバケット用の PhysicsMaterial を新規作成
            pm = new PhysicsMaterial();

            // バケット中心の値を計算（min-maxの間）
            float snappedT = (float)bucket / divisions;
            float snappedBounciness = Mathf.Lerp(min, max, snappedT);

            pm.bounciness = snappedBounciness;
            pm.bounceCombine = PhysicsMaterialCombine.Maximum;

            // 摩擦（同じ snappedT でバケット化）
            float dyn = Mathf.Lerp(preset.minDynamicFriction, preset.maxDynamicFriction, snappedT);
            float sta = Mathf.Lerp(preset.minStaticFriction, preset.maxStaticFriction, snappedT);

            _runtime.currentDynamicFriction = dyn;
            _runtime.currentStaticFriction = sta;

            pm.dynamicFriction = dyn;
            pm.staticFriction = sta;
            pm.frictionCombine = preset.frictionCombine;

            bucketDict[bucket] = pm;
        }

        _col.material = pm;
        _runtime.currentBounciness = pm.bounciness;
    }

    private void ApplyRandomBounciness(WagashiPreset preset)
    {

        PhysicsMaterial pmInstance = (_col.material != null)
       ? Object.Instantiate(_col.material)
       : new PhysicsMaterial();

        //PhysicsMaterial pmInstance;

        //if (_col.material != null)
        //{
        //    pmInstance = Object.Instantiate(_col.material);
        //}
        //else
        //{
        //    pmInstance = new PhysicsMaterial();
        //}

        //弾力（完全ランダム）
        float bounciness = Random.Range(preset.minBounciness, preset.maxBounciness);
        pmInstance.bounciness = bounciness;
        pmInstance.bounceCombine = PhysicsMaterialCombine.Maximum;

        // 摩擦（完全ランダム）
        float dyn = Random.Range(preset.minDynamicFriction, preset.maxDynamicFriction);
        float sta = Random.Range(preset.minStaticFriction, preset.maxStaticFriction);

        _runtime.currentDynamicFriction = dyn;
        _runtime.currentStaticFriction = sta;

        pmInstance.dynamicFriction = dyn;
        pmInstance.staticFriction = sta;
        pmInstance.frictionCombine = preset.frictionCombine;

        _col.material = pmInstance;
        _runtime.currentBounciness = bounciness;
    }


}
