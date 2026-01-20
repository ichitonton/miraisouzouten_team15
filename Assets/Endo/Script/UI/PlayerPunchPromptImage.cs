using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// プレイヤー頭上に「殴る画像」を表示する（重なりTrigger対応版）
/// - 表示要求元（氷柱など）を sources で管理
/// - sources が1つでもあれば表示
/// - sources が0になったら非表示
/// </summary>
public class PlayerPunchPromptImage : NetworkBehaviour
{
    [Header("Prompt Prefab")]
    [SerializeField] private GameObject promptPrefab;

    [Header("Anchor (Head)")]
    [SerializeField] private Transform headAnchor;

    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.2f, 0f);

    private GameObject _instance;

    // 表示要求元（氷柱など）のID集合
    private readonly HashSet<ulong> _sources = new();

    private void Start()
    {
        if (headAnchor == null) headAnchor = transform;
    }

    /// <summary>
    /// ある氷柱（sourceId）から「表示して」と言われた
    /// </summary>
    public void AddSource(ulong sourceId)
    {
        if (!IsOwner) return;

        bool added = _sources.Add(sourceId);
        if (!added) return; // 同じsourceが二重Enterしても増えない

        Refresh();
    }

    /// <summary>
    /// ある氷柱（sourceId）から「消して」と言われた
    /// </summary>
    public void RemoveSource(ulong sourceId)
    {
        if (!IsOwner) return;

        bool removed = _sources.Remove(sourceId);
        if (!removed) return; // 既に無いなら何もしない

        Refresh();
    }

    /// <summary>
    /// 強制的に全部消す（保険）
    /// </summary>
    public void ForceHideAll()
    {
        if (!IsOwner) return;

        _sources.Clear();
        Refresh();
    }

    /// <summary>
    /// sources状態に応じて表示を切り替える
    /// </summary>
    private void Refresh()
    {
        if (_sources.Count > 0)
        {
            ShowInternal();
        }
        else
        {
            HideInternal();
        }
    }

    private void ShowInternal()
    {
        if (_instance != null) return;

        if (promptPrefab == null)
        {
            Debug.LogWarning("[PlayerPunchPromptImage] promptPrefab が未設定です");
            return;
        }

        _instance = Instantiate(promptPrefab, headAnchor.position + offset, Quaternion.identity);
        _instance.transform.SetParent(headAnchor, worldPositionStays: true);
    }

    private void HideInternal()
    {
        if (_instance != null)
        {
            Destroy(_instance);
            _instance = null;
        }
    }
}
