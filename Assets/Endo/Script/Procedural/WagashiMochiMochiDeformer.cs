using System.Collections;
using UnityEngine;

[RequireComponent(typeof(WagashiRuntime))]
public class WagashiMochiMochiDeformer : MonoBehaviour
{

    [Header("衝突判定パラメータ")]
    [SerializeField] private float _minImpactVelocity = 1.5f;   // これ以下の弱い衝突は無視
    [SerializeField] private float _maxImpactVelocity = 8f;     // ここで強さを頭打ち

    [Header("スクイッシュ量（基準）")]
    [Tooltip("衝突が最大のときの基準ムニ量")]
    [SerializeField] private float _baseSquishAmount = 0.4f;

    [Header("縦/横でのスクイッシュ倍率")]
    [Tooltip("Y方向に潰れる量の倍率（上から押す感じ）")]
    [SerializeField] private float _verticalSquishMultiplier = 1.25f;

    [Tooltip("X/Z方向に潰れる量の倍率（横から押す感じ）")]
    [SerializeField] private float _horizontalSquishMultiplier = 0.85f;

    [Header("ブレンド（斜め衝突の自然さ）")]
    [Tooltip("大きいほど支配軸寄り、小さいほど均等ブレンド。おすすめ 2-6")]
    [SerializeField] private float _axisBlendSharpness = 3.5f;

    [Header("戻りスピード")]
    [SerializeField] private float _returnSpeed = 8f;           // 値を大きくすると早く元の形に戻る

    [Header("つぶれ下限（潰れすぎ防止）")]
    [SerializeField] private float _minAxisScale = 0.35f;

    private WagashiRuntime _runtime;
    private Vector3 _defaultScale;
    private Vector3 _targetScale;

    private void Awake()
    {
        _runtime = GetComponent<WagashiRuntime>();
        if( _runtime == null ) Debug.LogError($"{name}: WagashiRuntime がないです");
    }

    private void Start()
    {
        // RandomizerがStartでスケールを変える可能性があるので、1フレーム待って確定値を取る
        StartCoroutine(CaptureDefaultScaleNextFrame());
    }

    // Update is called once per frame
    private void Update()
    {

        if (_defaultScale == Vector3.zero) return;

        // スケールをターゲットへ
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * _returnSpeed);

        // ターゲット自体をデフォルトへ戻す（ぷに…が残る）
        _targetScale = Vector3.Lerp(_targetScale, _defaultScale, Time.deltaTime * _returnSpeed * 0.5f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_defaultScale == Vector3.zero) return;

        float impact = collision.relativeVelocity.magnitude;
        if (impact < _minImpactVelocity) return;

        float t = Mathf.InverseLerp(_minImpactVelocity, _maxImpactVelocity, impact);
        t = Mathf.Clamp01(t);

        float mochiFactor = 1f + Mathf.Max(0f, _runtime.currentStretchFactor); // 個体差
        float amount = _baseSquishAmount * t * mochiFactor;
        amount = Mathf.Clamp(amount, 0f, 0.75f);

        // 衝突の代表法線を取る（平均）
        Vector3 normal = AverageContactNormal(collision);
        if (normal.sqrMagnitude < 1e-6f) return;

        ApplyDirectionalSquish(amount, normal);
    }

    //当たり判定から法線方向を求める
    private Vector3 AverageContactNormal(Collision collision)
    {
        // contacts が取れないケースはほぼ無いけど保険
        if (collision.contactCount <= 0) return Vector3.up;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < collision.contactCount; i++)
        {
            sum += collision.GetContact(i).normal;
        }
        return (sum / collision.contactCount).normalized;
    }
    /// <summary>
    /// 法線方向（当たった面の向き）に潰す：上からならY潰す、横からならX/Z潰す
    /// </summary>
    private void ApplyDirectionalSquish(float amount, Vector3 worldNormal)
    {
        // 法線をローカルに変換して、どの軸で潰すべきか決める
        Vector3 localN = transform.InverseTransformDirection(worldNormal).normalized;
        localN = new Vector3(Mathf.Abs(localN.x), Mathf.Abs(localN.y), Mathf.Abs(localN.z));

        // 0除算回避
        float sum = localN.x + localN.y + localN.z;
        if (sum < 1e-6f) return;

        // ブレンドのシャープさ：成分を指数で強調（支配軸寄りにしたいほど大きく）
        float p = Mathf.Max(0.01f, _axisBlendSharpness);
        Vector3 w = new Vector3(
            Mathf.Pow(localN.x, p),
            Mathf.Pow(localN.y, p),
            Mathf.Pow(localN.z, p)
        );

        float wsum = w.x + w.y + w.z;
        if (wsum < 1e-6f) return;
        w /= wsum; // w.x + w.y + w.z = 1

        // 縦/横の潰れ量倍率を適用
        float axX = amount * _horizontalSquishMultiplier;
        float axY = amount * _verticalSquishMultiplier;
        float axZ = amount * _horizontalSquishMultiplier;

        // 軸の最終潰れ量（ブレンド）
        // 例：上からなら w.y が大きくなってY潰れが強くなる
        float squashX = 1f - (axX * w.x);
        float squashY = 1f - (axY * w.y);
        float squashZ = 1f - (axZ * w.z);

        // 潰れすぎ防止
        squashX = Mathf.Clamp(squashX, _minAxisScale, 1f);
        squashY = Mathf.Clamp(squashY, _minAxisScale, 1f);
        squashZ = Mathf.Clamp(squashZ, _minAxisScale, 1f);

        // 体積っぽさ維持：潰した分、他方向に膨らませる（掛け算でざっくり）
        // 全体の体積比（おおよそ） = squashX*squashY*squashZ
        float volume = squashX * squashY * squashZ;

        // volumeが小さいほど膨らませる。立方根で全方向に均等に戻す
        float expand = 1f / Mathf.Pow(Mathf.Max(0.0001f, volume), 1f / 3f);

        // 「全部をexpandすると潰れが薄まる」ので、潰してない軸を中心に少し膨らむように調整
        // 重みが小さいほど膨らませたい → (1 - w) を使う
        float expX = Mathf.Lerp(1f, expand, 1f - w.x);
        float expY = Mathf.Lerp(1f, expand, 1f - w.y);
        float expZ = Mathf.Lerp(1f, expand, 1f - w.z);

        Vector3 s = _defaultScale;

        _targetScale = new Vector3(
            s.x * squashX * expX,
            s.y * squashY * expY,
            s.z * squashZ * expZ
        );
    }

    private IEnumerator CaptureDefaultScaleNextFrame()
    {
        yield return null; // 1 frame
        _defaultScale = transform.localScale;
        _targetScale = _defaultScale;
    }

}
