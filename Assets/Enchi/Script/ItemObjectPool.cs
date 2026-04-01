using System.Drawing;
using Unity.Netcode;
using UnityEngine;

public class ItemObjectPool : NetworkBehaviour
{

    // シングルトンのグローバルなアクセスポイント (public static)
    public static ItemObjectPool Instance { get; private set; }

    [SerializeField] GameObject _itemBomb;

    bool[] bools = new bool[(int)ItemVariant.Max];

    enum ItemVariant
    {
        Bomb =0,
        Max
    }

    private void Awake()
    {
        //シングルトンのインスタンス生成
        if (Instance == null)
        {
            Instance = this;
            //シーンの切り替えで消えない
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
        // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void InstantiateItemObject()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.GetComponent<ItemBomb>() != null)
            {
                //生成
                bools[(int)ItemVariant.Bomb] = true;

                InstatiateObj(_itemBomb, transform, Quaternion.identity, transform).SetActive(false);
            }

        }
    }

    private GameObject InstatiateObj(GameObject joint, Transform transform, Quaternion quaternion, Transform pearent)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[RPC] ClientでRope誤実行されたためスキップ");
            return null;
        }
        ulong clientId = NetworkManager.Singleton.LocalClientId;

        Debug.Log($"[Host] Client {clientId} からRope生成リクエストを受信");

        // Ropeを生成
        GameObject obj = Instantiate(joint, transform.position, quaternion, pearent);
        obj.SetActive(false);

        //var netObj = GetComponent<NetworkObject>();
        //オブジェクトのオーナーを決める
        //netObj.SpawnWithOwnership(clientId);

        // ClientRpcの送信先を1クライアントに限定
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId } // ← ここで送信先を指定！
            }
        };
        return obj;
    }

}
