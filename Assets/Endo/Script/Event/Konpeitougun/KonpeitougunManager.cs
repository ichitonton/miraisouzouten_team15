using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class KonpeitougunManager : NetworkBehaviour
{
    [Header("Origin (固定1つ)")]
    [SerializeField] private Transform spawnOrigin;

    [Header("Impact Area")]
    [SerializeField] private Transform areaCenter;
    [SerializeField] private float areaRadius = 15f;
    [SerializeField] private LayerMask groundMask;

    [Header("Local Visual Prefabs (Non-Network)")]
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private GameObject meteorPrefab;

    [Header("Explosion Hook (Optional)")]
    [SerializeField] private bool playExplosionHere = false;

    [Header("Event Settings")]
    [SerializeField] private int meteorsPerEvent = 10;
    [SerializeField] private float interval = 0.35f;

    [Header("Timing")]
    [SerializeField] private float markerLeadTime = 1.2f; // 予告→着弾
    [SerializeField] private float flightTime = 1.0f;     // 隕石飛来時間

    [Header("Meteor Path Look")]
    [SerializeField] private float arcHeight = 12f;       // 山なり
    [SerializeField] private float lateralOffset = 6f;    // 斜め感（横ズレ）

    public KonpeitougunEvent _konpeitougun = null;

    private NightController _N_controller;

    private bool _running;

    private int _dropSeq = 0;

    // --------------------------
    // Server: start event
    // --------------------------

    [ContextMenu("Start Konpeito Meteor Event (Server)")]
    public void StartEvent_Server()
    {
        if (!IsServer) return;
        if (_running) return;

        // 安全：LeadTime < FlightTime だと開始時刻が過去になる
        if (markerLeadTime < flightTime)
            markerLeadTime = flightTime + 0.2f;
        StartCoroutine(ServerRoutine());

        //夜へチェンジ
        _N_controller = GetComponent<NightController>();
        _N_controller.SetNightServerRpc(true);

        _konpeitougun._eventTime = (meteorsPerEvent * interval) + 2f;

    }

    private IEnumerator ServerRoutine()
    {
        _running = true;

        for (int i = 0; i < meteorsPerEvent; i++)
        {
            if (!TryGetRandomImpactPoint(out Vector3 impactPoint))
            {
                yield return null;
                continue;
            }

            // 1) Marker（NetworkObject）をimpactPointへSpawn
            var marker = Instantiate(markerPrefab, impactPoint, markerPrefab.transform.localRotation);
            marker.GetComponent<NetworkObject>().Spawn(true);
            marker.GetComponent<ImpactMarker>().ServerSetup(markerLeadTime);

            // 2) Meteor（NetworkObject）をspawnOriginへSpawn
            var meteor = Instantiate(meteorPrefab, spawnOrigin.position, Quaternion.identity);
            meteor.GetComponent<NetworkObject>().Spawn(true);

            // 3) 軌道パラメータをサーバーが決めて同期（NetworkVariables）
            double now = NetworkManager.Singleton.ServerTime.Time;
            double impactAt = now + markerLeadTime;
            double startAt = impactAt - flightTime; // meteorが動き始める時刻（ServerTime基準）

            // 制御点計算（斜め感 + 山なり）
            Vector3 p0 = spawnOrigin.position;
            Vector3 p2 = impactPoint;

            Vector3 toTarget = (p2 - p0);
            Vector3 side = Vector3.Cross(Vector3.up, toTarget);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right; // 真上/真下対策
            side.Normalize();

            Vector3 p1 = (p0 + p2) * 0.5f + Vector3.up * arcHeight + side * lateralOffset;

            meteor.GetComponent<MeteorManager>().ServerSetupPath(p0, p1, p2, startAt, flightTime);

            yield return new WaitForSeconds(interval);

            if(i == meteorsPerEvent - 1)
            {
                _N_controller.SetNightServerRpc(false);
            }
        }



        _running = false;
    }

    // --------------------------
    // Utils
    // --------------------------
    private bool TryGetRandomImpactPoint(out Vector3 impactPoint)
    {
        Vector2 r = Random.insideUnitCircle * areaRadius;
        Vector3 candidate = areaCenter.position + new Vector3(r.x, 0f, r.y);

        Vector3 rayStart = candidate + Vector3.up * 250f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 600f, groundMask, QueryTriggerInteraction.Ignore))
        {
            impactPoint = hit.point;
            return true;
        }

        impactPoint = default;
        return false;
    }

    //範囲
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (areaCenter != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(areaCenter.position, areaRadius);
        }
        if (spawnOrigin != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnOrigin.position, 0.5f);
        }
    }
#endif
}
