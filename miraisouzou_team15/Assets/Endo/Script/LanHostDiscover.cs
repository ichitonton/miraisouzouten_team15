using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using UnityEngine;

public class LanHostDiscovery : MonoBehaviour
{
    UdpClient udpServer;
    const int DiscoveryPort = 47777;
    bool isAdvertising = false;

    void Start()
    {
        // 起動時に待機開始（必要なら）
        //udpServer = new UdpClient(DiscoveryPort);
        //udpServer.BeginReceive(OnReceive, null);
    }

    void OnReceive(IAsyncResult ar)
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, DiscoveryPort);
        byte[] data = udpServer.EndReceive(ar, ref remoteEP);
        string msg = Encoding.UTF8.GetString(data);

        if (msg == "DISCOVER_HOST")
        {
            string ip = GetLocalIPAddress();
            byte[] reply = Encoding.UTF8.GetBytes($"HOST_IP:{ip}");
            udpServer.Send(reply, reply.Length, remoteEP);
            //一回開放型にしてみる
            //udpServer.BeginReceive(OnReceive, null); // 続けて待機
            // Discoveryの役割は終了
            udpServer.Close();
            udpServer = null;
            isAdvertising = false;

            // 必要であればログを出力
            Debug.Log("Host Discovery: 応答後、Discoveryソケットをクローズしました。");
        }

        
    }

    string GetLocalIPAddress()
    {
        foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "0.0.0.0";
    }

    //追加：外部から呼び出すHost発信開始関数
    public void StartHostConnect()
    {
        if (isAdvertising) return;
        isAdvertising = true;

        udpServer = new UdpClient(DiscoveryPort);
        udpServer.BeginReceive(OnReceive, null);

        NetworkManager.Singleton.StartHost();

        Debug.Log("Host Discovery: 開始しました。LAN内で応答を待ちます。");
    }

    private void OnDestroy()
    {
        if (udpServer != null)
        {
            try
            {
                udpServer.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Host Discovery UdpClient close failed: {e.Message}");
            }
        }
    }

    void OnApplicationQuit()
    {
        udpServer?.Close();
    }
}
