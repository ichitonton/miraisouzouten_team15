using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class Time_Manager : NetworkBehaviour
{
	[Header("カウントダウン初期値")]
	[SerializeField] private int countdownMinutes = 3;
	[SerializeField] private int countdownSecondsExtra = 0;

   
	private float countdownSeconds;
	[SerializeField] private TMP_Text timeText;

	[SerializeField] private  NetworkVariable<float> _count  = new NetworkVariable<float>(
		0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
        );



	private void Start()
	{
		timeText = GetComponent<TMP_Text>();


		countdownSeconds = (countdownMinutes * 60) + countdownSecondsExtra;
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

            // 分:秒 表記
            var span = TimeSpan.FromSeconds(_count.Value);
            timeText.text = span.ToString(@"mm\:ss");
        }
        else if (!NetworkManager.Singleton.IsServer)
        {

            
            // 分:秒 表記
            var span = TimeSpan.FromSeconds(_count.Value);
            timeText.text = span.ToString(@"mm\:ss");


        }
    }
}