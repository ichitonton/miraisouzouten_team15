using Unity.Netcode;
using UnityEngine;

public class NetworkStartUI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnGUI()
    {

        if (NetworkManager.Singleton == null)
        {
            GUILayout.Label("NetworkManager not found in scene!");
            return;
        }

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            //ホストとして入る
            if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
            //クライアントとして入る
            if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
        }

    }
}
