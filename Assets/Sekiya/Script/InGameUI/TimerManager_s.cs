using System;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class TimeManager_s : NetworkBehaviour
{
    [SerializeField]
    private NetworkVariable<float> _count = new NetworkVariable<float>(
    0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );

    [SerializeField] private TMP_Text timeText;

    private void Start()
    {
        timeText = GetComponent<TMP_Text>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 例: サーバーで初期値セット
        if (IsServer)
        {
            Debug.Log("スポーンされたよ");
            //_count.Value = 180f; // 3分とか
        }
    }

    void Update()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            _count.Value -= Time.deltaTime;
            if (_count.Value < 0) _count.Value = 0; // マイナス防止
        }


        // 分:秒 表記
        var span = TimeSpan.FromSeconds(_count.Value);
        timeText.text = span.ToString(@"mm\:ss");
    }
}