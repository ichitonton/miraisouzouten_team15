using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WagashiRandomizer : MonoBehaviour
{
    [Header("この和菓子のプリセット")]
    [SerializeField] private WagashiPreset preset;

    [Header("自動適用")]
    [SerializeField] private bool autoApplyOnStart = true;

    [Tooltip("deformModeに応じて不要なDeformerを無効化する")]
    [SerializeField] private bool disableUnusedDeformers = true;

    private Renderer[] _renderers;
    private Rigidbody _rb;
    private Collider _col;
    private WagashiRuntime _runtime;

    // Presetごとに PhysicsMaterial をバケットキャッシュ
    private static readonly Dictionary<WagashiPreset, Dictionary<int, PhysicsMaterial>> _pmBucketCache
        = new Dictionary<WagashiPreset, Dictionary<int, PhysicsMaterial>>();

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();

        _runtime = GetComponent<WagashiRuntime>();
        if (_runtime == null) _runtime = gameObject.AddComponent<WagashiRuntime>();
    }

    private void Start()
    {
        if (!autoApplyOnStart) return;
        if (preset == null)
        {
            Debug.LogWarning($"{name}: WagashiPreset が未設定です");
            return;
        }

        ApplyRandomParameters(preset);
    }

    /// <summary>
    /// 生成時に呼ぶメイン入口
    /// </summary>
    public void ApplyRandomParameters(WagashiPreset p)
    {
        if (p == null) return;

        preset = p;
        _runtime.preset = p;

        ApplyVisual(p);
        ApplyRigidbody(p);
        ApplyPhysicsMaterial(p);

        ApplyDeformMode(p);
    }

    // -------------------------
    // 見た目
    // -------------------------
    private void ApplyVisual(WagashiPreset p)
    {
        // 色
        Color color = Color.white;
        if (p.possibleColors != null && p.possibleColors.Length > 0)
        {
            color = p.possibleColors[Random.Range(0, p.possibleColors.Length)];
        }

        if (_renderers != null && _renderers.Length > 0)
        {
            if (p.useSingleMaterialInstance)
            {
                Material mat = Instantiate(_renderers[0].sharedMaterial);
                SetColor(mat, color);

                foreach (var r in _renderers)
                    r.sharedMaterial = mat;
            }
            else
            {
                foreach (var r in _renderers)
                {
                    Material mat = Instantiate(r.sharedMaterial);
                    SetColor(mat, color);
                    r.sharedMaterial = mat;
                }
            }
        }

        // スケール
        Vector3 scale = new Vector3(
            Random.Range(Mathf.Min(p.minScale.x, p.maxScale.x), Mathf.Max(p.minScale.x, p.maxScale.x)),
            Random.Range(Mathf.Min(p.minScale.y, p.maxScale.y), Mathf.Max(p.minScale.y, p.maxScale.y)),
            Random.Range(Mathf.Min(p.minScale.z, p.maxScale.z), Mathf.Max(p.minScale.z, p.maxScale.z))
        );
        transform.localScale = scale;

        _runtime.currentColor = color;
        _runtime.currentScale = scale;
    }

    private void SetColor(Material mat, Color color)
    {
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }

    // -------------------------
    // Rigidbody
    // -------------------------
    private void ApplyRigidbody(WagashiPreset p)
    {
        float mass = Rand(p.mass);
        float linD = Rand(p.linearDamping);
        float angD = Rand(p.angularDamping);

        _rb.mass = mass;

        // Unity 6: Linear/Angular Damping
        // もし環境差でプロパティが無い場合は drag/angularDrag に置換してね
        _rb.linearDamping = linD;
        _rb.angularDamping = angD;

        _runtime.currentMass = mass;
        _runtime.currentLinearDamping = linD;
        _runtime.currentAngularDamping = angD;
    }

    // -------------------------
    // PhysicsMaterial（弾力＋摩擦）
    // -------------------------
    private void ApplyPhysicsMaterial(WagashiPreset p)
    {
        if (_col == null) return;

        // まずレンジ抽選（Runtime確定値）
        float bounciness = Rand(p.bounciness);
        float dyn = Rand(p.dynamicFriction);
        float sta = Rand(p.staticFriction);

        _runtime.currentBounciness = bounciness;
        _runtime.currentDynamicFriction = dyn;
        _runtime.currentStaticFriction = sta;

        if (p.useBucketForPhysicsMaterial)
        {
            ApplyBucketPhysicsMaterial(p, bounciness, dyn, sta);
        }
        else
        {
            ApplyUniquePhysicsMaterial(p, bounciness, dyn, sta);
        }
    }

    private void ApplyUniquePhysicsMaterial(WagashiPreset p, float b, float dyn, float sta)
    {
        PhysicsMaterial pm = (_col.material != null) ? Instantiate(_col.material) : new PhysicsMaterial();
        FillPhysicsMaterial(pm, p, b, dyn, sta);
        _col.material = pm;
    }

    private void ApplyBucketPhysicsMaterial(WagashiPreset p, float b, float dyn, float sta)
    {
        int div = Mathf.Max(1, p.bucketDivisions);

        // bouncinessレンジで t を作り、バケットへ
        float minB = Mathf.Min(p.bounciness.min, p.bounciness.max);
        float maxB = Mathf.Max(p.bounciness.min, p.bounciness.max);
        float denom = Mathf.Max(0.0001f, maxB - minB);
        float t = Mathf.Clamp01((b - minB) / denom);

        int bucket = Mathf.Clamp(Mathf.RoundToInt(t * div), 0, div);

        if (!_pmBucketCache.TryGetValue(p, out var dict))
        {
            dict = new Dictionary<int, PhysicsMaterial>();
            _pmBucketCache[p] = dict;
        }

        if (!dict.TryGetValue(bucket, out var pm))
        {
            pm = new PhysicsMaterial();

            // バケット中心値でスナップ（同バケットは同値になる）
            float snappedT = (float)bucket / div;

            float snappedB = Mathf.Lerp(minB, maxB, snappedT);

            float minD = Mathf.Min(p.dynamicFriction.min, p.dynamicFriction.max);
            float maxD = Mathf.Max(p.dynamicFriction.min, p.dynamicFriction.max);
            float snappedDyn = Mathf.Lerp(minD, maxD, snappedT);

            float minS = Mathf.Min(p.staticFriction.min, p.staticFriction.max);
            float maxS = Mathf.Max(p.staticFriction.min, p.staticFriction.max);
            float snappedSta = Mathf.Lerp(minS, maxS, snappedT);

            FillPhysicsMaterial(pm, p, snappedB, snappedDyn, snappedSta);
            dict[bucket] = pm;

            // Runtimeにもスナップ値で上書きしておく（デバッグ一致）
            _runtime.currentBounciness = snappedB;
            _runtime.currentDynamicFriction = snappedDyn;
            _runtime.currentStaticFriction = snappedSta;
        }

        _col.material = pm;
    }

    private void FillPhysicsMaterial(PhysicsMaterial pm, WagashiPreset p, float b, float dyn, float sta)
    {
        pm.bounciness = Mathf.Clamp01(b);
        pm.dynamicFriction = Mathf.Max(0f, dyn);
        pm.staticFriction = Mathf.Max(0f, sta);

        pm.bounceCombine = p.bounceCombine;
        pm.frictionCombine = p.frictionCombine;
    }

    // -------------------------
    // 変形（Mochi / Prupru）
    // -------------------------
    private void ApplyDeformMode(WagashiPreset p)
    {
        switch (p.deformMode)
        {
            case WagashiDeformMode.Mochi:
                ApplyMochiRuntime(p);
                if (disableUnusedDeformers)
                {
                    DisableIfExists<WagashiPrupruDeformer>();
                    EnableIfExists<WagashiMochiMochiDeformer>();
                }
                break;

            case WagashiDeformMode.Prupru:
                ApplyPrupruRuntimeAndInject(p);
                if (disableUnusedDeformers)
                {
                    DisableIfExists<WagashiMochiMochiDeformer>();
                    EnableIfExists<WagashiPrupruDeformer>();
                }
                break;

            default:
                if (disableUnusedDeformers)
                {
                    DisableIfExists<WagashiMochiMochiDeformer>();
                    DisableIfExists<WagashiPrupruDeformer>();
                }
                break;
        }
    }

    private void ApplyMochiRuntime(WagashiPreset p)
    {
        // モチモチ度だけは Runtime に確定値として保存（MochiDeformer が参照して動く想定）
        _runtime.currentStretchFactor = Rand(p.mochiStretchFactor);

        // MochiDeformerのコードは「完璧」って言ってたので、ここから注入はしない
        // （必要なら後で MochiDeformer に ApplySettings を足して同じ構造にできる）
    }

    private void ApplyPrupruRuntimeAndInject(WagashiPreset p)
    {
        var r = p.prupru;

        // 抽選 → Runtime保存
        float minImp = Rand(r.minImpactVelocity);
        float maxImp = Mathf.Max(minImp + 0.01f, Rand(r.maxImpactVelocity));

        float freq = Rand(r.frequencyHz);
        float damp = Mathf.Clamp01(Rand(r.damping01));
        float imp = Rand(r.impulse);
        float maxTilt = Rand(r.maxTiltDeg);

        float normalInf = Rand(r.normalInfluence);
        float vertResp = Mathf.Clamp01(Rand(r.verticalResponse));
        float decay = Mathf.Clamp01(Rand(r.extraDecay));

        _runtime.prupruMinImpactVelocity = minImp;
        _runtime.prupruMaxImpactVelocity = maxImp;
        _runtime.prupruFrequencyHz = freq;
        _runtime.prupruDamping = damp;
        _runtime.prupruImpulse = imp;
        _runtime.prupruMaxTiltDeg = maxTilt;
        _runtime.prupruNormalInfluence = normalInf;
        _runtime.prupruVerticalResponse = vertResp;
        _runtime.prupruExtraDecay = decay;

        // Deformerへ注入（無ければ付ける）
        var deformer = GetOrAddComponent<WagashiPrupruDeformer>();

        // WagashiPrupruDeformer 側にこの struct と ApplySettings がある前提
        var s = new WagashiPrupruDeformer.PrupruSettings
        {
            minImpactVelocity = minImp,
            maxImpactVelocity = maxImp,
            wobbleFrequency = freq,
            wobbleDamping = damp,
            wobbleImpulse = imp,
            maxTiltAngle = maxTilt,
            normalInfluence = normalInf,
            verticalResponse = vertResp,
            extraDecay = decay
        };

        deformer.ApplySettings(s);
    }

    // -------------------------
    // 便利関数
    // -------------------------
    private static float Rand(WagashiPreset.FloatRange r)
    {
        float min = r.min;
        float max = r.max;
        if (max < min) (min, max) = (max, min);
        return Random.Range(min, max);
    }

    private T GetOrAddComponent<T>() where T : Component
    {
        var c = GetComponent<T>();
        return c != null ? c : gameObject.AddComponent<T>();
    }

    private void DisableIfExists<T>() where T : Behaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = false;
    }

    private void EnableIfExists<T>() where T : Behaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = true;
    }


}
