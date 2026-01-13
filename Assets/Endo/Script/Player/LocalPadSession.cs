using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalPadSession : MonoBehaviour
{
    [Header("UI: Connection")]
    [SerializeField] private GameObject blurUI;
    [SerializeField] private GameObject connectUI;
    [SerializeField] private GameObject gamepadUI1;
    [SerializeField] private GameObject gamepadUI2;

    [Header("UI: InGame (turn on when playing)")]
    [SerializeField] private GameObject[] inGameUIs;

    [Header("Start Condition")]
    [SerializeField] private int requiredGamepads = 2;

    [Tooltip("開始ボタン：Dpad下。A(×)にしたいなら false にして buttonSouth")]
    [SerializeField] private bool useDpadDownToStart = true;

    // ---- GameManagerから登録される参照（Inspector不要） ----
    private PlayerPadBinding p1Binding;
    private PlayerPadBinding p2Binding;
    private PlayerControlGate p1Gate;
    private PlayerControlGate p2Gate;

    // Pad重複割当防止
    private readonly HashSet<int> assigned = new();

    // 状態
    private bool inGameUI = false;          // InGame UIを出してるか
    private bool startRequested = false;    // Start押下済み（プレイヤー生成待ち）
    private bool startedOnce = false;       // 開始確定済み（一度開始した）
    private bool waitingReconnect = false;  // 切断復帰待ち

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
        if(IsDebug) return;
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
        if (!inGameUI) RefreshPadCountUI();
        if (inGameUI) return;

        // 初回開始：Start押下で GameManager に開始要求 → RegisterPlayers待ち
        if (!startedOnce)
        {
            if (!startRequested)
            {
                if (AvailablePadCount() < requiredGamepads) return;
                if (!AnyPadPressedStart()) return;

                startRequested = true;
                RequestGameStart();
                Debug.Log("[LocalPadSession] Start requested -> waiting RegisterPlayers()");
            }

            TryFinalizeStartIfReady();
            return;
        }

        // 復帰：Padが揃ったら即復帰（Start押し不要）
        if (waitingReconnect)
        {
            FinalizeResumeIfReady();
        }
    }

    // =========================================================
    // ★ GameManager -> LocalPadSession 登録口（これがメイン）
    // =========================================================
    public void RegisterPlayers(GameObject p1Player, GameObject p2Player)
    {
        if (p1Player == null || p2Player == null)
        {
            Debug.LogError("[LocalPadSession] RegisterPlayers: p1/p2 が null");
            return;
        }

        p1Binding = p1Player.GetComponent<PlayerPadBinding>() ?? p1Player.GetComponentInChildren<PlayerPadBinding>(true);
        p2Binding = p2Player.GetComponent<PlayerPadBinding>() ?? p2Player.GetComponentInChildren<PlayerPadBinding>(true);

        if (p1Binding == null || p2Binding == null)
        {
            Debug.LogError("[LocalPadSession] RegisterPlayers: PlayerPadBinding が見つからない（プレイヤーに付けてね）");
            return;
        }

        p1Gate = p1Player.GetComponent<PlayerControlGate>() ?? p1Player.GetComponentInChildren<PlayerControlGate>(true);
        p2Gate = p2Player.GetComponent<PlayerControlGate>() ?? p2Player.GetComponentInChildren<PlayerControlGate>(true);

        Debug.Log($"[LocalPadSession] RegisterPlayers OK: P1={p1Player.name} P2={p2Player.name}");

        // Start要求済みなら開始確定を試す
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
        if (AvailablePadCount() < requiredGamepads) return;

        BindFirstTwoPads();

        if (!HasBothBound())
        {
            Debug.LogWarning("[LocalPadSession] FinalizeStart: Pad割当失敗");
            return;
        }

        startedOnce = true;
        startRequested = false;
        waitingReconnect = false;

        SetGateLocked(p1Gate, false);
        SetGateLocked(p2Gate, false);

        ShowInGameUI();

        Debug.Log($"[LocalPadSession] Start Finalized: P1={p1Binding.DeviceId}, P2={p2Binding.DeviceId}");
    }

    // =========================================================
    // 切断→接続UIへ
    // =========================================================
    private void HandlePadDisconnected(Gamepad gp)
    {
        if (!startedOnce) return; // まだゲーム開始してないなら無視

        bool hit = false;

        if (p1Binding != null && p1Binding.HasPad && p1Binding.DeviceId == gp.deviceId)
        {
            assigned.Remove(gp.deviceId);
            p1Binding.Unbind();
            SetGateLocked(p1Gate, true);
            hit = true;
            Debug.Log("[LocalPadSession] P1 pad disconnected");
        }

        if (p2Binding != null && p2Binding.HasPad && p2Binding.DeviceId == gp.deviceId)
        {
            assigned.Remove(gp.deviceId);
            p2Binding.Unbind();
            SetGateLocked(p2Gate, true);
            hit = true;
            Debug.Log("[LocalPadSession] P2 pad disconnected");
        }

        if (hit)
        {
            waitingReconnect = true;
            ShowConnectionUI();
        }
    }

    // =========================================================
    // 再接続→InGameへ（Padが揃ったら即復帰）
    // =========================================================
    private void FinalizeResumeIfReady()
    {
        if (!waitingReconnect) return;
        if (p1Binding == null || p2Binding == null) return;

        // assigned を再構築（ズレ対策）
        assigned.Clear();
        if (p1Binding.HasPad) assigned.Add(p1Binding.DeviceId);
        if (p2Binding.HasPad) assigned.Add(p2Binding.DeviceId);

        // Missing を埋める
        AutoFillMissing();

        if (!HasBothBound()) return;

        waitingReconnect = false;

        SetGateLocked(p1Gate, false);
        SetGateLocked(p2Gate, false);

        ShowInGameUI();
        Debug.Log($"[LocalPadSession] Resume Finalized: P1={p1Binding.DeviceId}, P2={p2Binding.DeviceId}");
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

    // =========================================================
    // Device change
    // =========================================================
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad gp) return;

        // UIカウントは常に更新（接続画面中）
        if (!inGameUI) RefreshPadCountUI();

        // ★抜けた時：接続UIへ
        if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)
        {
            HandlePadDisconnected(gp);
            return;
        }

        // ★戻ってきた時：復帰を試す
        if (change == InputDeviceChange.Added ||
            change == InputDeviceChange.Reconnected ||
            change == InputDeviceChange.Enabled)
        {
            // まだ開始してないならUI更新だけでOK
            if (!startedOnce) return;

            // 復帰待ちなら復帰を試す
            if (waitingReconnect && !inGameUI)
                FinalizeResumeIfReady();
        }
    }

    // =========================================================
    // Binding helpers
    // =========================================================
    private void BindFirstTwoPads()
    {
        assigned.Clear();
        p1Binding.Unbind();
        p2Binding.Unbind();

        int bound = 0;
        foreach (var pad in Gamepad.all)
        {
            if (!IsUsable(pad)) continue;

            if (bound == 0)
            {
                if (TryBind(p1Binding, pad)) bound++;
            }
            else if (bound == 1)
            {
                if (TryBind(p2Binding, pad)) bound++;
            }

            if (bound >= 2) break;
        }
    }

    private bool TryBind(PlayerPadBinding binding, Gamepad pad)
    {
        if (binding == null || pad == null) return false;
        if (!IsUsable(pad)) return false;
        if (assigned.Contains(pad.deviceId)) return false;

        if (binding.HasPad) assigned.Remove(binding.DeviceId);

        binding.Bind(pad);
        assigned.Add(pad.deviceId);
        return true;
    }

    private bool HasBothBound()
        => p1Binding != null && p2Binding != null && p1Binding.HasPad && p2Binding.HasPad;

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

    private bool IsUsable(Gamepad pad)
        => pad != null && pad.added && pad.enabled;

    // =========================================================
    // UI helpers
    // =========================================================
    private void ShowConnectionUI()
    {
        inGameUI = false;

        if (blurUI) blurUI.SetActive(true);
        if (connectUI) connectUI.SetActive(true);

        if (inGameUIs != null)
            foreach (var ui in inGameUIs)
                if (ui) ui.SetActive(false);

        RefreshPadCountUI();
    }

    private void ShowInGameUI()
    {
        inGameUI = true;

        if (blurUI) blurUI.SetActive(false);
        if (connectUI) connectUI.SetActive(false);
        if (gamepadUI1) gamepadUI1.SetActive(false);
        if (gamepadUI2) gamepadUI2.SetActive(false);

        if (inGameUIs != null)
            foreach (var ui in inGameUIs)
                if (ui) ui.SetActive(true);
    }

    private void RefreshPadCountUI()
    {
        // まだプレイヤー登録前（初回開始前）なら「接続台数」で表示
        if (p1Binding == null || p2Binding == null || !startedOnce)
        {
            int c = AvailablePadCount();
            if (gamepadUI1) gamepadUI1.SetActive(c >= 1);
            if (gamepadUI2) gamepadUI2.SetActive(c >= 2);
            return;
        }

        // ゲーム開始後 / 登録済みなら「割当状態」で表示（どっちが抜けたか分かる）
        if (gamepadUI1) gamepadUI1.SetActive(p1Binding.HasPad);
        if (gamepadUI2) gamepadUI2.SetActive(p2Binding.HasPad);
    }

    private int AvailablePadCount()
    {
        int count = 0;
        foreach (var p in Gamepad.all)
            if (IsUsable(p)) count++;
        return count;
    }

    private bool AnyPadPressedStart()
    {
        foreach (var pad in Gamepad.all)
        {
            if (!IsUsable(pad)) continue;

            if (useDpadDownToStart)
            {
                if (pad.dpad.down.wasPressedThisFrame) return true;
            }
            else
            {
                if (pad.buttonSouth.wasPressedThisFrame) return true;
            }
        }
        return false;
    }

    private void SetGateLocked(PlayerControlGate gate, bool locked)
    {
        if (gate != null) gate.SetLocked(locked);
    }

    // =========================================================
    // GameManager bridge
    // =========================================================
    private void RequestGameStart()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[LocalPadSession] GameManager.Instance が見つからない");
            return;
        }

        GameManager.Instance.StartLocalGameRequest();
    }
}
