using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.Burst.CompilerServices;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

public class PlayerJoint : NetworkBehaviour
{

    [SerializeField] GameObject _joint;
    [SerializeField] int _jointCount = 30;
    [SerializeField] float _upperSpring = 1000f;
    [SerializeField] float _upperDamper = 100f;
    [SerializeField] float _lowerSpring = 1500f;
    [SerializeField] float _lowerDamper = 150f;
    [SerializeField] float halfHeight = 0.5f; // オブジェクトの半分の高さ

    private GameObject _playerA;
    private GameObject _playerB;
    private float _magicNumber = 0.5f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        List<GameObject> players = new List<GameObject>();
        ulong clientId = NetworkManager.Singleton.LocalClientId;

        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
            //成功した場合 playerObj に GameObject が入る。
            if (playerRef.TryGet(out var playerObj))
            {
                //プレイヤーのタグを持っているかつ所有権があるなら
                if (playerObj.gameObject.CompareTag("Player") && playerObj.OwnerClientId == OwnerClientId)
                {
                    Debug.Log("所有権を持ったプレイヤーです");
                    players.Add(playerObj.gameObject);
                    Debug.Log($"Added player: {playerObj.gameObject.name}");
                }
            }
        }
        if (players.Count < 2)
        {
            Debug.LogError($"プレイヤー数が足りません！検出数: {players.Count}");
            return;
        }

        _playerA = players[0];
        _playerB = players[1];

        if (IsServer)
        {
            for (int i = 0; i < _jointCount; i++)
            {
                // Ropeを生成
                GameObject rope = Instantiate(_joint, transform.position, Quaternion.identity, transform);

            }
            Vector3 posA = this.transform.position + new Vector3(0, -1.0f + _magicNumber, 0);
            Vector3 posB = this.transform.position + new Vector3(0.01f * (transform.childCount + 1), -1.0f + _magicNumber, 0);

            ulong clientA = _playerA.GetComponent<NetworkObject>().OwnerClientId;
            ulong clientB = _playerB.GetComponent<NetworkObject>().OwnerClientId;

            //Debug.Log($"LocalClientId = {clientId}");
            //Debug.Log($"clientA = {clientA}");
            //Debug.Log($"clientB = {clientB}");
            //Debug.Log($"OwnerClientId = {OwnerClientId}");

            // 2人にテレポート命令
            TeleportServerRpc(posA, posB, clientA, clientB);
            Joint();
            Invoke("SetOwner", 0.1f);
        }
    }

    // ========================
    // クライアント側でのみ実行される Teleport RPC
    // ========================
    [ServerRpc(RequireOwnership = false)]
    private void TeleportServerRpc(Vector3 posA, Vector3 posB, ulong clientA, ulong clientB)
    {
        ulong local = OwnerClientId;

        // --- ローカルクライアントが A 担当なら ---
        if (local == clientA)
        {
            _playerA.GetComponent<NetworkObject>().ChangeOwnership(0);
            var nt = _playerA.GetComponent<NetworkTransform>();
            nt.Teleport(posA, _playerA.transform.rotation, _playerA.transform.localScale);
            Debug.Log("[Teleport] Player A テレポート");
            //_playerA.GetComponent<NetworkObject>().ChangeOwnership(local);
        }

        // --- ローカルクライアントが B 担当なら ---
        if (local == clientB)
        {
            _playerB.GetComponent<NetworkObject>().ChangeOwnership(0);
            var nt = _playerB.GetComponent<NetworkTransform>();
            nt.Teleport(posB, _playerB.transform.rotation, _playerB.transform.localScale);
            Debug.Log("[Teleport] Player B テレポート");
            //_playerB.GetComponent<NetworkObject>().ChangeOwnership(local);
        }
    }

    void SetOwner()
    {
        _playerA.GetComponent<NetworkObject>().ChangeOwnership(OwnerClientId);
        _playerB.GetComponent<NetworkObject>().ChangeOwnership(OwnerClientId);
    }

    //[ServerRpc(RequireOwnership = false)]
    //void ReturnOwnerServerRpc(ulong objectId, ulong newOwnerId)
    //{
    //    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out var obj))
    //    {
    //        obj.ChangeOwnership(newOwnerId);
    //        //Debug.LogError($" newOwnerId: {newOwnerId} ");
    //    }
    //    else
    //    {
    //        Debug.LogError($"ReturnOwnerServerRpc: objectId {objectId} が存在しません");
    //    }
    //}


    void Joint()
    {
        GameObject _husi_up = null;
        GameObject _husi_down = null;
        Debug.Log(_playerA);
        //_playerA.transform.position = this.transform.position + new Vector3(0, -1.0f + _magicNumber, 0);
        //_playerA.transform.rotation = transform.rotation;
        //_playerB.transform.position = this.transform.position + new Vector3(0.01f * (transform.childCount + 1), -1.0f + _magicNumber, 0);
        //_playerB.transform.rotation = transform.rotation;


        GameObject _A = _playerA;
        GameObject _B;
        Vector3 spawnRotation = new Vector3(0, 0, 0);

        for (int i = 0; i <= _jointCount; i++)
        {
            if (i == _jointCount)
            {
                _B = _playerB;
            }
            else
            {
                _B = transform.GetChild(0).gameObject;
                _B.SetActive(true);
                _B.transform.position = this.transform.position + new Vector3(0.01f * i, 0 + _magicNumber, 0);
                _B.transform.rotation = this.transform.rotation;

                var netObj = _B.GetComponent<NetworkObject>();

                _B.GetComponent<Rigidbody>().isKinematic = true;

                netObj.Spawn();

                //ReturnOwnerServerRpc(netObj.NetworkObjectId, OwnerClientId);

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

            if (_B.GetComponent<JointLiner>() != null)
            {
                _B.GetComponent<JointLiner>().SetHaveJoint(true);

                //var netObj = _B.GetComponent<NetworkObject>();

                //ReturnOwnerServerRpc(netObj.NetworkObjectId, OwnerClientId);

                //_B.GetComponent<Rigidbody>().isKinematic = false;
                //var netRb = _B.GetComponent<NetworkRigidbody>();
                //netRb.enabled = true;
            }

            _A = _B;

        }

    }
}
