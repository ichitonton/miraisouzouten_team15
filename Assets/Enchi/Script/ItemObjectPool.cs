using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class ItemObjectPool : NetworkBehaviour
{

    // シングルトンのグローバルなアクセスポイント (public static)
    public static ItemObjectPool Instance { get; private set; }

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
            }

        }
    }
}
