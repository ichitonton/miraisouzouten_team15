using NUnit.Framework;
using System;
using System.Security.Cryptography;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class ConnectPlayers : MonoBehaviour
{
    private GameObject _player1;
    private GameObject _player2;

    [SerializeField] private float _ropeLength = 20.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void Connect()
    {

        List<GameObject> players = new List<GameObject>();

        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
            //成功した場合 playerObj に GameObject が入る。
            if (playerRef.TryGet(out var playerObj))
            {
                //プレイヤーのタグを持っているかつ所有権があるなら
                if (playerObj.gameObject.CompareTag("Player") && playerObj.IsOwner)
                {
                    Debug.Log("所有権を持ったプレイヤーです");
                    players.Add(playerObj.gameObject);
                }
            }


        }

        _player1 = players[0];
        _player2 = players[1];

       
        Rigidbody rb1 = _player1.GetComponent<Rigidbody>();
        Rigidbody rb2 = _player2.GetComponent<Rigidbody>();

        if (rb1 == null) rb1 = _player1.AddComponent<Rigidbody>();
        if (rb2 == null) rb2 = _player2.AddComponent<Rigidbody>();

        if (rb1 == rb2)
        {
            Debug.LogError("同じRigidbodyにJointを接続しようとしています！");
        }

        _player1.transform.position = new Vector3(0.0f, 3.0f, 0.0f);
        _player2.transform.position = new Vector3(3.0f, 3.0f, 0.0f);


        //紐の物理挙動を追加
        var joint = _player1.AddComponent<ConfigurableJoint>();
        //プレイヤー2につなげる(物理きょづだからRigidbodyにつけてね
        joint.connectedBody = rb2;

        //紐でつながっているため移動を制限
        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;

        //紐の長さ、限界値を設定
        SoftJointLimit limit = new SoftJointLimit();
        limit.limit = _ropeLength;
        joint.linearLimit = limit;


        //回転は自由にする
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;


        JointDrive drive = new JointDrive();
        drive.positionSpring = 0; //ゴムの挙動なし
        drive.positionDamper = 0; //動きの減数無し
        drive.maximumForce = Mathf.Infinity;
        joint.xDrive = joint.yDrive = joint.zDrive = drive;


        GetComponent<RopeRenderer>().RegisterPlayers(_player1.transform, _player2.transform);

        //一応出す
        Debug.Log("プレイヤーコネクト完了");
    }

}
