using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalPadSession : MonoBehaviour
{
	[Header("UI: Connection")]
	[SerializeField] private GameObject blurUI;
	[SerializeField] private GameObject connectUI;

	// 登録完了で出すキャラ（今までのやつ）
	[SerializeField] private PlayerJoinVisual gamepadUI1;
	[SerializeField] private PlayerJoinVisual gamepadUI2;

	// 「登録待ちシルエット」用（D案の1枚目/3枚目）
	// ※スクショの青いシルエット / オレンジのシルエットのGameObjectをここに入れて
	[SerializeField] private GameObject p1WaitingVisual;
	[SerializeField] private GameObject p2WaitingVisual;

	// 下のラインとか、必要なら切り替える用（無くても動く）
	[SerializeField] private GameObject bluelineUI;
	[SerializeField] private GameObject orangelineUI;

	[Header("UI: InGame (turn on when playing)")]
	[SerializeField] private GameObject[] inGameUIs;

	[Header("Start Condition")]
	[SerializeField] private float startTime = 1.0f;

	[Tooltip("参加ボタン：Dpad下。A(×)にしたいなら false にして buttonSouth")]
	[SerializeField] private bool useDpadDownToStart = true;

	// ---- GameManagerから登録される参照 ----
	private PlayerPadBinding p1Binding;
	private PlayerPadBinding p2Binding;
	private PlayerControlGate p1Gate;
	private PlayerControlGate p2Gate;

	[Header("SE")]
	[SerializeField] private string joinSfxTag = "SE_Decision";
	[SerializeField] private NetworkSoundManager.SoundScope joinSfxScope = NetworkSoundManager.SoundScope.LocalOnly;

	// Pad重複割当防止（兼・現在のアクティブなデバイス管理）
	private readonly HashSet<int> assigned = new();

	// 状態
	private bool inGameUI = false;
	private bool startedOnce = false;
	private bool waitingReconnect = false;

	// D案用：P1/P2の確定ID
	private int p1DeviceId = -1;
	private int p2DeviceId = -1;

	private bool startRequested = false;
	private bool isStarting = false;
	private Coroutine startCoroutine;

	private bool p1Shown = false;
	private bool p2Shown = false;



	public static LocalPadSession Instance { get; private set; }

	[Header("Debug")]
	[SerializeField] private bool IsDebug = false;

	private enum EntryState
	{
		WaitP1,     // 1枚目
		WaitP2,     // 3枚目
		BothReady,  // 4枚目状態（揃った）
		Starting,   // カウント中
		Started
	}
	[SerializeField] private EntryState entryState = EntryState.WaitP1;

	private void Awake()
	{
		Instance = this;
	}

	private void Start()
	{
		if (IsDebug)
		{
			// デバッグは強制開始
			RequestGameStart();
			return;
		}

		ResetEntry();
		ShowConnectionUI();
		ApplyEntryUI();
	}

	private void OnEnable()
	{
		if (IsDebug) return;
		InputSystem.onDeviceChange += OnDeviceChange;
		ApplyEntryUI();
	}

	private void OnDisable()
	{
		InputSystem.onDeviceChange -= OnDeviceChange;
	}

	private void LateUpdate()
	{
		if (IsDebug) return;
		if (inGameUI) return;

		// ゲーム開始後の復帰待ちは元のロジック寄せ
		if (startedOnce)
		{
			if (waitingReconnect && !inGameUI)
			{
				// ここは必要なら復帰処理を強化できるが、今は既存方針維持
				FinalizeResumeIfReady();
			}
			return;
		}

		// 開始処理中は入力受付しない
		if (isStarting) return;

		// エントリー処理
		HandleEntryByState();

		// UI反映
		ApplyEntryUI();

		// 両方揃ったら自動開始
		if (entryState == EntryState.BothReady && !isStarting)
		{
			StartAutoStart();
		}

		// RequestGameStart済みで、プレイヤー参照も揃ってたら確定へ
		TryFinalizeStartIfReady();
	}

	private void HandleEntryByState()
	{
		switch (entryState)
		{
			case EntryState.WaitP1:
				TryJoinP1();
				break;
			case EntryState.WaitP2:
				TryJoinP2();
				break;
		}
	}

	private void TryJoinP1()
	{
		// すでに埋まってるなら次へ
		if (p1DeviceId != -1)
		{
			entryState = EntryState.WaitP2;
			return;
		}

		foreach (var pad in Gamepad.all)
		{
			if (!IsUsable(pad)) continue;
			if (assigned.Contains(pad.deviceId)) continue;

			if (IsJoinPressed(pad))
			{
				p1DeviceId = pad.deviceId;
				assigned.Add(pad.deviceId);
				PlayJoinSe();

				// P1確定 → P2待ちへ
				entryState = EntryState.WaitP2;
				break;
			}
		}
	}

	private void TryJoinP2()
	{
		// P1未確定なら戻す（保険）
		if (p1DeviceId == -1)
		{
			entryState = EntryState.WaitP1;
			return;
		}

		// すでに埋まってるなら完了へ
		if (p2DeviceId != -1)
		{
			entryState = EntryState.BothReady;
			return;
		}

		foreach (var pad in Gamepad.all)
		{
			if (!IsUsable(pad)) continue;
			if (assigned.Contains(pad.deviceId)) continue; // P1と同じは弾く

			if (IsJoinPressed(pad))
			{
				p2DeviceId = pad.deviceId;
				assigned.Add(pad.deviceId);
				PlayJoinSe();

				// ★ここで即、P2完了表示を出す（4枚目にしたいならP1もついでに出す）
				if (p2WaitingVisual) p2WaitingVisual.SetActive(false);
				if (orangelineUI) orangelineUI.SetActive(false);

				if (gamepadUI1) { if (!gamepadUI1.gameObject.activeSelf) gamepadUI1.Show(); }
				if (gamepadUI2) { if (!gamepadUI2.gameObject.activeSelf) gamepadUI2.Show(); }

				entryState = EntryState.BothReady;
				break;
			}

		}
	}

	private bool IsJoinPressed(Gamepad pad)
	{
		return useDpadDownToStart
			? pad.dpad.down.wasPressedThisFrame
			: pad.buttonSouth.wasPressedThisFrame;
	}

	private void StartAutoStart()
	{
		if (startCoroutine != null) StopCoroutine(startCoroutine);
		startCoroutine = StartCoroutine(WaitAndStartSequence());
	}

	private IEnumerator WaitAndStartSequence()
	{
		isStarting = true;
		entryState = EntryState.Starting;

		// ここで「READY!」演出入れたければ好きにどうぞ
		yield return new WaitForSeconds(startTime);

		startRequested = true;
		RequestGameStart();

		isStarting = false;
		// 開始確定は TryFinalizeStartIfReady がやる
	}

	// =========================================================
	// GameManager -> LocalPadSession 登録口
	// =========================================================
	public void RegisterPlayers(GameObject p1Player, GameObject p2Player)
	{
		if (p1Player == null || p2Player == null) return;

		p1Binding = p1Player.GetComponent<PlayerPadBinding>() ?? p1Player.GetComponentInChildren<PlayerPadBinding>(true);
		p2Binding = p2Player.GetComponent<PlayerPadBinding>() ?? p2Player.GetComponentInChildren<PlayerPadBinding>(true);
		if (p1Binding == null || p2Binding == null) return;

		p1Gate = p1Player.GetComponent<PlayerControlGate>() ?? p1Player.GetComponentInChildren<PlayerControlGate>(true);
		p2Gate = p2Player.GetComponent<PlayerControlGate>() ?? p2Player.GetComponentInChildren<PlayerControlGate>(true);

		TryFinalizeStartIfReady();
	}

	// =========================================================
	// 初回開始確定
	// =========================================================
	private void TryFinalizeStartIfReady()
	{
		if (!startRequested) return;
		if (startedOnce) return;
		if (p1Binding == null || p2Binding == null) return;

		// D案：固定IDでバインド
		BindFixedPads();

		if (!HasBothBound())
		{
			Debug.LogWarning("[LocalPadSession] FinalizeStart: Pad割当失敗 (Binding情報不足)");
			return;
		}

		startedOnce = true;
		startRequested = false;
		waitingReconnect = false;
		entryState = EntryState.Started;

		SetGateLocked(p1Gate, false);
		SetGateLocked(p2Gate, false);

		ShowInGameUI();

		Debug.Log($"[LocalPadSession] Start Finalized: P1(ID:{p1Binding.DeviceId}), P2(ID:{p2Binding.DeviceId})");
	}

	private void BindFixedPads()
	{
		// ここは「ゲーム開始時に最終的に割り当て直す」用
		if (p1Binding != null) p1Binding.Unbind();
		if (p2Binding != null) p2Binding.Unbind();

		// P1
		if (p1DeviceId != -1)
		{
			var pad1 = Gamepad.all.FirstOrDefault(g => g.deviceId == p1DeviceId);
			if (pad1 != null && IsUsable(pad1))
			{
				TryBind(p1Binding, pad1);
			}
		}

		// P2
		if (p2DeviceId != -1)
		{
			var pad2 = Gamepad.all.FirstOrDefault(g => g.deviceId == p2DeviceId);
			if (pad2 != null && IsUsable(pad2))
			{
				TryBind(p2Binding, pad2);
			}
		}
	}

	// =========================================================
	// 切断 / 再接続
	// =========================================================
	private void OnDeviceChange(InputDevice device, InputDeviceChange change)
	{
		if (device is not Gamepad gp) return;

		if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
		{
			// 開始前に抜けた場合：登録を戻す
			if (!startedOnce)
			{
				if (gp.deviceId == p2DeviceId)
				{
					p2DeviceId = -1;
					assigned.Remove(gp.deviceId);
					entryState = EntryState.WaitP2;
					CancelAutoStart();
					return;
				}
				if (gp.deviceId == p1DeviceId)
				{
					p1DeviceId = -1;
					assigned.Remove(gp.deviceId);
					entryState = EntryState.WaitP1;
					CancelAutoStart();
					return;
				}
			}

			HandlePadDisconnected(gp);
		}
		else if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled)
		{
			if (startedOnce && waitingReconnect && !inGameUI)
				FinalizeResumeIfReady();
		}
	}

	private void CancelAutoStart()
	{
		startRequested = false;
		isStarting = false;
		if (startCoroutine != null)
		{
			StopCoroutine(startCoroutine);
			startCoroutine = null;
		}
	}

	private void HandlePadDisconnected(Gamepad gp)
	{
		if (!startedOnce) return;

		bool hit = false;

		if (p1Binding != null && p1Binding.HasPad && p1Binding.DeviceId == gp.deviceId)
		{
			assigned.Remove(gp.deviceId);
			p1Binding.Unbind();
			SetGateLocked(p1Gate, true);
			hit = true;
		}

		if (p2Binding != null && p2Binding.HasPad && p2Binding.DeviceId == gp.deviceId)
		{
			assigned.Remove(gp.deviceId);
			p2Binding.Unbind();
			SetGateLocked(p2Gate, true);
			hit = true;
		}

		if (hit)
		{
			waitingReconnect = true;
			ShowConnectionUI();
		}
	}

	private void FinalizeResumeIfReady()
	{
		if (!waitingReconnect) return;
		if (p1Binding == null || p2Binding == null) return;

		assigned.Clear();

		// できるだけ元のIDを戻す（簡易）
		var pad1 = Gamepad.all.FirstOrDefault(g => g.deviceId == p1DeviceId);
		var pad2 = Gamepad.all.FirstOrDefault(g => g.deviceId == p2DeviceId);

		if (!p1Binding.HasPad && pad1 != null) TryBind(p1Binding, pad1);
		if (!p2Binding.HasPad && pad2 != null) TryBind(p2Binding, pad2);

		if (!HasBothBound()) return;

		waitingReconnect = false;
		SetGateLocked(p1Gate, false);
		SetGateLocked(p2Gate, false);
		ShowInGameUI();
	}

	// =========================================================
	// Binding helpers
	// =========================================================
	private bool TryBind(PlayerPadBinding binding, Gamepad pad)
	{
		if (binding == null || pad == null) return false;
		if (!IsUsable(pad)) return false;

		binding.Bind(pad);
		return true;
	}

	private bool HasBothBound()
		=> p1Binding != null && p2Binding != null && p1Binding.HasPad && p2Binding.HasPad;

	private bool IsUsable(Gamepad pad) => pad != null && pad.added && pad.enabled;

	// =========================================================
	// UI helpers（D案の4画面をここで作る）
	// =========================================================
	private void ShowConnectionUI()
	{
		inGameUI = false;
		if (blurUI) blurUI.SetActive(true);
		if (connectUI) connectUI.SetActive(true);
		if (inGameUIs != null) foreach (var ui in inGameUIs) if (ui) ui.SetActive(false);
	}

	private void ShowInGameUI()
	{
		inGameUI = true;
		if (blurUI) blurUI.SetActive(false);
		if (connectUI) connectUI.SetActive(false);

		if (p1WaitingVisual) p1WaitingVisual.SetActive(false);
		if (p2WaitingVisual) p2WaitingVisual.SetActive(false);

		if (gamepadUI1) gamepadUI1.Hide();
		if (gamepadUI2) gamepadUI2.Hide();

		if (inGameUIs != null) foreach (var ui in inGameUIs) if (ui) ui.SetActive(true);
	}

	private void ApplyEntryUI()
	{
		if (startedOnce) return;

		// 一旦全部オフのベース
		if (p1WaitingVisual) p1WaitingVisual.SetActive(false);
		if (p2WaitingVisual) p2WaitingVisual.SetActive(false);

		// lineは「待ちのほうだけON」くらいの雑制御（いらなきゃ消してOK）
		if (bluelineUI) bluelineUI.SetActive(false);
		if (orangelineUI) orangelineUI.SetActive(false);

		// キャラ表示（完了）側
		// Showを連打するとアニメが再発火する可能性あるので activeSelfでガード
		if (gamepadUI1 && gamepadUI1.gameObject.activeSelf) { /*そのまま*/ }
		if (gamepadUI2 && gamepadUI2.gameObject.activeSelf) { /*そのまま*/ }

		switch (entryState)
		{
			case EntryState.WaitP1:
				// 1枚目：P1待ち
				if (p1WaitingVisual) p1WaitingVisual.SetActive(true);
				if (bluelineUI) bluelineUI.SetActive(true);

				// P1完了は隠す（まだ）
				if (gamepadUI1) gamepadUI1.Hide();
				if (gamepadUI2) gamepadUI2.Hide();
				break;

			case EntryState.WaitP2:
				// 2枚目/3枚目：P1は完了、P2待ち
				if (p2WaitingVisual) p2WaitingVisual.SetActive(true);
				if (orangelineUI) orangelineUI.SetActive(true);

				// P1完了絵
				if (gamepadUI1 && !gamepadUI1.gameObject.activeSelf) gamepadUI1.Show();
				// P2はまだ
				if (gamepadUI2) gamepadUI2.Hide();
				break;

			case EntryState.BothReady:
			case EntryState.Starting:
				// P1は演出済みのはずなので毎フレームShowしない
				// （Showがアニメをリスタートするタイプだと再表示し続ける）
				if (gamepadUI1 && !p1Shown) { gamepadUI1.Show(); p1Shown = true; }

				// P2は「activeSelfガードが効かない」可能性があるので自前フラグで1回だけShow
				if (gamepadUI2 && !p2Shown) { gamepadUI2.Show(); p2Shown = true; }
				break;


		}
	}

	private void ResetEntry()
	{
		assigned.Clear();
		p1DeviceId = -1;
		p2DeviceId = -1;
		entryState = EntryState.WaitP1;

		startRequested = false;
		isStarting = false;
		startedOnce = false;
		waitingReconnect = false;

		if (startCoroutine != null)
		{
			StopCoroutine(startCoroutine);
			startCoroutine = null;
		}

		if (gamepadUI1) gamepadUI1.Hide();
		if (gamepadUI2) gamepadUI2.Hide();
	}

	private void PlayJoinSe()
	{
		if (NetworkSoundManager.Instance == null) return;
		if (string.IsNullOrEmpty(joinSfxTag)) return;

		NetworkSoundManager.Instance.PlaySfx(
			joinSfxTag,
			joinSfxScope,
			spatial: false
		);
	}

	private void SetGateLocked(PlayerControlGate gate, bool locked)
	{
		if (gate != null) gate.SetLocked(locked);
	}

	private void RequestGameStart()
	{
		if (GameManager.Instance == null) return;
		GameManager.Instance.StartLocalGameRequest();
	}
}
