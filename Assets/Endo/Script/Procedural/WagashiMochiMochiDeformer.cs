using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WagashiRuntime))]
public class WagashiMochiMochiDeformer : MonoBehaviour
{

    public enum DeformTriggerMode
    {
        DeformOnlyHitPart,   // 当たった部位だけムニる（おすすめ）
        DeformAllTargets     // どれか当たったら全部ムニる
    }

    [Header("変形対象（複数）")]
    [Tooltip("未指定ならこのオブジェクト自身を変形します")]
    [SerializeField] private List<Transform> deformTargets = new();

    [Header("当たり判定に使う部位（複数）")]
    [Tooltip("空なら『どこで当たっても』反応。指定すると、そのTransform配下のColliderで当たった時だけ反応します。")]
    [SerializeField] private List<Transform> collisionPartRoots = new();

    [Header("ヒット時の挙動")]
    [SerializeField] private DeformTriggerMode triggerMode = DeformTriggerMode.DeformOnlyHitPart;

    [Header("衝突判定パラメータ")]
    [SerializeField] private float minImpactVelocity = 0.8f;
    [SerializeField] private float maxImpactVelocity = 4.0f;

    [Header("スクイッシュ量（基準）")]
    [SerializeField] private float baseSquishAmount = 0.4f;

    [Header("縦/横でのスクイッシュ倍率")]
    [SerializeField] private float verticalSquishMultiplier = 1.25f;
    [SerializeField] private float horizontalSquishMultiplier = 0.85f;

    [Header("ブレンド（斜め衝突の自然さ）")]
    [SerializeField] private float axisBlendSharpness = 3.5f;

    [Header("戻りスピード")]
    [SerializeField] private float returnSpeed = 4.5f;

    [Header("つぶれ下限（潰れすぎ防止）")]
    [SerializeField] private float minAxisScale = 0.35f;

    private WagashiRuntime _runtime;

    // ターゲットごとにデフォルト/目標スケールを持つ
    private readonly Dictionary<Transform, Vector3> _defaultScale = new();
    private readonly Dictionary<Transform, Vector3> _targetScale = new();

    private void Awake()
    {
        _runtime = GetComponent<WagashiRuntime>();

        // 未指定なら親自身をターゲットにする
        if (deformTargets == null || deformTargets.Count == 0)
        {
            deformTargets = new List<Transform> { transform };
        }

        // collisionPartRoots を空にしたい場合は「どこで当たっても」反応
        // ただし DeformOnlyHitPart を使うなら、通常は deformTargets と同じものを入れるのが直感的
    }

    private void Start()
    {
        StartCoroutine(CaptureDefaultScaleNextFrame());
    }

    private IEnumerator CaptureDefaultScaleNextFrame()
    {
        yield return null;

        _defaultScale.Clear();
        _targetScale.Clear();

        foreach (var t in deformTargets)
        {
            if (t == null) continue;
            _defaultScale[t] = t.localScale;
            _targetScale[t] = t.localScale;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // どの部位が当たったかを判定（空なら null = 全許可）
        Transform hitPartRoot = ResolveHitPartRoot(collision);

        // 指定部位があるのに、どれにも当たってない → 無視
        if (collisionPartRoots != null && collisionPartRoots.Count > 0 && hitPartRoot == null)
            return;

        float impact = collision.relativeVelocity.magnitude;
        if (impact < minImpactVelocity) return;

        float t = Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, impact);
        t = Mathf.Clamp01(t);

        float mochiFactor = 1f + Mathf.Max(0f, _runtime.currentStretchFactor);
        float amountBase = baseSquishAmount * t * mochiFactor;
        amountBase = Mathf.Clamp(amountBase, 0f, 0.75f);

        Vector3 normal = AverageContactNormal(collision);
        if (normal.sqrMagnitude < 1e-6f) return;

        if (triggerMode == DeformTriggerMode.DeformAllTargets || hitPartRoot == null)
        {
            // どれか当たったら全部（または部位指定なしで常に全）
            foreach (var target in deformTargets)
            {
                if (target == null) continue;
                ApplyBlendedDirectionalSquishToTarget(target, amountBase, normal);
            }
        }
        else
        {
            // 当たった部位だけ
            // hitPartRoot と deformTargets を “親子関係” で紐づけて、該当ターゲットだけムニる
            foreach (var target in deformTargets)
            {
                if (target == null) continue;
                if (target == hitPartRoot || target.IsChildOf(hitPartRoot) || hitPartRoot.IsChildOf(target))
                {
                    ApplyBlendedDirectionalSquishToTarget(target, amountBase, normal);
                }
            }
        }
    }

