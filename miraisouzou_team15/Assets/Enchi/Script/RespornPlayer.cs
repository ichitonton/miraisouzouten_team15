using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;


public class RespornPlayer : MonoBehaviour
{
    [SerializeField] string _respornEreaTag = "Retry_Board";
    [SerializeField] List<Transform> _respornTranses;

    private List<Vector3> _positions;
    // 親オブジェクトを取得

    // 子オブジェクトの数を取得
    int _childCount;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _childCount = this.transform.childCount;

        for (int i = 0; i < _childCount; i++)
        {
            _positions.Add(this.transform.GetChild(i).position);

        }
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.tag == _respornEreaTag)
        {
            Debug.Log("あたったおおおお");
            for (int i = 0; i < _respornTranses.Count; i++)
            {
                this.transform.GetChild(i).position = _positions[i];
            }
        }
    }
}
