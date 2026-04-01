using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapIcon : MonoBehaviour
{
    [SerializeField] private Camera _mapCamera;
    [SerializeField] RectTransform _mapRect;
    [SerializeField] RectTransform _playerIcon;
    [SerializeField] Transform _player = null;

    // 自動検出用のリスト
    private readonly List<Transform> _playerList = new();

    //インターバル
    private float _updateInterval = 0.1f;

    // UniTask 用
    private CancellationTokenSource _cts;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        

    }

    // === UniTask で位置更新を回すループ ===
    private async UniTaskVoid StartUpdateLoop(CancellationToken token)
    {
        // OnEnable のたびに一回だけ回り始める
        while (!token.IsCancellationRequested)
        {
            Debug.Log("回してます");
            UpdateIcon();

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

    // Update is called once per frame
    
    private void UpdateIcon()
    {
        RegistPlayers();
        if (_playerList == null) return;


        foreach (Transform t in _playerList)
        {
            //mapCameraから見たプレイヤーの位置をRectの0～1の位置に変換
            Vector3 vp = _mapCamera.WorldToViewportPoint(t.position);


            // RectTransform 上の座標へ変換
            float px = (vp.x - 0.5f) * _mapRect.rect.width;
            float pz = (vp.y - 0.5f) * _mapRect.rect.height;

            _playerIcon.anchoredPosition = new Vector2(px, pz);

            // アイコンの角度もつける場合
            _playerIcon.localEulerAngles = new Vector3(0, 0, t.eulerAngles.y);
        }
    }

    private void RegistPlayers()
    {

        _playerList.Clear();

        if (GameManager.Instance._IsLanModeActive == true)
        {

            foreach (var playerRef in GameManager.Instance._networkObjectList)
            {
                //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
                //成功した場合 playerObj に GameObject が入る。

                if (playerRef.TryGet(out var playerObj))
                {
                    if (playerObj.IsLocalPlayer == true)
                    {
                        Debug.Log("所有権を持ったプレイヤーです");
                        _playerList.Add(playerObj.gameObject.transform);
                    }
                }

            }
        }
        else
        {
            // 2) なければタグで自動検出
            var found = GameObject.FindGameObjectsWithTag("Player");
            foreach (var go in found)
            {
                if (go != null)
                    _playerList.Add(go.transform);
            }
        }

    }

}