    /// <summary>
    /// collisionPartRoots が空なら null（全許可）
    /// 指定ありなら「当たったthisColliderがどのRoot配下か」を返す
    /// </summary>
    private Transform ResolveHitPartRoot(Collision collision)
    {
        if (collisionPartRoots == null || collisionPartRoots.Count == 0)
            return null;

        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            var cp = collision.GetContact(i);
            var thisCol = cp.thisCollider;
            if (thisCol == null) continue;

            var ct = thisCol.transform;

            foreach (var root in collisionPartRoots)
            {
                if (root == null) continue;
                if (ct == root || ct.IsChildOf(root))
                    return root;
            }
        }

        return null;
    }

    private Vector3 AverageContactNormal(Collision collision)
    {
        int count = collision.contactCount;
        if (count <= 0) return Vector3.up;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < count; i++)
            sum += collision.GetContact(i).normal;

        return (sum / count).normalized;
    }

    private void ApplyBlendedDirectionalSquishToTarget(Transform target, float amountBase, Vector3 worldNormal)
    {
        if (!_defaultScale.TryGetValue(target, out var baseScale))
        {
            baseScale = target.localScale;
            _defaultScale[target] = baseScale;
        }

        // 法線をターゲットのローカルで解釈
        Vector3 ln = target.InverseTransformDirection(worldNormal).normalized;
        ln = new Vector3(Mathf.Abs(ln.x), Mathf.Abs(ln.y), Mathf.Abs(ln.z));

        float p = Mathf.Max(0.01f, axisBlendSharpness);
        Vector3 w = new Vector3(
            Mathf.Pow(ln.x, p),
            Mathf.Pow(ln.y, p),
            Mathf.Pow(ln.z, p)
        );

        float wsum = w.x + w.y + w.z;
        if (wsum < 1e-6f) return;
        w /= wsum;

        float axX = amountBase * horizontalSquishMultiplier;
        float axY = amountBase * verticalSquishMultiplier;
        float axZ = amountBase * horizontalSquishMultiplier;

        float squashX = 1f - (axX * w.x);
        float squashY = 1f - (axY * w.y);
        float squashZ = 1f - (axZ * w.z);

        squashX = Mathf.Clamp(squashX, minAxisScale, 1f);
        squashY = Mathf.Clamp(squashY, minAxisScale, 1f);
        squashZ = Mathf.Clamp(squashZ, minAxisScale, 1f);

        float volume = squashX * squashY * squashZ;
        float expand = 1f / Mathf.Pow(Mathf.Max(0.0001f, volume), 1f / 3f);

        float expX = Mathf.Lerp(1f, expand, 1f - w.x);
        float expY = Mathf.Lerp(1f, expand, 1f - w.y);
        float expZ = Mathf.Lerp(1f, expand, 1f - w.z);

        Vector3 targetScale = new Vector3(
            baseScale.x * squashX * expX,
            baseScale.y * squashY * expY,
            baseScale.z * squashZ * expZ
        );

        _targetScale[target] = targetScale;
    }

    private void Update()
    {
        foreach (var target in deformTargets)
        {
            if (target == null) continue;
            if (!_defaultScale.TryGetValue(target, out var baseScale)) continue;
            if (!_targetScale.TryGetValue(target, out var tgtScale)) continue;

            target.localScale = Vector3.Lerp(target.localScale, tgtScale, Time.deltaTime * returnSpeed);
            _targetScale[target] = Vector3.Lerp(tgtScale, baseScale, Time.deltaTime * returnSpeed * 0.5f);
        }
    }
}
