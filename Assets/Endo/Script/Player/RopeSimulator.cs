using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class RopeSimulator : MonoBehaviour
{

    [SerializeField] private Transform _player1;
    [SerializeField] private Transform _player2;

    [SerializeField] private int _segmentCount = 20;
    [SerializeField] float _ropeLength = 10f;
    [SerializeField] float _stiffness = 0.9f;
    [SerializeField] float _bounceStrength = 50f;

    private List<Vector3> _points;
    private List<Vector3> _prevPoints;
    private float _segmentLength;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        _points = new List<Vector3>();
        _prevPoints = new List<Vector3>();
        _segmentLength = _ropeLength / _segmentCount;

        for(int i = 0;i < _segmentCount;i++)
        {
            //プレイヤーをつなぐ間に区切りを設置
            Vector3 p = Vector3.Lerp(_player1.position,_player2.position,(float)i/_segmentCount);
            _points.Add(p);
            _prevPoints.Add(p);
        }
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        //Verlet積分
        for (int i = 1; i < _points.Count; i++)
        {
            Vector3 velocity = _points[i] - _prevPoints[i];
            _prevPoints[i] = _points[i];
            _points[i] += velocity + Physics.gravity * Time.fixedDeltaTime * Time.fixedDeltaTime;

        }

        //距離制約
        for (int iter = 0; iter < 5; iter++)
        {
            _points[0] = _player1.position;
            _points[_points.Count - 1] = _player2.position;
            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector3 delta = _points[i + 1] - _prevPoints[i];
                float dist = delta.magnitude;
                float diff = (dist - _segmentLength) / dist;
                Vector3 correction = delta * 0.5f * diff * _stiffness;
                _points[i] += correction;
                _points[i + 1] -= correction;
            }
        }

        //反発
        float currrentLength = Vector3.Distance(_player1.position, _player2.position);
        if(currrentLength > _ropeLength * 1.1f)
        {
            Vector3 dir = (_player2.position - _player1.position).normalized;
            
        }

    }
}
