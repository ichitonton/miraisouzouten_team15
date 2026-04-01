using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class BombManager : NetworkBehaviour
{
    [Header("Fuse")]
    [SerializeField] private float fuseSeconds = 3f;

    [Header("Explosion")]
    [SerializeField] private float radius = 3f;
    [SerializeField] private float explosionForce = 900f;
    [SerializeField] private float upwardsModifier = 0.2f;
    [SerializeField] private LayerMask affectLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Blink (faster near explosion)")]
    [SerializeField] private Color blinkRed = Color.red;

    [Tooltip("点滅開始(秒)。3なら生成直後から。1なら爆発1秒前から。")]
    [SerializeField] private float blinkStartSeconds = 3f;

    [Tooltip("点滅周波数(Hz) 生成直後のゆっくり")]
    [SerializeField] private float blinkHzSlow = 2f;

    [Tooltip("点滅周波数(Hz) 爆発直前の速い")]
    [SerializeField] private float blinkHzFast = 12f;

    [Header("Toon Shader Property Names (あなたのシェーダー)")]
    [SerializeField] private string baseColorProp = "_BaseColor";
    [SerializeField] private string midColorProp = "_MidColor";
    [SerializeField] private string shadowColorProp = "_ShadowColor";

    // サーバーが爆発予定時刻(NetworkTime)を共有
    private readonly NetworkVariable<double> detonateNetworkTime = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 爆発は1回だけ
    private bool exploded;

    // OverlapSphereNonAlloc
    private Collider[] hits = new Collider[64];

    // 点滅対象Renderer
    private Renderer[] renderers;
    private MaterialPropertyBlock mpb;

    // Shader property IDs
    private int baseId, midId, shadowId;

    // Rendererごとの元色と、プロパティ存在フラグ
    private struct ToonOriginal
    {
        public bool hasBase, hasMid, hasShadow;
        public Color baseC, midC, shadowC;
    }
    private ToonOriginal[] originals;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CacheRenderersAndOriginals();

        if (IsServer)
        {
            detonateNetworkTime.Value = NetworkManager.ServerTime.Time + fuseSeconds;
        }
    }

    private void CacheRenderersAndOriginals()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();

        baseId = Shader.PropertyToID(baseColorProp);
        midId = Shader.PropertyToID(midColorProp);
        shadowId = Shader.PropertyToID(shadowColorProp);

        // 子も含めて点滅（爆弾モデルが子にある想定）
        renderers = GetComponentsInChildren<Renderer>(true);
        originals = new ToonOriginal[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            var ren = renderers[i];
            var mat = ren != null ? ren.sharedMaterial : null;

            ToonOriginal o = default;

            if (mat != null)
            {
                o.hasBase = mat.HasProperty(baseId);
                o.hasMid = mat.HasProperty(midId);
                o.hasShadow = mat.HasProperty(shadowId);

                if (o.hasBase) o.baseC = mat.GetColor(baseId);
                if (o.hasMid) o.midC = mat.GetColor(midId);
                if (o.hasShadow) o.shadowC = mat.GetColor(shadowId);
            }

            originals[i] = o;
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;
        if (detonateNetworkTime.Value <= 0) return;

        double nowNet = NetworkManager.LocalTime.Time; // 各端末のNetworkTime基準
        float remaining = (float)(detonateNetworkTime.Value - nowNet);

        UpdateBlink(remaining, nowNet);

        if (IsServer && !exploded && remaining <= 0f)
        {
            exploded = true;
            ExplodeServer();
        }
    }

    private void UpdateBlink(float remaining, double nowNet)
    {
        // 点滅する時間帯（例：blinkStartSeconds=3なら生成直後から）
        float start = Mathf.Clamp(blinkStartSeconds, 0f, fuseSeconds);
        if (start <= 0f) return;

        // remaining が start より大きい間は点滅しない
        if (remaining > start)
        {
            ApplyToonBlink(on: false); // 元色に戻す
            return;
        }

        // 0(点滅開始) -> 1(爆発直前)
        float t = Mathf.InverseLerp(start, 0f, Mathf.Clamp(remaining, 0f, start));

        // 近づくほど速く
        float hz = Mathf.Lerp(blinkHzSlow, blinkHzFast, t);

        // NetworkTime基準の位相でON/OFF（端末間のズレが小さい）
        // 0.5周期で反転
        double phase = (nowNet * hz) % 1.0;
        bool on = phase < 0.5;

        ApplyToonBlink(on);
    }

    private void ApplyToonBlink(bool on)
    {
        if (renderers == null || originals == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var ren = renderers[i];
            if (ren == null) continue;

            var o = originals[i];
            if (!o.hasBase && !o.hasMid && !o.hasShadow) continue;

            ren.GetPropertyBlock(mpb);

            if (o.hasBase) mpb.SetColor(baseId, on ? blinkRed : o.baseC);
            if (o.hasMid) mpb.SetColor(midId, on ? blinkRed : o.midC);
            if (o.hasShadow) mpb.SetColor(shadowId, on ? blinkRed : o.shadowC);

            ren.SetPropertyBlock(mpb);
        }
    }

    private void ExplodeServer()
    {
        Vector3 pos = transform.position;

        int count = Physics.OverlapSphereNonAlloc(pos, radius, hits, affectLayers, triggerInteraction);

        for (int i = 0; i < count; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null) continue;

            if(hits[i].gameObject.CompareTag("Player"))
            {
                hits[i].gameObject.GetComponent<MovePlayerKey>().Stun(2f);
            }

            rb.AddExplosionForce(explosionForce, pos, radius, upwardsModifier, ForceMode.Impulse);
        }

        //エフェクトの再生
        NetworkEffectSpawner.Instance.PlayEffect(2, pos, Quaternion.identity, new Vector3(radius,radius,radius));

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
