using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{

    [SerializeField]private float moveSpeed = 5f;
    private Rigidbody rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        Vector3 input = Vector3.zero;
        
        //if (!IsOwner) return;

        if (Input.GetKey(KeyCode.W)) input += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) input += Vector3.back;
        if (Input.GetKey(KeyCode.A)) input += Vector3.left;
        if (Input.GetKey(KeyCode.D)) input += Vector3.right;

        //Debug.Log("はしるー");

        input = input.normalized * moveSpeed;

        
        rb.linearVelocity = new Vector3(input.x, rb.linearVelocity.y, input.z);

    }

    public override void OnNetworkSpawn()
    {
        if (IsClient && !IsHost)
        {
            // Client側で自分が接続完了したらHostに通知
            NotifyHostPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyHostPlayerReadyServerRpc(ulong clientId)
    {
        Debug.Log($"[Host] Client {clientId} のプレイヤー準備完了通知を受け取りました。");

        var allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in allPlayers)
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj == null || netObj.IsSpawned) continue;

            // ClientのPlayerをSpawn
            netObj.SpawnAsPlayerObject(clientId);
            Debug.Log($"[Host] Client {clientId} のPlayer {p.name} をSpawnしました。");
            return;
        }

        Debug.LogWarning($"[Host] Client {clientId} に対応するPlayerが見つかりません。");
    }
}
