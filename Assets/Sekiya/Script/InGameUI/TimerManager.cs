using System;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class TimerManager : NetworkBehaviour
{
    [SerializeField] private NetworkVariable<float> _count = new NetworkVariable<float>(180f);
	[SerializeField] private float startSeconds = 300f;

	[SerializeField] private TMP_Text timeText;

    [SerializeField] private SceneChangerNetwork scenechange;
    [SerializeField] private float _delay = 1f;
    private bool isTimeUp = false;

    // ★追加：タイマー動作フラグ（サーバーが管理、全員に同期）
    private NetworkVariable<bool> _isRunning = new NetworkVariable<bool>(false);

    void Update()
    {
        // UI更新は常に（止まってても表示したい）
        var span = TimeSpan.FromSeconds(_count.Value);
        if (timeText) timeText.text = span.ToString(@"m\:ss");

        if (!IsServer) return;
        if (isTimeUp) return;

        // ★追加：開始ボタン押されるまで減らさない
        if (!_isRunning.Value) return;

        _count.Value -= Time.deltaTime;

        if (_count.Value <= 0)
        {
            _count.Value = 0;
            isTimeUp = true;

			_isRunning.Value = false;

			// ▼追加：タイムアップSE
			if (NetworkSoundManager.Instance != null)
			{
				NetworkSoundManager.Instance.PlaySfx(
					"SE_TimeUP",
					NetworkSoundManager.SoundScope.AllClients,
			false
		);
			}

			FinishGame();
        }
    }

    // ★ホストの開始ボタンから呼ぶ用
    [ServerRpc(RequireOwnership = false)]
	public void StartTimerServerRpc()
	{
		if (_isRunning.Value) return;

		_count.Value = startSeconds;   // ★ここ！
		isTimeUp = false;
		_isRunning.Value = true;
	}


	private void FinishGame()
    {
        float r = 0, b = 0, w = 0;

        var goals = FindObjectsByType<GoalToUI>(FindObjectsSortMode.None);
        foreach (var goal in goals)
        {
            if (goal.targetPlayerId == 0) r = goal.Score;
            else if (goal.targetPlayerId == 1) b = goal.Score;
            else if (goal.targetPlayerId == 2) w = goal.Score;
        }

        SetScoreClientRpc(r, b, w);

        UIEventManager.Instance.OnHideSceneUI();

        VideoFadeManager.Instance._videoIndex = 1;
        VideoFadeManager.Instance.fadeInDuration = 1.1f;
        VideoFadeManager.Instance.fadeOutDuration = 1.1f;

        StartCoroutine(DelayChangeScene());

    }

    [ClientRpc]
    private void SetScoreClientRpc(float red, float blue, float white)
    {
        FinalScore.ScoreTeam0 = red;
        FinalScore.ScoreTeam1 = blue;
        FinalScore.ScoreTeam2 = white;

        Debug.Log($"スコア届いたよ！ R:{red} B:{blue} W:{white}");
    }

    private IEnumerator DelayChangeScene()
    {

        yield return new WaitForSeconds(_delay);

        scenechange.ChangeScene();

    }

}
