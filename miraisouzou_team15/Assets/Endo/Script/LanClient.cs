using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System;

/// <summary>
/// LAN内のHostを探すクライアント側スクリプト。
/// 定期的にブロードキャストで「Hostいますか？」と問い合わせ、
/// 応答を受け取ったらそのHostに自動接続する。
/// </summary>
public class LanClient : MonoBehaviour
{
    private UnityTransport _transport;
    private UdpClient _udpClient;
    private bool _isDiscovering = false;
    private const int _broadcastPort = 47777; // Hostと通信するポート(Hostを見つけるためのブロードキャスト用のPort、ゲームの通信portじゃないよ)
    private const string _discoveryRequest = "LAN_DISCOVERY";
    private const string _discoveryResponse = "LAN_GAME_HOST";
    private float _broadcastInterval = 1.0f; // 1秒ごとに問い合わせを送る


    private void Start()
    {
        _transport = GetComponent<UnityTransport>();
    }

    private void OnDestroy()
    {
        StopDiscovery();
    }

    /// <summary>
    /// LAN上のHost探索を開始
    /// </summary>
    public void StartClientConnect()
    {
        if (_isDiscovering) return;
        _isDiscovering = true;

        _udpClient = new UdpClient();
        _udpClient.EnableBroadcast = true;

        Debug.Log("LAN Client Discovery: Starting discovery...");

        _transport.ConnectionData.Port = 7778;// ← Hostの待受ポートと一致(同一PCだとだめかも)

        // 問い合わせ送信と応答待ちを同時に実行
        _ = SendDiscoveryRequests();
        _ = ListenForResponses();
    }

    /// <summary>
    /// LAN全体に「Hostいますか？」とブロードキャストを定期的に送信する
    /// </summary>
    private async Task SendDiscoveryRequests()
    {
        //_broadcastPortあてにHostいますかー？と送信するよ
        IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _broadcastPort);
        byte[] requestData = Encoding.UTF8.GetBytes(_discoveryRequest);

        while (_isDiscovering)
        {
            try
            {
                await _udpClient.SendAsync(requestData, requestData.Length, broadcastEndpoint);
                Debug.Log("Sent discovery broadcast.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Broadcast send failed: {ex.Message}");
            }

            await Task.Delay((int)(_broadcastInterval * 1000));
        }
    }

    /// <summary>
    /// Hostからの応答を待ち受ける
    /// </summary>
    private async Task ListenForResponses()
    {
        while (_isDiscovering)
        {
            try
            {
                // ReceiveAsync がキャンセルされた場合の例外対策
                UdpReceiveResult result = await _udpClient.ReceiveAsync();

                string message = Encoding.UTF8.GetString(result.Buffer);

                if (message == _discoveryResponse)
                {
                    string hostIp = result.RemoteEndPoint.Address.ToString();
                    Debug.Log($"Host found at: {hostIp}");

                    
                    _transport.ConnectionData.Address = hostIp;// ← Hostの実IP（例：192.168.0.12）
                    _transport.SetConnectionData(hostIp, 7777);

                    var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
                    Debug.Log($"Connecting to {transport.ConnectionData.Address}:{transport.ConnectionData.Port}");

                    NetworkManager.Singleton.StartClient();

                    Debug.Log(hostIp);


                    // 停止前にループを抜ける
                    _isDiscovering = false;
                    break;
                }
            }
            catch (ObjectDisposedException)
            {
                // udpClient.Close() 後に発生 → 無視して終了
                break;
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"Socket error: {ex.Message}");
                // 一時的なエラーなら続行、重大な場合は抜けてもOK
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Unexpected error: {ex}");
            }
        }

        // 最後に安全にクローズ
        StopDiscovery();
    }

    /// <summary>
    /// 探索処理を停止
    /// </summary>
    public void StopDiscovery()
    {
        if (!_isDiscovering) return;
        _isDiscovering = false;

        _udpClient?.Close();
        _udpClient = null;

        Debug.Log("LAN Client Discovery: Stopped discovery.");
    }

    private void OnApplicationQuit()
    {
        _udpClient?.Close();
        _udpClient = null;
    }
}
