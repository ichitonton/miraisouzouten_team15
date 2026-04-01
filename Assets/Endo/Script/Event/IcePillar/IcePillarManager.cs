using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー権限で、範囲内に氷柱をランダム生成するイベント実行役
/// - RunEvent() を呼べば、ホスト/サーバーで生成が走る
/// - クライアントから呼ぶ場合は ServerRpc 経由でサーバーに依頼する
/// </summary>

public class IcePillarManager : NetworkBehaviour
{
    [Header("Spawn Area")]
    [SerializeField] private Transform areaCenter;
    [SerializeField, Min(0f)] private float spawnRadius = 12f;

    [Header("How Many Pillars")]
    [SerializeField, Min(1)] private int pillarCount = 3;
    [SerializeField, Min(0f)] private float minDistanceBetweenPillars = 2.5f;

    [Header("Ground Check (Raycast)")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0f)] private float rayStartHeight = 20f;
    [SerializeField, Min(0f)] private float rayMaxDistance = 60f;

    [Header("Pillar Prefab (NetworkObject 必須)")]
    [SerializeField] private GameObject pillarPrefab;

    [Header("Spawn Try")]
    [SerializeField, Min(1)] private int maxTryPerPillar = 25;

    private readonly List<Vector3> _spawnedXZ = new();

    /// <summary>
    /// イベント開始（どこから呼んでもOK）
    /// - サーバーならそのまま開始
    /// - クライアントならサーバーに依頼
    /// </summary>
    public void StartEvent_Server()
    {
        if (IsServer)
        {
            StartCoroutine(SpawnRoutineServer());
        }
        else
        {
            RequestRunEventServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRunEventServerRpc()
    {
        // ここで「イベント権限」チェックを入れたい場合は入れる（管理者のみ等）
        StartCoroutine(SpawnRoutineServer());
    }

    private IEnumerator SpawnRoutineServer()
    {
        if (!IsServer) yield break;

        _spawnedXZ.Clear();

        var center = (areaCenter != null) ? areaCenter.position : transform.position;

        for (int i = 0; i < pillarCount; i++)
        {
            if (TryGetSpawnPoint(center, out var hit, out var xz))
            {
                // ヨーだけランダム


                var rot = pillarPrefab.transform.rotation;
                //rot.y = Random.Range(0f, 360f);

                // サーバーで生成 → Spawn で全クライアントに同期される
                var pillar = Instantiate(pillarPrefab, hit.point, rot);
                Debug.Log("氷のrot = " + rot);
                pillar.GetComponent<NetworkObject>().Spawn(true);

                _spawnedXZ.Add(xz);

                yield return new WaitForSeconds(0.15f);
            }
            else
            {
                Debug.LogWarning("[IcePillarEventRunnerNet] 生成位置が見つかりませんでした（groundMask/半径/試行回数を見直し）");
            }
        }
    }

    private bool TryGetSpawnPoint(Vector3 center, out RaycastHit hit, out Vector3 chosenXZ)
    {
        for (int t = 0; t < maxTryPerPillar; t++)
        {
            var rnd = Random.insideUnitCircle * spawnRadius;
            var xz = new Vector3(rnd.x, 0f, rnd.y);

            var rayOrigin = center + xz + Vector3.up * rayStartHeight;

            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, rayMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                chosenXZ = new Vector3(hit.point.x, 0f, hit.point.z);

                // 近すぎ防止（XZ）
                for (int i = 0; i < _spawnedXZ.Count; i++)
                {
                    if (Vector3.Distance(_spawnedXZ[i], chosenXZ) < minDistanceBetweenPillars)
                    {
                        chosenXZ = default;
                        goto NEXT_TRY;
                    }
                }
                return true;
            }

        NEXT_TRY:
            continue;
        }

        hit = default;
        chosenXZ = default;
        return false;
    }

    //範囲
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (areaCenter != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(areaCenter.position, spawnRadius);
        }
        if (areaCenter != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(areaCenter.position, 0.5f);
        }
    }
#endif
}
