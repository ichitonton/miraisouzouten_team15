using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
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

    private List<GameObject> _joints;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Spawn();


    }
    void Spawn()
    {

        GameObject _husi_up = new GameObject();
        GameObject _husi_down = new GameObject();

        _player1.transform.position = this.transform.position + new Vector3(0, -1, 0);
        _player2.transform.position = this.transform.position + new Vector3(0.01f * (_jointCount + 1), -1, 0);
        _player1.transform.rotation = this.transform.rotation;
        _player2.transform.rotation = this.transform.rotation;
        

        GameObject _A = _player1;
        GameObject _B;
        Vector3 spawnRotation = new Vector3(0, 0, 0);

        for (int i = 0; i < _jointCount + 1; i++)
        {
            if(i == _jointCount)
                {
                _B = _player2;
            }
            else
                _B = Instantiate(_joint, this.transform.position + new Vector3(0.01f * i, 0, 0), this.transform.rotation, this.transform);

            // 上側のジョイント設定
            ConfigurableJoint upper = _B.AddComponent<ConfigurableJoint>();
            upper.xMotion = ConfigurableJointMotion.Limited;
            upper.yMotion = ConfigurableJointMotion.Limited;
            upper.zMotion = ConfigurableJointMotion.Limited;
            upper.connectedBody = _A.GetComponent<Rigidbody>();
            upper.autoConfigureConnectedAnchor = false;
            upper.anchor = new Vector3(0, +halfHeight, 0);
            upper.connectedAnchor = new Vector3(0, +halfHeight, 0);
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
            lower.anchor = new Vector3(0, -halfHeight, 0);
            lower.connectedAnchor = new Vector3(0, -halfHeight, 0);

            lower.linearLimit = limitStruct;

            SoftJointLimitSpring lowerSpringStruct = lower.linearLimitSpring;
            lowerSpringStruct.spring = _lowerSpring;
            lowerSpringStruct.damper = _lowerDamper;
            lower.linearLimitSpring = lowerSpringStruct;

            _A = _B;
        }

    }

}
