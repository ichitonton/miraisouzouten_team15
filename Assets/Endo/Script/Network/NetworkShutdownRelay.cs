using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkShutdownRelay : NetworkBehaviour
{
    public static NetworkShutdownRelay Instance { get; private set; }

    [Header("Hostが全員切断する時の待ち（RPC到達用）")]
    [SerializeField] private float hostShutdownDelay = 0.05f;

    [Header("Shutdown後、IsListeningが落ちるまで待つ時間（秒）")]
    [SerializeField] private float shutdownWaitTimeout = 2.0f;

    [Header("NetworkManagerも破棄する（完全リセット）")]
    [SerializeField] private bool destroyNetworkManagerObject = true;

    private bool _isLeaving = false;

    //  Coroutineが止まらないための Runner
    private static ShutdownRunner _runner;

    //==================================================
    // Runner（これがあるから Relay が消えても完走する）
    //==================================================
    private class ShutdownRunner : MonoBehaviour { }

    private static void EnsureRunner()
    {
        if (_runner != null) return;

        var go = new GameObject("[NetworkShutdownRunner]");
        DontDestroyOnLoad(go);
        _runner = go.AddComponent<ShutdownRunner>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // シーン毎に持つなら外してOK

        EnsureRunner();
    }

    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    //==================================================
    // 外から呼ぶ入口
    //==================================================
    public void ShutDown()
    {
        if (_isLeaving) return;

        if (IsServer)
        {
            //  Hostが落ちたら全員落とす
            Button_HostShutdownAll();
        }
        else
        {
            //  Clientが落ちたら自分だけ退出
            Button_ClientLeave();
        }
    }

    //==================================================
    // Host：全員終了
    //==================================================
    private void Button_HostShutdownAll()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        var nm = NetworkManager.Singleton;

        // ネットワークが動いてないなら、ローカルだけ完全終了
        if (nm == null || !nm.IsListening)
        {
            BeginLocalShutdownAndDestroy();
            return;
        }

        // Host/Server以外が押したら無視
        if (!IsServer)
        {
            _isLeaving = false;
            return;
        }

        //  Relayが消えても完走するRunnerで回す
        EnsureRunner();
        _runner.StartCoroutine(HostShutdownAllRoutine());
    }

    private IEnumerator HostShutdownAllRoutine()
    {
        // 1) 全Clientへ「切断してね」
        ShutdownAllClientsClientRpc();

        // 2) RPC到達待ち
        yield return null;
        if (hostShutdownDelay > 0f)
            yield return new WaitForSecondsRealtime(hostShutdownDelay);

        // 3) Host自身も完全終了（NetworkManager破棄まで）
        BeginLocalShutdownAndDestroy();
    }

    [ClientRpc]
    private void ShutdownAllClientsClientRpc()
    {
        // Hostにも飛ぶので Host はここでは落ちない（HostはRoutineで落ちる）
        if (IsServer) return;

        //  Clientは受信したら自分だけ完全終了
        BeginLocalShutdownAndDestroy();
    }

    //==================================================
    // Client：自分だけ退出
    //==================================================
    private void Button_ClientLeave()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        var nm = NetworkManager.Singleton;

        // ネットワークが動いてないならローカルだけ完全終了
        if (nm == null || !nm.IsListening)
        {
            BeginLocalShutdownAndDestroy();
            return;
        }

        // Hostがこれ押したら全員終了に切り替える
        if (IsServer)
        {
            EnsureRunner();
            _runner.StartCoroutine(HostShutdownAllRoutine());
            return;
        }

        EnsureRunner();
        _runner.StartCoroutine(ClientLeaveRoutine());
    }

    private IEnumerator ClientLeaveRoutine()
    {
        // 任意：Hostに「抜けるね」を送って Host側で即切断してもらう
        RequestKickMeServerRpc();

        yield return null;

        BeginLocalShutdownAndDestroy();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestKickMeServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        // Host自身は対象外
        if (senderId == nm.LocalClientId) return;

        nm.DisconnectClient(senderId);
    }

    //==================================================
    // 切断イベント（Host落ち/蹴られ/回線落ち）
    //==================================================
    private void OnClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // 自分が切断されたら完全終了へ
        if (clientId == nm.LocalClientId)
        {
            if (_isLeaving) return;

            _isLeaving = true;
            BeginLocalShutdownAndDestroy();
        }
    }

    //==================================================
    //  ここがメイン：NetworkManager破壊までやる
    //==================================================
    private void BeginLocalShutdownAndDestroy()
    {
        EnsureRunner();

        // 連続実行ガード（Runner側でも安全）
        _runner.StartCoroutine(ShutdownAndDestroyRoutine());
    }

    private IEnumerator ShutdownAndDestroyRoutine()
    {
        var nm = NetworkManager.Singleton;

        // すでに無いなら終わり
        if (nm == null)
            yield break;

        // 先にイベント解除（残り続ける事故を潰す）
        nm.OnClientDisconnectCallback -= OnClientDisconnected;

        //  Shutdown
        if (nm.IsListening)
        {
            nm.Shutdown();
        }

        //  IsListening が false になるまで待つ（後処理がある）
        float t = 0f;
        while (nm != null && nm.IsListening && t < shutdownWaitTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // 念のため1フレ
        yield return null;

        //  NetworkManagerのGameObjectを破壊（完全リセット）
        if (destroyNetworkManagerObject && nm != null)
        {
            // ここ重要：Singleton参照が残る場合があるから破壊で確殺
            Destroy(nm.gameObject);
        }

        // 念のため1フレ
        yield return null;

        // leaving解除（必要なら次の接続用に戻す）
        _isLeaving = false;
    }
}
