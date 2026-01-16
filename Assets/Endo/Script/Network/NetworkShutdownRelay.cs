using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
public class NetworkShutdownRelay : NetworkBehaviour
{
    public static NetworkShutdownRelay Instance { get; private set; }

    //[Header("切断後に戻すシーン名")]
    //[SerializeField] private string backSceneName = "Title";

    [Header("Hostが全員切断する時の待ち（RPC到達用）")]
    [SerializeField] private float hostShutdownDelay = 0.05f;

    private bool _isLeaving = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }


    public void ShutDown()
    {
        if(IsServer)
        {
            //ホストが落ちたら全部落とす
            Button_HostShutdownAll();
        }
        else
        {
            //クライアントが落ちたらそいつだけ退出
            Button_ClientLeave();
        }
    }

    // =========================================================
    //  ボタン用：Hostが押す（全員終了）
    // =========================================================
    private void Button_HostShutdownAll()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        // ネットワークが動いてないならローカルだけ戻す
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return;
        }

        // Host/Server以外が押したら無視（安全）
        if (!IsServer)
        {
            _isLeaving = false;
            return;
        }

        StartCoroutine(HostShutdownAllRoutine());
    }

    private IEnumerator HostShutdownAllRoutine()
    {
        //  全Clientへ「切断して戻ってね」通知
        ShutdownAllClientsClientRpc();

        //  すぐHostがShutdownするとRPC届かない事があるので少し待つ
        yield return null;
        if (hostShutdownDelay > 0f) yield return new WaitForSeconds(hostShutdownDelay);

        ShutdownLocalNetwork();
    }

    [ClientRpc]
    private void ShutdownAllClientsClientRpc()
    {
        // Host自身にも飛ぶので、Hostはここでは切らない（Host側はRoutineで切る）
        if (IsServer) return;

        // Clientは受け取ったら安全に退出
        ClientLeave_Local();
    }

    // =========================================================
    //  ボタン用：Clientが押す（自分だけ退出）
    // =========================================================
    private void Button_ClientLeave()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        // ネットワークが動いてないならローカルだけ戻す
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return;
        }

        // Hostがこれを押すのは事故なので HostShutdownAll に誘導
        if (IsServer)
        {
            // Hostが押しちゃった場合は全員終了を実行しちゃうのが安全
            StartCoroutine(HostShutdownAllRoutine());
            return;
        }

        StartCoroutine(ClientLeaveRoutine());
    }

    private IEnumerator ClientLeaveRoutine()
    {
        //  Hostへ「退出するね」を送って、Host側で即切断してもらう（任意だけど安全）
        RequestKickMeServerRpc();

        // 1フレ待ってからローカルShutdown（安定）
        yield return null;

        ClientLeave_Local();
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

        // Host側でそのClientだけ切断
        nm.DisconnectClient(senderId);
    }

    // =========================================================
    //  切断イベント対応（Host落ち/蹴られた/回線切れでも戻す）
    // =========================================================
    private void OnClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // 自分が切断されたらタイトルへ戻す
        if (clientId == nm.LocalClientId)
        {
            // すでに自分で退出処理中なら二重実行しない
            if (_isLeaving) return;

            _isLeaving = true;
            ShutdownLocalNetwork();
        }
    }

    // =========================================================
    //  ローカル切断（共通）
    // =========================================================
    private void ClientLeave_Local()
    {
        ShutdownLocalNetwork();
    }

    private void ShutdownLocalNetwork()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            nm.Shutdown();
        }
    }
}
