using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class PlayerJoint : MonoBehaviour
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
    private List<GameObject> _joints;

    private float _magicNumber = 0.5f;


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
                if (playerObj.gameObject.CompareTag("Player") && playerObj.IsOwner)
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
            Instantiate(_joint, this.transform.position + new Vector3(0.01f * i, 0, 0), this.transform.rotation, this.transform).SetActive(false);
        }

            Joint();
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


}
