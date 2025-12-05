using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

public class PlayerMapIconManager : MonoBehaviour
{

    [SerializeField] private Camera _mapCamera;
    [SerializeField] private RectTransform _mapRect;

    [Header("プレイヤーアイコン用のUIプレハブ（Image付き）")]
    [SerializeField] private GameObject _playerIconPrefab;

    [Header("位置更新の間隔（秒） 0なら毎フレーム")]
    [SerializeField] private float _updateInterval = 0.1f;

    // ==== static 管理（プレイヤーから Register/Unregister される） ====
    private static PlayerMapIconManager _instance;
    private static readonly HashSet<PlayerMapIconTarget> _targets = new();

    // ==== 各ターゲット → アイコンRect ====
    private readonly Dictionary<PlayerMapIconTarget, RectTransform> _iconInstances = new();

    // UniTask 用
    private CancellationTokenSource _cts;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[PlayerMapIconManager] 複数存在しています。最初のインスタンスのみ使用されます。");
        }
        _instance = this;
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        StartUpdateLoop(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // 必要ならここでアイコン全部消してもOK
        // CleanupAllIcons();
    }


    // === UniTask ループ ===

    private async UniTaskVoid StartUpdateLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                UpdateIcons();

                if (_updateInterval <= 0f)
                {
                    await UniTask.NextFrame(token);
                }
                else
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_updateInterval),
                        cancellationToken: token
                    );
                }
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセル時は無視
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerMapIconManager] StartUpdateLoop Exception: {ex}");
        }
    }


    // === 位置更新本体 ===

    private void UpdateIcons()
    {
        if (_mapCamera == null || _mapRect == null) return;

        var deadTargets = new List<PlayerMapIconTarget>();

        foreach (var target in _targets)
        {
            if (target == null || target.IconTarget == null)
            {
                deadTargets.Add(target);
                continue;
            }

            // 新しく追加されたプレイヤーはここでアイコン生成
            EnsureIconForTarget(target);

            if (!_iconInstances.TryGetValue(target, out var rect) || rect == null)
                continue;

            // 位置変換
            var t = target.IconTarget;
            Vector3 vp = _mapCamera.WorldToViewportPoint(t.position);

            float px = (vp.x - 0.5f) * _mapRect.rect.width;
            float pz = (vp.y - 0.5f) * _mapRect.rect.height;

            rect.anchoredPosition = new Vector2(px, pz);

            // 向きも反映したい場合（Y回転をZに）
            rect.localEulerAngles = new Vector3(0f, 0f, -t.eulerAngles.y);
            // ↑プラスかマイナスかはマップの向きによってお好みで
        }

        // 死んだターゲットの掃除
        foreach (var dead in deadTargets)
        {
            _targets.Remove(dead);
            RemoveIconForTarget(dead);
        }
    }

    //プレイヤーアイコンクリア
    private void CleanupAllIcons()
    {
        foreach (var kv in _iconInstances)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
        _iconInstances.Clear();
    }

    // === プレイヤー側から呼ばれる静的メソッド ===

    public static void Register(PlayerMapIconTarget target)
    {
        if (target == null) return;
        _targets.Add(target);

        if (_instance != null)
        {
            _instance.EnsureIconForTarget(target);
        }
        else
        {
            Debug.LogWarning("[PlayerMapIconManager] Registerされたが、インスタンスがまだない");
        }
    }

    public static void Unregister(PlayerMapIconTarget target)
    {
        if (target == null) return;
        _targets.Remove(target);

        if (_instance != null)
        {
            _instance.RemoveIconForTarget(target);
        }
    }

    // === アイコン生成・削除 ===

    private void EnsureIconForTarget(PlayerMapIconTarget target)
    {
        if (target == null) return;
        if (_iconInstances.ContainsKey(target)) return;
        if (_playerIconPrefab == null || _mapRect == null) return;

        var iconGO = Instantiate(_playerIconPrefab, _mapRect);
        var rect = iconGO.GetComponent<RectTransform>();
        if (rect == null)
        {
            Debug.LogWarning("[PlayerMapIconManager] プレイヤーアイコンPrefabに RectTransform が付いていません");
            Destroy(iconGO);
            return;
        }

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector2.zero;

        _iconInstances[target] = rect;
    }

    private void RemoveIconForTarget(PlayerMapIconTarget target)
    {
        if (target == null) return;

        if (_iconInstances.TryGetValue(target, out var rect))
        {
            if (rect != null)
            {
                Destroy(rect.gameObject);
            }
            _iconInstances.Remove(target);
        }
    }

}
