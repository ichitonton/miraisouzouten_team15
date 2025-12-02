using System.Collections.Generic;
using System.Drawing;
using Unity.Burst.CompilerServices;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerJoint : MonoBehaviour
{

    [SerializeField] GameObject _joint;
    [SerializeField] int _jointCount = 30;
    [SerializeField] float _upperSpring = 1000f;
    [SerializeField] float _upperDamper = 100f;
    [SerializeField] float _lowerSpring = 1500f;
    [SerializeField] float _lowerDamper = 150f;
    [SerializeField] float halfHeight = 0.5f; // オブジェクトの半分の高さ
    [SerializeField] Material _material; // オブジェクトの半分の高さ

    private GameObject _playerA;
    private GameObject _playerB;
    private List<GameObject> _joints;

    private float _magicNumber = 0.5f;

    private LineRenderer line;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        List<GameObject> players = new List<GameObject>();

        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
            //成功した場合 playerObj に GameObject が入る。
            if (playerRef.TryGet(out var playerObj))
            {
                //プレイヤーのタグを持っているかつ所有権があるなら
                if (playerObj.gameObject.CompareTag("Player") && playerObj.IsOwnedByServer)
                {
                    Debug.Log("所有権を持ったプレイヤーです");
                    players.Add(playerObj.gameObject);
                }
            }


        }
        _playerA = players[0];
        _playerB = players[1];

        for (int i = 0; i < _jointCount; i++)
        {
            //生成して非アクティブにしておく
            //Instantiate(_joint, this.transform.position + new Vector3(0.01f * i, 0, 0), this.transform.rotation, this.transform).SetActive(false);


            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.Log("[RPC] ClientでRope誤実行されたためスキップ");
                return;
            }
            ulong clientId = NetworkManager.Singleton.LocalClientId;

            Debug.Log($"[Host] Client {clientId} からRope生成リクエストを受信");

            // Ropeを生成
            GameObject rope = Instantiate(_joint, transform.position, Quaternion.identity, transform);
            rope.SetActive(false);

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
        }

        Joint();
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = (_jointCount) * 4 ;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f; 
        line.material = _material;  // マテリアルの色を白にする
        //line.startColor = UnityEngine.Color.yellow;
        //line.endColor = UnityEngine.Color.yellow;
    }


    private ConfigurableJoint joint;
    Vector3[] ancors = new Vector3[4];

    void Update()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                joint = transform.GetChild(i).GetComponents<ConfigurableJoint>()[j];
                ancors[j * 2] = joint.transform.TransformPoint(joint.anchor);
                ancors[j * 2 + 1] = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
                
                //網の上を少し下げる（当たり判定には影響なし）
                if (j < 1)
                {
                    ancors[j * 2] -= Vector3.up * 0.2f;
                    ancors[j * 2 + 1] -= Vector3.up * 0.2f;
                }
                if (joint == null || joint.connectedBody == null)
                {
                    line.enabled = false;
                    return;
                }
                line.enabled = true;
            }
                // 線を描画
                //[1]  [0]
                //   ×
                //[3]－[2]
            line.SetPosition(i * 4, ancors[0]);
            line.SetPosition(i * 4 + 1, ancors[3]);
            line.SetPosition(i * 4 + 2, ancors[2]);
            line.SetPosition(i * 4 + 3, ancors[1]);
        }
    }
    private void OnGUI()
    {
        if (GUI.Button(new Rect(300, Screen.height - 30, 100, 30), "つなぐ"))
        {
        }
    }

    void Joint()
    {


        GameObject _husi_up = new GameObject();
        GameObject _husi_down = new GameObject();
        Debug.Log(_playerA);
        _playerA.transform.position = this.transform.position + new Vector3(0, -1.0f + _magicNumber, 0);
        _playerA.transform.rotation = transform.rotation;
        _playerB.transform.position = this.transform.position + new Vector3(0.01f * (transform.childCount + 1), -1.0f + _magicNumber, 0);
        _playerB.transform.rotation = transform.rotation;


        GameObject _A = _playerA;
        GameObject _B;
        Vector3 spawnRotation = new Vector3(0, 0, 0);

        for (int i = 0; i <= transform.childCount; i++)
        {
            if (i == transform.childCount)
            {
                _B = _playerB;
            }
            else
            {
                _B = transform.GetChild(i).gameObject;
                _B.SetActive(true);
                _B.transform.position = this.transform.position + new Vector3(0.01f * i, 0 + _magicNumber, 0);
                _B.transform.rotation = this.transform.rotation;
            }
            // 上側のジョイント設定
            ConfigurableJoint upper = _B.AddComponent<ConfigurableJoint>();
            upper.xMotion = ConfigurableJointMotion.Limited;
            upper.yMotion = ConfigurableJointMotion.Limited;
            upper.zMotion = ConfigurableJointMotion.Limited;
            upper.connectedBody = _A.GetComponent<Rigidbody>();
            upper.autoConfigureConnectedAnchor = false;
            upper.anchor = new Vector3(0, +halfHeight, 0);

            if (_B.tag == "Player")
            {
                upper.anchor = new Vector3(0, halfHeight * 2.0f + _magicNumber, 0);
            }
            else
            {
                upper.anchor = new Vector3(0, halfHeight, 0);
            }
            if (_A.tag == "Player")
            {
                upper.connectedAnchor = new Vector3(0, halfHeight * 2.0f + _magicNumber, 0);
            }
            else
            {
                upper.connectedAnchor = new Vector3(0, +halfHeight, 0);
            }
            // 修正: SoftJointLimit構造体を使ってlinearLimitを設定
            SoftJointLimit limitStruct = upper.linearLimit;
            limitStruct.limit = 0.1f;
            upper.linearLimit = limitStruct;
            // 修正: 構造体を取得して値を設定し、再代入する

            SoftJointLimitSpring upperSpringStruct = upper.linearLimitSpring;
            upperSpringStruct.spring = _upperSpring;
            upperSpringStruct.damper = _upperDamper;
            upper.linearLimitSpring = upperSpringStruct;

            joint = _B.GetComponent<ConfigurableJoint>();

            //OnDrawGizmos();

            // 下側のジョイント設定
            ConfigurableJoint lower = _B.AddComponent<ConfigurableJoint>();
            lower.xMotion = ConfigurableJointMotion.Limited;
            lower.yMotion = ConfigurableJointMotion.Limited;
            lower.zMotion = ConfigurableJointMotion.Limited;
            lower.connectedBody = _A.GetComponent<Rigidbody>();
            lower.autoConfigureConnectedAnchor = false;
            if (_B.tag == "Player")
            {
                lower.anchor = new Vector3(0, _magicNumber, 0);
            }
            else
            {
                lower.anchor = new Vector3(0, -halfHeight, 0);
            }
            if (_A.tag == "Player")
            {
                lower.connectedAnchor = new Vector3(0, _magicNumber, 0);
            }
            else
            {
                lower.connectedAnchor = new Vector3(0, -halfHeight, 0);
            }

            lower.linearLimit = limitStruct;

            SoftJointLimitSpring lowerSpringStruct = lower.linearLimitSpring;
            lowerSpringStruct.spring = _lowerSpring;
            lowerSpringStruct.damper = _lowerDamper;
            lower.linearLimitSpring = lowerSpringStruct;


            _A = _B;
        }

    }
    //private void OnDrawGizmos()
    //{
    //    Gizmos.color = UnityEngine.Color.yellow;

    //    // 接続されているポイント
    //    Vector3 a = transform.TransformPoint(joint.anchor);
    //    Vector3 b = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);

    //    Gizmos.DrawSphere(a, 0.05f);
    //    Gizmos.DrawSphere(b, 0.05f);
    //    Gizmos.DrawLine(a, b);
    //}


}
