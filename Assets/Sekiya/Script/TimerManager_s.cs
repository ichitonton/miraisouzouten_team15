using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JetBrains.Annotations;
using System.Security.Cryptography.X509Certificates;

public class TimeManager_s : MonoBehaviour
{
    [SerializeField] SceneChangerWithSoundInvoke scenechange;

    [Header("カウントダウン初期値")]
	[SerializeField] private float countdownMinutes = 3;
	[SerializeField] private int countdownSecondsExtra = 0;

    private float countdownSeconds;
	[SerializeField] private TMP_Text timeText;

	private void Start()
	{
		timeText = GetComponent<TMP_Text>();
		countdownSeconds = (countdownMinutes * 60) + countdownSecondsExtra;
	}

	void Update()
	{
		countdownSeconds -= Time.deltaTime;
		if (countdownSeconds < 0) countdownSeconds = 0; // マイナス防止

		// 分:秒 表記
		var span = TimeSpan.FromSeconds(countdownSeconds);
		timeText.text = span.ToString(@"mm\:ss");

		if (countdownSeconds <= 0)
		{
			// 0秒になったときの処理
			scenechange.LoadNextScene();
        }
	}
}