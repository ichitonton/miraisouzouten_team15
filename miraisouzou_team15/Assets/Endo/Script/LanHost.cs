using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

/// <summary>
/// LAN内でHostを発見してもらうためのホスト側スクリプト。
/// クライアントからのブロードキャスト問い合わせを受けて、
/// 応答を返す（＝自分がHostであることを通知）
/// </summary>
public class LanHost : MonoBehaviour
{
    [TextArea(2,5)]
    public string _memo;
    [Header("スクリプトは基本問題ないから、ローカル通信できなかったらまずはIPアドレスを確認して、" +
        "そのあとWin + R　キーでncpa.cplを入力→イーサネットが識別中ならOK(メディアが切断されています。" +
        "とか有効、ブリッジになってたら二人の時はできないよ)")]
    
    private UnityTransport _transport;
    private UdpClient _udpClient;
    private bool _isRunning = false;
    private const int _listenPort = 47777;   // クライアントが送ってくるポート番号(メッセージを受け取るためのブロードキャストport、ゲームの接続自体には関係ないよ)
    private const string _discoveryRequest = "LAN_DISCOVERY"; //クライアントから送られてくるメッセージ
    private const string _discoveryResponse = "LAN_GAME_HOST"; //クライアントに送るメッセージ

    private void Start()
    {
        _transport = GetComponent<UnityTransport>();
    }
    private void OnDestroy()
    {
        StopListening();
    }

    /// <summary>
    /// HostとしてLAN上での問い合わせを待ち受け開始
    /// </summary>
    public void StartHostConnect()
    {
        if (_isRunning) return;
        _isRunning = true;

        _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, _listenPort));
        //クライアントから_litePortあてにHostいますかー？と問い合わせが来るよ
        Debug.Log("ホスト : 問い合わせを待ちます");

        if(GameManager.Instance._onlineMode == GameManager.OnlineMode.OnePC)
        {
           
            //ホストとして入室
            _transport.ConnectionData.Address = "0.0.0.0";// ← どこからでも接続を受ける
            _transport.ConnectionData.Port = 7777;// ← Netcodeの待ち受けポート(同一PCからだとClientのportが被ってソケットエラーになるよ)
            _transport.SetConnectionData("0.0.0.0", 7777);//あなたはホストとしてどのIPアドレスでも受け取れるようにポート7777で待機しなさい。
            NetworkManager.Singleton.StartHost();

            // 非同期でリッスン開始
            _ = ListenForDiscoveryRequestsOnePC();
        }
        if (GameManager.Instance._onlineMode == GameManager.OnlineMode.MoreTowPC)
        {
            //ホストとして入室
            _transport.ConnectionData.Address = "0.0.0.0";// ← どこからでも接続を受ける
            _transport.ConnectionData.Port = 7777;// ← Netcodeの待ち受けポート(同一PCからだとClientのportが被ってソケットエラーになるよ)
            _transport.SetConnectionData("0.0.0.0", 7777);//あなたはホストとしてどのIPアドレスでも受け取れるようにポート7777で待機しなさい。
            NetworkManager.Singleton.StartHost();

            // 非同期でリッスン開始
            _ = ListenForDiscoveryRequestsMoreTowPC();
            //_←戻り値無視だって、awaitは完了を待つ、taskの戻り値を使ういがいで、完了を待たずバックグラウンドで動かすけど、警告出したくないときに使うんだって
        }

    }

    /// <summary>
    /// クライアントからの問い合わせメッセージを受け取り、
    /// 受け取った相手（クライアント）に対して「Hostがここにいるよ」と応答を返す。
    /// </summary>
    /// 
    private async Task ListenForDiscoveryRequestsOnePC()
    {
        while (_isRunning)
        {
            try
            {
                // クライアントからのUDPメッセージ受信待機
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);

                // クライアントからの問い合わせを検出
                if (message == _discoveryRequest)
                {
                    Debug.Log($"Discovery request received from {result.RemoteEndPoint.Address}");

                    // 応答メッセージをクライアントに返す
                    byte[] responseData = Encoding.UTF8.GetBytes(_discoveryResponse);
                    await _udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                    Debug.Log($"Sent discovery response to {result.RemoteEndPoint.Address}");
                }
            }
            catch (SocketException)
            {
                // UDPソケットが閉じられたときなどは無視
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error while listening for discovery: {ex.Message}");
            }
        }
    }

    private async Task ListenForDiscoveryRequestsMoreTowPC()
    {
        while (_isRunning)
        {
            try
            {
                // クライアントからのUDPメッセージ受信待機
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);

                // クライアントからの問い合わせを検出
                if (message == _discoveryRequest)
                {
                    Debug.Log($"Discovery request received from {result.RemoteEndPoint.Address}");

                    // 応答メッセージをクライアントに返す
                    byte[] responseData = Encoding.UTF8.GetBytes(_discoveryResponse);
                    await _udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                    Debug.Log($"Sent discovery response to {result.RemoteEndPoint.Address}");
                }
            }
            catch (SocketException)
            {
                // UDPソケットが閉じられたときなどは無視
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error while listening for discovery: {ex.Message}");
            }
        }
    }



    /// <summary>
    /// Hostの待ち受けを停止
    /// </summary>
    public void StopListening()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _udpClient?.Close();
        _udpClient = null;

        Debug.Log("LAN Host Discovery: Stopped listening for requests.");
    }

    private void OnApplicationQuit()
    {
        _udpClient?.Close();
        _udpClient = null;
    }
}
