using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// IcePillar周囲のTriggerで「近づいたら殴るUI」を出す（超安定版）
/// - OnTriggerStayで「今入ってる」を維持
/// - Stayが一定時間来なかったら消す（Exit取りこぼし対策）
/// - 氷柱が壊れたら即消して、二度と出ないようにする
/// </summary>
public class IcePillarProximityPromptImage : MonoBehaviour
{
    [Header("Target IcePillar (optional)")]
    [SerializeField] private IcePillar icePillar;

    [Header("Stay Lost Timeout")]
    [Tooltip("この秒数だけStayが来なければ範囲外とみなして消す")]
    [SerializeField, Min(0.05f)] private float stayTimeout = 0.15f;

    private ulong _sourceId;
    private readonly Dictionary<PlayerPunchPromptImage, float> _lastStayTime = new();

    // ★追加：壊れたら二度とAddしない
    private bool _pillarBroken = false;

    // ★追加：自分のTriggerCollider（無効化用）
    private Collider _myTriggerCol;

    private void Awake()
    {
        if (icePillar == null)
            icePillar = GetComponentInParent<IcePillar>();

        var no = GetComponentInParent<NetworkObject>();
        _sourceId = (no != null) ? no.NetworkObjectId : (ulong)GetInstanceID();

        _myTriggerCol = GetComponent<Collider>(); // ここに付いてるカプセル想定
    }

    private void OnEnable()
    {
        _pillarBroken = false;

        if (icePillar != null)
            icePillar.OnBroken += HandleBroken;
    }

    private void OnDisable()
    {
        if (icePillar != null)
            icePillar.OnBroken -= HandleBroken;

        RemoveThisSourceFromAll();
    }

    private void Update()
    {
        if (_pillarBroken) return;  // ★壊れた後は何もしない
        if (_lastStayTime.Count == 0) return;

        float now = Time.time;

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
        if (_pillarBroken) return; // ★ここが一番重要

        var prompt = other.GetComponentInParent<PlayerPunchPromptImage>();
        if (prompt == null) return;

        // ローカルプレイヤーだけ表示
        if (!prompt.IsOwner) return;

        _lastStayTime[prompt] = Time.time;
        prompt.AddSource(_sourceId);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_pillarBroken) return; // ★壊れた後はExitも無視

        var prompt = other.GetComponentInParent<PlayerPunchPromptImage>();
        if (prompt == null) return;

        if (!prompt.IsOwner) return;

        prompt.RemoveSource(_sourceId);
        _lastStayTime.Remove(prompt);
    }

    private void HandleBroken()
    {
        if (_pillarBroken) return;

        _pillarBroken = true;

        // 氷柱が壊れた瞬間に必ず消す
        RemoveThisSourceFromAll();

        // ★最強：このTriggerそのものを無効化（Stayが二度と来ない）
        if (_myTriggerCol != null)
            _myTriggerCol.enabled = false;

        // もしくはスクリプトごと止めてもOK（好きな方で）
        // enabled = false;
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
