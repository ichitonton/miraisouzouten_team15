using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HanayoriDangoEvent : EventBasic
{
    [SerializeField] private NetworkPrefabDatabase _networkPrefabDatabase;

    [SerializeField] private Vector3 _targetCenter = Vector3.zero;
    [SerializeField] private float _radius = 1f;
    [SerializeField] private float _derayTime = 0f;
    [SerializeField] private int _dangoCount = 20;
    [SerializeField] private List<string> _prefbId = new List<string>();

    public override void Event()
    {
        isEvent = true;
        Debug.Log("花より団子のイベント");

        StartCoroutine(SpawnDango());
        _eventTime = 10f;
        Destroy();
    }

    private IEnumerator SpawnDango()
    {
        int count = 0;

        while(count < _dangoCount)
        {
            NetworkObjectSpawner.Instance.RequestSpawnObjectRandomInRange2D(
                _prefbId[Random.Range(0, _prefbId.Count)], _targetCenter, _radius, _targetCenter.y, Quaternion.identity, NetworkObjectSpawner.OwnerMode.Host);
            count++;
            yield return new WaitForSeconds(_derayTime);
        }

    }

    public override void Destroy()
    {
        Destroy(gameObject, _eventTime);
    }
}
