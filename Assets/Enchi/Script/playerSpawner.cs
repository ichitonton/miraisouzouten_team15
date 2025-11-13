using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Profiling;

public class playerSpawner : MonoBehaviour
{
    [SerializeField] GameObject _player1;
    [SerializeField] GameObject _player2;
    [SerializeField] GameObject _joint;
    [SerializeField] int _jointCount;
    [SerializeField] float _upperSpring = 1000f;
    [SerializeField] float _upperDamper = 100f;
    [SerializeField] float _lowerSpring = 1500f;
    [SerializeField] float _lowerDamper = 150f;
    [SerializeField] float halfHeight = 0.5f; // オブジェクトの半分の高さ
    [SerializeField] GameObject _jointPool;
    //[SerializeField] NetworkObjectSpawner _objectSpawner;

    private GameObject _playerA;
    private GameObject _playerB;
    private List<GameObject> _joints;
    GameObject _jointPoolkari;

    private float _magicNumber = 0.5f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlayerSpawn();

        //プレイヤー検索
        
    }

    void PlayerSpawn()
    {
        //_objectSpawner.ObjectInstantiate(_player1, NetworkManager.Singleton.LocalClientId);
        //_objectSpawner.ObjectInstantiate(_player2, NetworkManager.Singleton.LocalClientId);

        _playerA.transform.position = this.transform.position + new Vector3(0, -1, 0);
        _playerA.transform.rotation = transform.rotation;
        _playerB.transform.position = this.transform.position + new Vector3(0.01f * (_jointCount + 1), -1, 0);
        _playerB.transform.rotation = transform.rotation;
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(200, Screen.height - 30, 100, 30), "リスポーン"))
        {
            Respawn();
        }
        if (GUI.Button(new Rect(100, Screen.height - 30, 100, 30), "ジョイント生成"))
        {
            SpawnJoint();
        }
    }

    void SpawnJoint()
    {

        //ジョイントプール生成
        //_jointPoolkari = _objectSpawner.ObjectInstantiate(_jointPool, NetworkManager.Singleton.LocalClientId);

        _jointPoolkari.transform.position = this.transform.position;
        _jointPoolkari.transform.rotation = this.transform.rotation;
        _jointPoolkari.SetActive(true);
        _jointPoolkari.GetComponent<PlayerJoint>()._playerA = _playerA;
        _jointPoolkari.GetComponent<PlayerJoint>()._playerB = _playerB;

        for (int i = 0; i < _jointCount; i++)
        {
            Instantiate(_joint, this.transform.position + new Vector3(0.01f * i, 0, 0), this.transform.rotation, _jointPoolkari.transform).SetActive(false);
        }
    }

    public void Respawn()
    {
        _playerA.transform.position = this.transform.position + new Vector3(0, -1.0f, 0);
        _playerA.transform.rotation = transform.rotation;
        _playerB.transform.position = this.transform.position + new Vector3(0.01f * (_jointCount + 1), -1, 0);
        _playerB.transform.rotation = transform.rotation;

        for (int i = 0; i < _jointPoolkari.transform.childCount; i++)
        {
            transform.GetChild(i).transform.position = this.transform.position + new Vector3(0.01f * i, 0, 0);
            transform.GetChild(i).transform.rotation = this.transform.rotation;
        }
    }

}
