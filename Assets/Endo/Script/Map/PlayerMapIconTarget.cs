using UnityEngine;
using Unity.Netcode; // Netcode使ってなければ不要
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
/// <summary>
/// ミニマップ用プレイヤーアイコンのターゲット
/// </summary>
public class PlayerMapIconTarget : MonoBehaviour
{

    [Tooltip("アイコンの位置・向きを取りたいTransform。空ならこのオブジェクト")]
    [SerializeField] private Transform _iconTarget;

    [Tooltip("LANモード時、ローカルプレイヤーだけ表示するか")]
    [SerializeField] private bool _onlyLocalInLan = true;

    [SerializeField] public int _id = 0;

    // Netcode使ってないなら消してOK
    //private NetworkObject _networkObject;

    public Transform IconTarget => _iconTarget != null ? _iconTarget : transform;

    // オフライン用に、NetworkManager が動いてないときもアイコン出したいなら true
    [SerializeField] private bool _registerWhenNoNetwork = true;

    private NetworkObject _networkObject;

    private CancellationTokenSource _cts;

    private void Awake()
    {
        // Netcode for GameObjects 使ってる前提
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnEnable()
    {
        // ネットワーク的に「プレイヤー」として確定したタイミング
        _cts = new CancellationTokenSource();
        WaitAndTryRegisterAsync(_cts.Token).Forget();
        // オフライン or Netcode止まってるとき用
        //TryRegister();
    }

    private void OnDisable()
    {
        PlayerMapIconManager.Unregister(this);
    }


    /// <summary>
    /// ちょっと待ってから GameManager / IsLocalPlayer を判定して Register する
    /// </summary>
    private async UniTaskVoid WaitAndTryRegisterAsync(CancellationToken token)
    {
        // ① 0.秒待つくらい様子を見る（Netcode内部の初期化待ち）
        await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: token);

        if (token.IsCancellationRequested) return;
        TryRegister();

    }


    /// <summary>
    /// 条件を満たしていれば Manager に登録
    /// </summary>
    private void TryRegister()
    {
        if (!ShouldRegister()) return;
        PlayerMapIconManager.Register(this);
    }

    /// <summary>
    /// GameManager / IsLocalPlayer の状態で登録するかどうか決める
    /// </summary>
    private bool ShouldRegister()
    {
        var gm = GameManager.Instance;

        if (gm != null && gm._IsLanModeActive)
        {
            // ここで IsLocalPlayer / IsOwner を見る
            // Host でも LocalClientId を持っているので true になるはず
            var net = GetComponent<NetworkObject>();
            Debug.Log("オーなプレイヤーですか" + net.IsLocalPlayer);
            return net.IsLocalPlayer;      // か、必要なら IsLocalPlayer でもOK
        }

        // GameManager がいない or 無効 → 全員登録（オフライン/メニュー用）
        return true;
    }

    private bool IsNetworkAvailable()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}
