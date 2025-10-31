using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class LanClientDiscovery : MonoBehaviour
{
    const int DiscoveryPort = 47777;
    private UnityTransport transport;

    void Start()
    {
        transport = GetComponent<UnityTransport>();
    }

    private async void DiscoverAndConnect()
    {
        using (UdpClient client = new UdpClient())
        {
            client.EnableBroadcast = true;
            byte[] msg = Encoding.UTF8.GetBytes("DISCOVER_HOST");

            // ブロードキャスト送信
            await client.SendAsync(msg, msg.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));

            // 応答待ち（1秒）
            var receiveTask = client.ReceiveAsync();
            if (await System.Threading.Tasks.Task.WhenAny(receiveTask, System.Threading.Tasks.Task.Delay(1000)) == receiveTask)
            {
                string response = Encoding.UTF8.GetString(receiveTask.Result.Buffer);
                if (response.StartsWith("HOST_IP:"))
                {
                    string hostIP = response.Substring(8);
                    Debug.Log($"Host found at {hostIP}");


                    
                    transport.SetConnectionData(hostIP,8888);
                    Debug.Log("※ダメかも");
                    NetworkManager.Singleton.StartClient();
                }
            }
            else
            {
                Debug.LogWarning("Host not found.");
            }
        }
    }

    //追加：外部UIから呼び出す関数
    public void StartClientConnect()
    {
        Debug.Log("Client Discovery: Host探索を開始します。");
        DiscoverAndConnect();
    }
}
