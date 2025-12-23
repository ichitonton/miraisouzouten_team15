using System.Collections;
using UnityEngine;
#if UNITY_VISUAL_EFFECT_GRAPH
using UnityEngine.VFX;
#endif

public class PooledEffect : MonoBehaviour
{
    [Header("Auto Return")]
    [SerializeField] private float delay = 0f;

    [Tooltip("安全装置。何かの理由で終わらない時に強制返却する最大時間")]
    [SerializeField] private float maxLifeTime = 10f;

    private NetworkEffectSpawner _owner;
    private int _effectId;
    private Coroutine _co;

    public void Setup(NetworkEffectSpawner owner, int effectId)
    {
        _owner = owner;
        _effectId = effectId;
    }

    void OnEnable()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoAutoReturn());
    }

    void OnDisable()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
    }

    IEnumerator CoAutoReturn()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        PlayAll();

        float t = 0f;
        while (t < maxLifeTime)
        {
            if (!IsAliveAny())
                break;

            t += Time.deltaTime;
            yield return null;
        }

        // プールに返却（無ければ破棄でもOK）
        if (_owner != null)
            _owner.ReturnToPool(gameObject, _effectId);
        else
            gameObject.SetActive(false);
    }

    bool IsAliveAny()
    {
        // ParticleSystem
        var ps = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < ps.Length; i++)
            if (ps[i] != null && ps[i].IsAlive(true)) return true;

#if UNITY_VISUAL_EFFECT_GRAPH
        // VFX Graph は「終わった」を取るのが難しいので、必要なら maxLifeTime で返す運用が安定
        var vfx = GetComponentsInChildren<VisualEffect>(true);
        for (int i = 0; i < vfx.Length; i++)
            if (vfx[i] != null && vfx[i].aliveParticleCount > 0) return true;
#endif

        // AudioSource が鳴ってる間は生存扱いにしたいならここに追加してもOK
        return false;
    }

    void PlayAll()
    {
        foreach (var p in GetComponentsInChildren<ParticleSystem>(true))
            p.Play(true);

#if UNITY_VISUAL_EFFECT_GRAPH
        foreach (var v in GetComponentsInChildren<VisualEffect>(true))
            v.Play();
#endif

        foreach (var a in GetComponentsInChildren<AudioSource>(true))
            a.Play();
    }
}
