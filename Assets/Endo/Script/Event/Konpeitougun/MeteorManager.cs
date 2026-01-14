using UnityEngine;
using Unity.Netcode;

public class MeteorManager : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float spinSpeed = 360f;

    [Header("Konpeito Prefabs (NetworkObject付き)")]
    [SerializeField] private NetworkObject[] konpeitoPrefabs;

    [Header("Impact Knockback")]
    [SerializeField] private float knockbackRadius = 5f;
    [SerializeField] private float knockbackForce = 12f;
    [SerializeField] private float knockbackUpward = 2f;          // explosionForceのupwardsModifier
    [SerializeField] private LayerMask knockbackMask = ~0;        // 影響対象レイヤー（必要なら絞る）
    [SerializeField] private bool affectKonpeitoToo = false;      // 金平糖も巻き込むか（任意）

    [Header("Konpeito Burst (8～11)")]
    [SerializeField] private int minKonpeito = 8;
    [SerializeField] private int maxKonpeito = 11;
    [SerializeField] private float burstSpeed = 4f;
    [SerializeField] private float upward = 4f;
    [SerializeField] private float spawnRadius = 0.25f;

    [Header("Meteor Life")]
    [SerializeField] private float despawnAfterImpact = 0.2f;

    // ---- Network Variables (Server writes) ----
    private readonly NetworkVariable<Vector3> P0 = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Vector3> P1 = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Vector3> P2 = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> StartTime = new(writePerm: NetworkVariableWritePermission.Server); // ServerTime基準
    private readonly NetworkVariable<float> Duration = new(writePerm: NetworkVariableWritePermission.Server);   // flightTime
    private readonly NetworkVariable<bool> Started = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> Impacted = new(writePerm: NetworkVariableWritePermission.Server);

    private Vector3 _prevPos;

    
    public void ServerSetupPath(Vector3 p0, Vector3 p1, Vector3 p2, double startTime, float duration)
    {
        if (!IsServer) return;

        P0.Value = p0;
        P1.Value = p1;
        P2.Value = p2;
        StartTime.Value = startTime;
        Duration.Value = Mathf.Max(0.01f, duration);

        Started.Value = true;
        Impacted.Value = false;

        // 初期位置はp0に固定
        transform.SetPositionAndRotation(p0, Quaternion.identity);
        _prevPos = p0;
    }

    private void Update()
    {
        if (!Started.Value) return;

        // ServerTime基準で全員同じtになる
        double now = NetworkManager.Singleton.ServerTime.Time;
        float t = (float)((now - StartTime.Value) / Duration.Value);

        if (t <= 0f)
        {
            // 開始前は起点で待機
            transform.position = P0.Value;
            _prevPos = transform.position;
            return;
        }

        float tt = Mathf.Clamp01(t);
        Vector3 pos = Bezier(P0.Value, P1.Value, P2.Value, tt);
        transform.position = pos;

        Vector3 vel = pos - _prevPos;
        if (vel.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);

        if (visualRoot != null)
            visualRoot.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);

        _prevPos = pos;

        // 着弾（サーバーだけが処理）
        if (tt >= 1f && IsServer && !Impacted.Value)
        {
            Impacted.Value = true;
            ServerOnImpact(P2.Value);
        }
    }

    private void ServerOnImpact(Vector3 impactPoint)
    {
        
        //エフェクトの再生
        NetworkEffectSpawner.Instance.PlayEffect(2,impactPoint,Quaternion.identity,new Vector3(knockbackRadius = 5f * 2f, knockbackRadius = 5f * 2f, knockbackRadius = 5f * 2f));
        //SEの再生

        //衝撃波を出す
        ServerApplyKnockback(impactPoint);
        //金平糖をはじけさせる
        ServerSpawnKonpeito(impactPoint);

        // 少し待って隕石を消す（見た目の余韻）
        Invoke(nameof(ServerDespawnSelf), despawnAfterImpact);
    }

    private void ServerSpawnKonpeito(Vector3 origin)
    {
        if (konpeitoPrefabs == null || konpeitoPrefabs.Length == 0)
        {
            Debug.LogWarning("[NetworkMeteor] konpeitoPrefabs が空");
            return;
        }

        int count = Random.Range(minKonpeito, maxKonpeito + 1);
        for (int i = 0; i < count; i++)
        {
            var prefab = konpeitoPrefabs[Random.Range(0, konpeitoPrefabs.Length)];

            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = Mathf.Abs(offset.y);

            Vector3 spawnPos = origin + offset;
            Quaternion rot = Random.rotation;

            //var obj = Instantiate(prefab, spawnPos, rot);
            //obj.Spawn(true);

            NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
            if (_ObjectPool == null)
            {
                Debug.LogError("NetworkObjectPool: prefab is NULL");
                return;
            }
            NetworkObject obj = _ObjectPool.Get(prefab.GetComponent<NetworkObject>(), spawnPos, rot);
            obj.Spawn(true);
            obj.GetComponent<PooledNetworkObject>().SetPrefab(prefab.GetComponent<NetworkObject>());
            //GetComponent<PooledNetworkObject>().DestroySelf();

            // 弾けさせる（サーバーで）
            if (obj.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y);
                dir = dir.normalized;

                Vector3 impulse = dir * burstSpeed + Vector3.up * upward;
                rb.AddForce(impulse, ForceMode.Impulse);
                rb.AddTorque(Random.onUnitSphere * 3f, ForceMode.Impulse);
            }
        }
    }


    private void ServerApplyKnockback(Vector3 impactPoint)
    {
        // サーバー権威でのみ
        if (!IsServer) return;

        // OverlapSphereで周囲のColliderを拾う
        Collider[] hits = Physics.OverlapSphere(
            impactPoint,
            knockbackRadius,
            knockbackMask,
            QueryTriggerInteraction.Ignore
        );

        Debug.Log( "当たったオブジェクト数 = " + hits.Length );

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];

            // 自分自身や、Meteorの子コライダーを巻き込まない
            if (col.transform.IsChildOf(transform)) continue;

            // Rigidbodyを探す（Collider直下になければ親を見る）
            Rigidbody rb = col.attachedRigidbody;
            if (rb == null)
                continue;
    
            // 爆発力で吹っ飛ばす（ForceMode.Impulse相当の方が派手なら AddForce でも可）
            rb.AddExplosionForce(
                knockbackForce,
                impactPoint,
                knockbackRadius,
                knockbackUpward,
                ForceMode.Impulse
            );

            var ice = hits[i].GetComponent<IcePillar>();

            //氷柱にダメージを与える
            if(ice != null)
            {

                Vector3 hitPoint = Vector3.zero;
                Vector3 hitDir = Vector3.zero;

                hitPoint = hits[i].ClosestPoint(transform.position);
                hitDir = (hits[i].transform.position - transform.position).normalized;
                if (hitDir.sqrMagnitude < 0.001f) hitDir = Vector3.up;

                ice.ApplyDamageServer(OwnerClientId, hitPoint, hitDir, false, 9f);
            }

            //プレイヤーはスタンさせる
            if (hits[i].gameObject.CompareTag("Player"))
            {
                if(hits[i].gameObject.GetComponent<MovePlayerKey>() != null)
                {
                    //スタンさせる
                    hits[i].gameObject.GetComponent<MovePlayerKey>().Stun(2f);
                    hits[i].gameObject.GetComponent<MovePlayerKey>().PlayCameraShake();
                }
                
            }

        }
    }

    private void ServerDespawnSelf()
    {
        if (!IsServer) return;
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        Vector3 ab = Vector3.Lerp(a, b, t);
        Vector3 bc = Vector3.Lerp(b, c, t);
        return Vector3.Lerp(ab, bc, t);
    }

}
