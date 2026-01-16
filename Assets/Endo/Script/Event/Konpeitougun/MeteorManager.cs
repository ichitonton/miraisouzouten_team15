using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;


public class MeteorManager : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float spinSpeed = 360f;


    // ==============================
    // ★追加：炎エフェクト（Attached）
    // ==============================
    [Header("Trail Fire Effect (Attached)")]
    [SerializeField] private int[] fireEffectId;                 // 炎エフェクトID（例）
                    // 炎エフェクトID（例）

    [SerializeField] private Vector3 fireLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 fireAxisOffsetEuler = Vector3.zero; // 向き補正（逆なら Y=180 など）
    [SerializeField] private bool fireFaceAgainstVelocity = true;  // 炎が後ろ向きなら true
    [SerializeField] private float fireRotationSlerp = 25f;         // 回転追従速度（0なら即反映）

    
    private List<Transform> _fireEffectTransform = new List<Transform>();   // ★生成した炎のTransformを掴む
    private bool _fireSpawnRequested = false; // ★二重生成防止

    [SerializeField] private GameObject _fireSpawnPrefab = null;

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

        transform.SetPositionAndRotation(p0, Quaternion.identity);
        _prevPos = p0;

        // ★追加：炎エフェクトを「隕石にアタッチ生成」
        ServerSpawnFireEffectOnce();
    }

    //エフェクト生成
    private void ServerSpawnFireEffectOnce()
    {
        //if (!IsServer) return;
        if (_fireSpawnRequested) return;
        _fireSpawnRequested = true;

        if (NetworkEffectSpawner.Instance == null)
        {
            Debug.LogWarning("[MeteorManager] NetworkEffectSpawner.Instance が null です");
            return;
        }

        // parent直下に生成
        NetworkEffectSpawner.Instance.PlayEffectAttached(
            13,
            GetComponent<NetworkObject>(),
            transform.position,
            transform.rotation// 初期回転（補正も込み）
        );
        //// parent直下に生成
        //NetworkEffectSpawner.Instance.PlayEffectAttached(
        //    15,
        //    GetComponent<NetworkObject>(),
        //    transform.position,
        //    transform.rotation// 初期回転（補正も込み）
        //);

        // ★生成した実体Transformを掴みに行く（1フレ待ってから探す）
        StartCoroutine(CoResolveFireEffectTransform());
    }

    private IEnumerator CoResolveFireEffectTransform()
    {
        // 生成直後だと子がまだ増えてないことがあるので1フレ待つ
        yield return null;

        // 目印（EffectIdMarker）を持つ子を探す
        var markers = GetComponentsInChildren<EffectIdMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null && markers[i].effectId == fireEffectId[i])
            {
                _fireEffectTransform.Add(markers[i].transform);
                break;
            }
        }

        if (_fireEffectTransform == null)
        {
            Debug.LogWarning("[MeteorManager] 炎エフェクトのTransformが見つかりません（EffectIdMarkerをPrefabに付けてる？）");
        }
    }

    private void Update()
    {
        if (!Started.Value) return;

        double now = NetworkManager.Singleton.ServerTime.Time;
        float t = (float)((now - StartTime.Value) / Duration.Value);

        if (t <= 0f)
        {
            transform.position = P0.Value;
            _prevPos = transform.position;
            return;
        }

        float tt = Mathf.Clamp01(t);
        Vector3 pos = Bezier(P0.Value, P1.Value, P2.Value, tt);
        transform.position = pos;

        Vector3 vel = pos - _prevPos;

        // 隕石本体の向き
        if (vel.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);

        // ★追加：炎エフェクトを進行方向に向ける
        UpdateFireFacing(vel);

        
        //Debug.Log(transform.rotation);
       // _fireSpawnPrefab.transform.rotation = transform.rotation;
        //Debug.Log(_fireSpawnPrefab.transform.rotation);

        if (visualRoot != null)
            visualRoot.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);

        _prevPos = pos;

        if (tt >= 1f && IsServer && !Impacted.Value)
        {
            Impacted.Value = true;
            ServerOnImpact(P2.Value);
        }
    }

    // ============================
    // ★追加：炎の回転制御（本体）
    // ============================
    private void UpdateFireFacing(Vector3 vel)
    {
        if (_fireEffectTransform == null) return;
        if (vel.sqrMagnitude < 0.000001f) return;

        Vector3 dir = vel.normalized;

        // 炎は基本「移動方向の逆」を向けると自然（尾を引く）
        if (fireFaceAgainstVelocity)
            dir = -dir;

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);

        // 補正（必要なら）
        Quaternion offset = Quaternion.Euler(fireAxisOffsetEuler);

        Quaternion target = look * offset;

        foreach (Transform t in _fireEffectTransform)
        {

            if (fireRotationSlerp <= 0f)
            {
                t.rotation = target;
            }
            else
            {
                t.rotation = Quaternion.Slerp(
                    t.rotation,
                    target,
                    Time.deltaTime * fireRotationSlerp
                );
            }
        }
    }



    private void ServerOnImpact(Vector3 impactPoint)
    {
        
        //エフェクトの再生
        NetworkEffectSpawner.Instance.PlayEffect(14,impactPoint,Quaternion.identity,new Vector3(knockbackRadius, knockbackRadius, knockbackRadius));
        //エフェクトの再生
        //NetworkEffectSpawner.Instance.PlayEffect(2, impactPoint, Quaternion.identity, new Vector3(knockbackRadius * 2, knockbackRadius * 2, knockbackRadius * 2));
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
