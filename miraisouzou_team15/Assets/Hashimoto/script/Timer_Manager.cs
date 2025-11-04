using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Time_Manager : MonoBehaviour
{
	[Header("カウントダウン初期値")]
	[SerializeField] private int countdownMinutes = 3;
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

		}
	}
}