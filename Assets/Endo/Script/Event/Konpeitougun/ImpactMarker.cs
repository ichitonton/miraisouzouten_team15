using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ImpactMarker : NetworkBehaviour
{
    [SerializeField] private float baseScale = 1.7f;
    [SerializeField] private bool pulse = true;
    [SerializeField] private float pulseSpeed = 7f;


    public void ServerSetup(float lifeTime)
    {
        transform.localScale = Vector3.one * baseScale;
        //少し上に出す
        Vector3 pos = transform.localPosition;
        pos.y += 0.2f;
        transform.localPosition = pos;
        
        Destroy(gameObject, Mathf.Max(0.05f, lifeTime));
    }

    private IEnumerator DespawnAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private void Update()
    {
        // 見た目は各端末でローカルに脈動させればOK（同期不要）
        if (!pulse) return;
        float s = (Mathf.Sin(Time.time * pulseSpeed) * 0.2f) + 1f;
        transform.localScale = Vector3.one * baseScale * s;
    }

}
