using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 金平糖：一定時間後に消える + 直前に点滅（徐々に速く）
/// - Server: 寿命を管理して Despawn
/// - Client: 見た目（Alpha点滅）だけローカルで再生
/// </summary>

public class KonpeitouAutoVanish : NetworkBehaviour
{
    [Header("Life")]
    [SerializeField, Min(0.1f)] private float lifeSeconds = 60f;
    [SerializeField, Min(0f)] private float blinkBeforeSeconds = 10f;

    [Header("Blink Speed (Hz)")]
    [SerializeField, Min(0.1f)] private float blinkHzStart = 1.0f;
    [SerializeField, Min(0.1f)] private float blinkHzEnd = 12.0f;

    [Header("Alpha Range")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlpha = 0.15f;  // 点滅の最低透明度
    [SerializeField] private bool alsoFadeOutToZeroAtEnd = true;
    [SerializeField, Min(0f)] private float finalFadeSeconds = 0.3f;

    [Header("Renderers")]
    [Tooltip("空なら子も含めて自動取得")]
    [SerializeField] private Renderer[] targetRenderers;

    private readonly NetworkVariable<double> spawnServerTime =
        new(writePerm: NetworkVariableWritePermission.Server);

    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private Color _baseColorCached = Color.white;
    private bool _hasBaseColor;

    public override void OnNetworkSpawn()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        _mpb = new MaterialPropertyBlock();

        CacheBaseColor();

        if (IsServer)
            spawnServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
    }

    private void CacheBaseColor()
    {
        _hasBaseColor = false;
        if (targetRenderers == null || targetRenderers.Length == 0) return;

        var mat = targetRenderers[0].sharedMaterial;
        if (mat == null) return;

        if (mat.HasProperty(BaseColorID))
        {
            _hasBaseColor = true;
            _baseColorCached = mat.GetColor(BaseColorID);
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;
        if (spawnServerTime.Value <= 0) return;

        double now = NetworkManager.Singleton.ServerTime.Time;
        double elapsed = now - spawnServerTime.Value;

        double blinkStartAt = Mathf.Max(0f, lifeSeconds - blinkBeforeSeconds);

        if (elapsed >= lifeSeconds)
        {
            ApplyAlpha(1f); // 念のため戻す

            if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
                gameObject.GetComponent<PooledNetworkObject>().DestroySelf();

            return;
        }

        if (elapsed < blinkStartAt)
        {
            ApplyAlpha(1f);
            return;
        }

        // 点滅区間：時間が進むほど周波数UP
        float t01 = (float)((elapsed - blinkStartAt) / Mathf.Max(0.0001f, (float)(lifeSeconds - blinkStartAt)));
        float hz = Mathf.Lerp(blinkHzStart, blinkHzEnd, t01);

        // 0..1
        float wave = (Mathf.Sin((float)elapsed * Mathf.PI * 2f * hz) + 1f) * 0.5f;

        // minAlpha..1
        float alpha = Mathf.Lerp(minAlpha, 1f, wave);

        // 終わり際に0へ落とす（より自然に消える）
        if (alsoFadeOutToZeroAtEnd && finalFadeSeconds > 0f)
        {
            float remain = (float)(lifeSeconds - elapsed);
            if (remain < finalFadeSeconds)
            {
                float k = Mathf.Clamp01(remain / finalFadeSeconds); // 1→0
                alpha *= k;
            }
        }

        ApplyAlpha(alpha);
    }

    private void ApplyAlpha(float a)
    {
        if (!_hasBaseColor || targetRenderers == null) return;

        a = Mathf.Clamp01(a);

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);

            Color c = _baseColorCached;
            c.a = a;
            _mpb.SetColor(BaseColorID, c);

            r.SetPropertyBlock(_mpb);
        }
    }

}
