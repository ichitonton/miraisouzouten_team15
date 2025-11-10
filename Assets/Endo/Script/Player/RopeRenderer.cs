using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeRenderer : NetworkBehaviour
{
    private Transform _player1;
    private Transform _player2;

    private LineRenderer _line;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
        if(_player1 != null&&_player2 != null)
        {
            //紐の両端をプレイヤーの位置にする
            _line.SetPosition(0, _player1.position);
            _line.SetPosition(1,_player2.position);
        }

    }

    public void RegisterPlayers(Transform player1, Transform player2)
    {
        
        _player1 = player1.transform;
        _player2 = player2.transform;

        _line = GetComponent<LineRenderer>();

        //線をつなぐ頂点数を2にする
        _line.positionCount = 2;

        //線の太さ
        _line.startWidth = 0.1f;
        _line.endWidth = 0.1f;
    }

}
