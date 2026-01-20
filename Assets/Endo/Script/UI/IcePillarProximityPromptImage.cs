using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// IcePillar周囲のTriggerで「近づいたら殴るUI」を出す（超安定版）
/// - OnTriggerStayで「今入ってる」を維持
/// - Stayが一定時間来なかったら消す（Exit取りこぼし対策）
/// - 複数カプセル重なりでも消えない
/// </summary>
public class IcePillarProximityPromptImage : MonoBehaviour
{
    [Header("Target IcePillar (optional)")]
    [SerializeField] private IcePillar icePillar;

    [Header("Stay Lost Timeout")]
    [Tooltip("この秒数だけStayが来なければ範囲外とみなして消す")]
    [SerializeField, Min(0.05f)] private float stayTimeout = 0.15f;

    // この氷柱（このTrigger）の一意ID（表示要求元として使う）
    private ulong _sourceId;

    // ローカルプレイヤーごとの「最後にStayを受け取った時間」
    private readonly Dictionary<PlayerPunchPromptImage, float> _lastStayTime = new();

    private void Awake()
    {
        if (icePillar == null)
            icePillar = GetComponentInParent<IcePillar>();

        // sourceIdは「この氷柱のNetworkObjectId」がベスト
        // 無ければInstanceIDで代用
        var no = GetComponentInParent<Unity.Netcode.NetworkObject>();
        if (no != null) _sourceId = no.NetworkObjectId;
        else _sourceId = (ulong)GetInstanceID();
    }

    private void OnEnable()
    {
        if (icePillar != null)
            icePillar.OnBroken += HandleBroken;
    }

    private void OnDisable()
    {
        if (icePillar != null)
            icePillar.OnBroken -= HandleBroken;

        // このTriggerが無効化されたら、この氷柱分だけ全員からRemove
        RemoveThisSourceFromAll();
    }

    private void Update()
    {
        // Stayが途切れたプレイヤーを範囲外扱いでRemove
        if (_lastStayTime.Count == 0) return;

        float now = Time.time;

        // foreach中に削除するので一旦バッファ
        List<PlayerPunchPromptImage> removeList = null;

        foreach (var kv in _lastStayTime)
        {
            var prompt = kv.Key;
            float last = kv.Value;

            if (prompt == null || now - last > stayTimeout)
            {
                removeList ??= new List<PlayerPunchPromptImage>();
                removeList.Add(prompt);
            }
        }

        if (removeList != null)
        {
            for (int i = 0; i < removeList.Count; i++)
            {
                var p = removeList[i];
                if (p != null) p.RemoveSource(_sourceId);
                _lastStayTime.Remove(p);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        var prompt = other.GetComponentInParent<PlayerPunchPromptImage>();
        if (prompt == null) return;

        // ローカルプレイヤーだけ表示
        if (other.gameObject.GetComponent<NetworkObject>().OwnerClientId!=NetworkManager.Singleton.LocalClientId ) return;

        // Stayが来た＝今範囲内なので、時間を更新
        _lastStayTime[prompt] = Time.time;

        // まだこの氷柱ソースを追加してなければAdd
        prompt.AddSource(_sourceId);
    }

    private void OnTriggerExit(Collider other)
    {
        var prompt = other.GetComponentInParent<PlayerPunchPromptImage>();
        if (prompt == null) return;
        if (other.gameObject.GetComponent<NetworkObject>().OwnerClientId != NetworkManager.Singleton.LocalClientId) return;

        // Exitが来たら即Remove（でも来ない場合はUpdateのtimeoutで消える）
        prompt.RemoveSource(_sourceId);
        _lastStayTime.Remove(prompt);
    }

    private void HandleBroken()
    {
        // 氷柱が壊れたら、この氷柱分だけ全員からRemove
        RemoveThisSourceFromAll();
    }

    private void RemoveThisSourceFromAll()
    {
        foreach (var kv in _lastStayTime)
        {
            var p = kv.Key;
            if (p != null) p.RemoveSource(_sourceId);
        }
        _lastStayTime.Clear();
    }
}
