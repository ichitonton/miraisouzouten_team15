using System.Collections;
using System.Collections.Generic;
using System.Linq; // List.Containsなどで使用
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalPadSession : MonoBehaviour
{
    [Header("UI: Connection")]
    [SerializeField] private GameObject blurUI;
    [SerializeField] private GameObject connectUI;
    // ★変更: GameObject型ではなく、作ったスクリプトの型にする
    [SerializeField] private PlayerJoinVisual gamepadUI1;
    [SerializeField] private PlayerJoinVisual gamepadUI2;

    [SerializeField] private GameObject bluelineUI;
    [SerializeField] private GameObject orangelineUI;



    [Header("UI: InGame (turn on when playing)")]
    [SerializeField] private GameObject[] inGameUIs;

    [Header("Start Condition")]
    [SerializeField] private int requiredGamepads = 2;

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


	// ★変更点1: 参加確定したデバイスIDを順番に保持するリスト
	private List<int> joinedDeviceIds = new List<int>();

    // Pad重複割当防止（兼・現在のアクティブなデバイス管理）
    private readonly HashSet<int> assigned = new();

    //一秒待機中のボタン押下防止
    private bool isStarting = false;

    [SerializeField]private float startTime = 1.0f;

    // 状態
    private bool inGameUI = false;
    private bool startRequested = false;
    private bool startedOnce = false;
    private bool waitingReconnect = false;

    public static LocalPadSession Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool IsDebug = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (IsDebug) RequestGameStart();
        else ShowConnectionUI();
    }

    private void OnEnable()
    {
        if (IsDebug) return;
        InputSystem.onDeviceChange += OnDeviceChange;
        RefreshPadCountUI();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void LateUpdate()
    {
        if (IsDebug) return;
        if (inGameUI) return;

        // --- 初回開始待ち（エントリー画面） ---
        if (!startedOnce)
        {
            // 入力受付は「開始要求も待機もしていない」ときだけ行う
            if (!startRequested && !isStarting)
            {
                HandlePlayerEntry();
            }
            RefreshPadCountUI();

            // 開始要求済みなら確定処理へ
            if (startRequested)
            {
                TryFinalizeStartIfReady();
            }
            return;
        }

        // ... (復帰待ちはそのまま)
    }

    private void HandlePlayerEntry()
    {
        // ★変更: ここでは RequestGameStart を即呼ばずに、コルーチンを開始する
        if (joinedDeviceIds.Count >= requiredGamepads)
        {
            // まだコルーチンが走っていなければ開始
            if (!isStarting)
            {
                StartCoroutine(WaitAndStartSequence());
            }
            return;
        }

        foreach (var pad in Gamepad.all)
        {
            if (!IsUsable(pad)) continue;
            if (joinedDeviceIds.Contains(pad.deviceId)) continue;

            bool pressed = useDpadDownToStart
                ? pad.dpad.down.wasPressedThisFrame
                : pad.buttonSouth.wasPressedThisFrame;

            if (pressed)
            {
                joinedDeviceIds.Add(pad.deviceId);
                Debug.Log($"[LocalPadSession] Player {joinedDeviceIds.Count} Joined! (DeviceID: {pad.deviceId})");

                // ★追加: もしこの参加で人数が揃ったら、直後にコルーチンへ
                if (joinedDeviceIds.Count >= requiredGamepads)
                {
                    if (!isStarting) StartCoroutine(WaitAndStartSequence());
                }
            }
        }
    }


    // =========================================================
    // ★ GameManager -> LocalPadSession 登録口
    // =========================================================
    public void RegisterPlayers(GameObject p1Player, GameObject p2Player)
    {
        if (p1Player == null || p2Player == null) return;

        p1Binding = p1Player.GetComponent<PlayerPadBinding>() ?? p1Player.GetComponentInChildren<PlayerPadBinding>(true);
        p2Binding = p2Player.GetComponent<PlayerPadBinding>() ?? p2Player.GetComponentInChildren<PlayerPadBinding>(true);

        if (p1Binding == null || p2Binding == null) return;

        p1Gate = p1Player.GetComponent<PlayerControlGate>() ?? p1Player.GetComponentInChildren<PlayerControlGate>(true);
        p2Gate = p2Player.GetComponent<PlayerControlGate>() ?? p2Player.GetComponentInChildren<PlayerControlGate>(true);

        Debug.Log($"[LocalPadSession] RegisterPlayers OK");

        TryFinalizeStartIfReady();
    }

    private IEnumerator WaitAndStartSequence()
    {
        isStarting = true; // ガードをかける（これ以上エントリー操作を受け付けない）

        Debug.Log("Players ready! Starting in 1 second...");

        // ここで「READY!」などの演出UIを出したり、決定音を鳴らすと親切です
        // if (readyUI) readyUI.SetActive(true); 

        // 1秒待機
        yield return new WaitForSeconds(startTime);

        // 待機完了後に本来の開始処理
        startRequested = true;
        RequestGameStart();

        Debug.Log("[LocalPadSession] Request Game Start sent.");
    }

    // =========================================================
    // 初回開始確定
    // =========================================================
    private void TryFinalizeStartIfReady()
    {
        if (!startRequested) return;
        if (startedOnce) return;
        if (p1Binding == null || p2Binding == null) return;

        // ★変更: エントリーリストを使ってバインドする
        BindOrderedPads();

        if (!HasBothBound())
        {
            Debug.LogWarning("[LocalPadSession] FinalizeStart: Pad割当失敗 (Binding情報不足)");
            return;
        }

        startedOnce = true;
        startRequested = false;
        waitingReconnect = false;

        SetGateLocked(p1Gate, false);
        SetGateLocked(p2Gate, false);

        ShowInGameUI();

        Debug.Log($"[LocalPadSession] Start Finalized: P1(ID:{p1Binding.DeviceId}), P2(ID:{p2Binding.DeviceId})");
    }

    // =========================================================
    // 切断→接続UIへ
    // =========================================================
    private void HandlePadDisconnected(Gamepad gp)
    {
        if (!startedOnce) return;

        bool hit = false;
        // 切断時、Bindingからは外すが、joinedDeviceIds から消すかどうかは仕様による
        // 今回は「ゲーム中」なので一時停止扱いとし、IDは保持したまま再接続を待つ形が自然

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

    // =========================================================
    // 再接続→InGameへ
    // =========================================================
    private void FinalizeResumeIfReady()
    {
        if (!waitingReconnect) return;
        if (p1Binding == null || p2Binding == null) return;

        assigned.Clear();
        // 既存の接続を確認
        if (p1Binding.HasPad) assigned.Add(p1Binding.DeviceId);
        if (p2Binding.HasPad) assigned.Add(p2Binding.DeviceId);

        // ★復帰時は「元々P1だったID」を探して割り当て直すのが理想だが、
        // 簡易的に「空いているパッドを割り当てる」なら AutoFillMissing でOK
        // もし厳密にID一致させるなら joinedDeviceIds を使うロジックにする
        AutoFillMissing();

        if (!HasBothBound()) return;

        waitingReconnect = false;
        SetGateLocked(p1Gate, false);
        SetGateLocked(p2Gate, false);

        ShowInGameUI();
    }

    private void AutoFillMissing()
    {
        if (!p1Binding.HasPad)
        {
            var pad = PickUnassignedPad();
            if (pad != null) TryBind(p1Binding, pad);
        }
        if (!p2Binding.HasPad)
        {
            var pad = PickUnassignedPad();
            if (pad != null) TryBind(p2Binding, pad);
        }
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad gp) return;

        if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
        {
            // エントリー中（開始前）に抜けた場合、リストから削除してやり直しさせる
            if (!startedOnce && joinedDeviceIds.Contains(gp.deviceId))
            {
                joinedDeviceIds.Remove(gp.deviceId);
                RefreshPadCountUI();
                return;
            }
            HandlePadDisconnected(gp);
        }
        else if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled)
        {
            if (startedOnce && waitingReconnect && !inGameUI)
                FinalizeResumeIfReady();
        }
    }

    // =========================================================
    // ★変更点3: Binding helpers (順番指定版)
    // =========================================================
    private void BindOrderedPads()
    {
        assigned.Clear();
        p1Binding.Unbind();
        p2Binding.Unbind();

        // 参加リスト(joinedDeviceIds)の順番通りに割り当てる

        // P1の割り当て
        if (joinedDeviceIds.Count > 0)
        {
            // IDからGamepadインスタンスを探す
            var pad1 = Gamepad.all.FirstOrDefault(g => g.deviceId == joinedDeviceIds[0]);
            if (pad1 != null && IsUsable(pad1))
            {
                TryBind(p1Binding, pad1);
            }
        }

        // P2の割り当て
        if (joinedDeviceIds.Count > 1)
        {
            var pad2 = Gamepad.all.FirstOrDefault(g => g.deviceId == joinedDeviceIds[1]);
            if (pad2 != null && IsUsable(pad2))
            {
                TryBind(p2Binding, pad2);
            }
        }
    }

    private bool TryBind(PlayerPadBinding binding, Gamepad pad)
    {
        if (binding == null || pad == null) return false;
        if (!IsUsable(pad)) return false;
        if (assigned.Contains(pad.deviceId)) return false;

        // すでに持ってる場合一旦外す処理は省略（ここでは新規割り当て前提）
        binding.Bind(pad);
        assigned.Add(pad.deviceId);
        return true;
    }

    private bool HasBothBound() => p1Binding != null && p2Binding != null && p1Binding.HasPad && p2Binding.HasPad;

    private Gamepad PickUnassignedPad()
    {
        foreach (var pad in Gamepad.all)
        {
            if (!IsUsable(pad)) continue;
            if (assigned.Contains(pad.deviceId)) continue;
            return pad;
        }
        return null;
    }

    private bool IsUsable(Gamepad pad) => pad != null && pad.added && pad.enabled;

    // =========================================================
    // UI helpers
    // =========================================================
    private void ShowConnectionUI()
    {
        inGameUI = false;
        if (blurUI) blurUI.SetActive(true);
        if (connectUI) connectUI.SetActive(true);
        if (inGameUIs != null) foreach (var ui in inGameUIs) if (ui) ui.SetActive(false);
        RefreshPadCountUI();
    }

    private void ShowInGameUI()
    {
        inGameUI = true;
        if (blurUI) blurUI.SetActive(false);
        if (connectUI) connectUI.SetActive(false);
        if (gamepadUI1) gamepadUI1.Hide();
        if (gamepadUI2) gamepadUI2.Hide();
        if (inGameUIs != null) foreach (var ui in inGameUIs) if (ui) ui.SetActive(true);
    }

    private void RefreshPadCountUI()
    {
        // まだ開始前（エントリー画面）の場合
        if (!startedOnce)
        {
            int joinedCount = joinedDeviceIds.Count;

            // P1の表示制御
            if (gamepadUI1 != null)
            {
                // 参加済み かつ まだ表示されてなければ Show()、そうでなければ Hide()
                // ※ ここで毎回Showを呼ぶとアニメーションし続けてしまうので、
                // 「アクティブじゃなかったらShowする」というガードを入れると良いです

                bool shouldShow = (joinedCount >= 1);

                if (shouldShow && !gamepadUI1.gameObject.activeSelf)
                {
                    bluelineUI.SetActive(false);
					PlayJoinSe();
					gamepadUI1.Show(); // ★アニメーション開始！

                }
                else if (!shouldShow && gamepadUI1.gameObject.activeSelf)
                {
                    gamepadUI1.Hide();
                }
            }

            // P2の表示制御
            if (gamepadUI2 != null)
            {
                bool shouldShow = (joinedCount >= 2);
                if (shouldShow) Debug.Log("P2を表示しようとしています！");

                if (shouldShow && !gamepadUI2.gameObject.activeSelf)
                {
                    orangelineUI.SetActive(false);
					PlayJoinSe();
					gamepadUI2.Show(); // ★アニメーション開始！
                }
                else if (!shouldShow && gamepadUI2.gameObject.activeSelf)
                {
                    gamepadUI2.Hide();
                }
            }
            return;
        }
        // ゲーム開始後（既存ロジックの修正）
        // Bindingがあるなら表示状態にする（アニメーションは不要なら強制表示でもOKですが、Showでも問題ないです）
        if (gamepadUI1)
        {
            if (p1Binding != null && p1Binding.HasPad) { if (!gamepadUI1.gameObject.activeSelf) gamepadUI1.Show(); }
            else gamepadUI1.Hide();
        }

        if (gamepadUI2)
        {
            if (p2Binding != null && p2Binding.HasPad) { if (!gamepadUI2.gameObject.activeSelf) gamepadUI2.Show(); }
            else gamepadUI2.Hide();
        }
    }
	private void PlayJoinSe()
	{
		if (NetworkSoundManager.Instance == null) return;
		if (string.IsNullOrEmpty(joinSfxTag)) return;

		// UIなので 2D（spatial=false）
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