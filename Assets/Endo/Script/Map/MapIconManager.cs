using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class MapIconManager : MonoBehaviour
{

    [Header("アイコン定義データベース")]
    [SerializeField] private MapIconDatabase _iconDatabase;

    [Header("マップ用カメラ")]
    [SerializeField] private Camera _mapCamera;

    [Header("ミニマップのRectTransform (ここにアイコンをぶら下げる)")]
    [SerializeField] private RectTransform _mapRect;

    [Header("アイコン用のUIプレハブ (Image付き)")]
    [SerializeField] private GameObject _iconUIPrefab;

    [Header("アイコン座標更新間隔（秒） 0なら毎フレーム")]
    [SerializeField] private float _updateInterval = 0.05f;

    // ==== static 管理（全 MapIconTarget からここに登録される） ====
    private static MapIconManager _instance;
    private static readonly HashSet<MapIconTarget> _registeredTargets = new();

    // ==== インスタンスごとに管理：Target → UI RectTransform ====
    private readonly Dictionary<MapIconTarget, RectTransform> _iconInstances = new();

    // UniTask 用
    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[MapIconManager] シーンに複数存在しています。最初のインスタンスのみ使用されます。");
        }

        _instance = this;

        if (_iconDatabase == null)
        {
            Debug.LogWarning("[MapIconManager] _iconDatabase が設定されていません。");
        }
        if (_mapCamera == null)
        {
            Debug.LogWarning("[MapIconManager] _mapCamera が設定されていません。");
        }
        if (_mapRect == null)
        {
            Debug.LogWarning("[MapIconManager] _mapRect が設定されていません。");
        }
        if (_iconUIPrefab == null)
        {
            Debug.LogWarning("[MapIconManager] _iconUIPrefab が設定されていません。");
        }
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

        // （必要なら）ここでアイコン全部消す処理を入れてもOK
        // CleanupAllIcons();
    }

    // === UniTask で位置更新を回すループ ===
    private async UniTaskVoid StartUpdateLoop(CancellationToken token)
    {
        // OnEnable のたびに一回だけ回り始める
        while (!token.IsCancellationRequested)
        {
            Debug.Log("回してます");
            UpdateIcons();

            if (_updateInterval <= 0f)
            {
                // 毎フレーム更新（デフォルトのUpdateタイミング）
                await UniTask.NextFrame(token);
            }
            else
            {
                // 一定間隔ごとに更新
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_updateInterval),
                    cancellationToken: token
                );
            }
        }
    }


    // === MapIconTarget から呼ばれる静的メソッド ===
    public static void Register(MapIconTarget target)
    {
        if (target == null) return;
        _registeredTargets.Add(target);

        // すでにインスタンスがあれば、その Manager 側にも教えておく
        if (_instance != null)
        {
            _instance.EnsureIconForTarget(target);
        }
    }

    public static void Unregister(MapIconTarget target)
    {
        if (target == null) return;
        _registeredTargets.Remove(target);

        if (_instance != null)
        {
            _instance.RemoveIconForTarget(target);
        }
    }

    // === インスタンス側の実装 ===

    /// <summary>
    /// まだアイコンを持っていない Target にアイコンを生成して紐づける
    /// </summary>
    private void EnsureIconForTarget(MapIconTarget target)
    {
        

        if (target == null) return;
        if (_iconInstances.ContainsKey(target)) return;
        if (_iconDatabase == null || _mapRect == null || _iconUIPrefab == null) return;

        // 名前 → Icon を ScriptableObject に全部任せる
        Sprite iconSprite = _iconDatabase.GetIconByObjectName(target.GetSearchName());
        if (iconSprite == null) return;

        var iconGO = Instantiate(_iconUIPrefab, _mapRect);
        var rect = iconGO.GetComponent<RectTransform>();
        var img = iconGO.GetComponent<Image>();

        if (img != null)
        {
            img.sprite = iconSprite;
        }

        _iconInstances[target] = rect;
    }

    /// <summary>
    /// Unregister 時などに呼び出されるアイコン削除処理
    /// </summary>
    private void RemoveIconForTarget(MapIconTarget target)
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

    /// <summary>
    /// 毎フレーム：登録済みオブジェクトのアイコン生成＆座標更新
    /// </summary>
    private void UpdateIcons()
    {
        if (_mapCamera == null || _mapRect == null) return;

        // 死んだターゲットを後でまとめて消す
        var deadTargets = new List<MapIconTarget>();

        foreach (var target in _registeredTargets)
        {
            if (target == null)
            {
                deadTargets.Add(target);
                continue;
            }

            // アイコンがまだないなら作る
            EnsureIconForTarget(target);

            // アイコンインスタンスが無ければ（データベースに無いなど）、スキップ
            if (!_iconInstances.TryGetValue(target, out var rect) || rect == null)
                continue;

            // useIcon フラグに従って表示切り替え
            if (!target.useIcon)
            {
                rect.gameObject.SetActive(false);
                continue;
            }
            else
            {
                rect.gameObject.SetActive(true);
            }

            // ====== ここが指定してくれた変換ロジック ======
            Vector3 worldPos = target.transform.position;
            Vector3 vp = _mapCamera.WorldToViewportPoint(worldPos);

            // カメラ裏に行った時などの挙動は必要ならここに条件追加
            // if (vp.z < 0f) { rect.gameObject.SetActive(false); continue; }

            float px = (vp.x - 0.5f) * _mapRect.rect.width;
            float py = (vp.y - 0.5f) * _mapRect.rect.height;

            //Debug.Log($"[MapIcon] {target.name} vp={vp}, px={px}, py={py}");

            rect.anchoredPosition = new Vector2(px, py);
        }

        // null になったターゲットの掃除
        foreach (var dead in deadTargets)
        {
            _registeredTargets.Remove(dead);
            RemoveIconForTarget(dead);
        }
    }

}
