// DestroyAfterEffect.cs
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class EffectDestroy : MonoBehaviour
{
    [Tooltip("true = GameObject.Destroy(gameObject), false = gameObject.SetActive(false) (object pooling)")]
    public bool destroyInsteadOfDisable = true;

    [Tooltip("最大待ち時間。0以下なら無制限に待ちます（安全のため大きめを推奨）")]
    public float maxWaitSeconds = 30f;

    void OnEnable()
    {
        StartCoroutine(WaitAndFinish());
    }

    IEnumerator WaitAndFinish()
    {
        float timer = 0f;

        // 1) ParticleSystem があればそれが終了するのを待つ（子も含める）
        var particleSystems = GetComponentsInChildren<ParticleSystem>();
        if (particleSystems != null && particleSystems.Length > 0)
        {
            // すべてのパーティクルが死ぬまで待つ
            bool anyAlive = true;
            while (anyAlive)
            {
                anyAlive = false;
                foreach (var ps in particleSystems)
                {
                    if (ps == null) continue;
                    // playing や alive を使って判定
                    if (ps.IsAlive(true))
                    {
                        anyAlive = true;
                        break;
                    }
                }
                if (!anyAlive) break;
                if (maxWaitSeconds > 0 && (timer += Time.deltaTime) > maxWaitSeconds) break;
                yield return null;
            }
        }

        // 2) AudioSource があれば再生終了を待つ（優先度は ParticleSystem に続く）
        var audioSources = GetComponentsInChildren<AudioSource>();
        if ((audioSources != null && audioSources.Length > 0) && (maxWaitSeconds <= 0f || timer < maxWaitSeconds))
        {
            bool anyPlaying = true;
            while (anyPlaying)
            {
                anyPlaying = false;
                foreach (var a in audioSources)
                {
                    if (a == null) continue;
                    if (a.isPlaying)
                    {
                        anyPlaying = true;
                        break;
                    }
                }
                if (!anyPlaying) break;
                if (maxWaitSeconds > 0 && (timer += Time.deltaTime) > maxWaitSeconds) break;
                yield return null;
            }
        }

        // 3) Animator（ループしない clip を持つ場合）はおおよその長さを待つ（存在すれば）
        var animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null && (maxWaitSeconds <= 0f || timer < maxWaitSeconds))
        {
            // 非ループのステートがあれば、その length を使う（厳密ではないが実用的）
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.loop == false)
            {
                float clipLength = info.length;
                if (clipLength > 0f)
                {
                    float wait = Mathf.Max(0f, clipLength - info.normalizedTime * clipLength);
                    float waited = 0f;
                    while (waited < wait)
                    {
                        if (maxWaitSeconds > 0 && (timer += Time.deltaTime) > maxWaitSeconds) break;
                        waited += Time.deltaTime;
                        yield return null;
                    }
                }
            }
        }

        // 最後に破棄 or 無効化
        if (destroyInsteadOfDisable)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    // エディタや外部から即時キャンセルしたいとき用の公開メソッド
    public void FinishNow()
    {
        if (destroyInsteadOfDisable) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
