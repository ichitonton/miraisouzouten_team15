using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class NetworkObjectRandomGenerator : NetworkBehaviour
{
	[Header("データベース")]
	[SerializeField] private NetworkPrefabDatabase _database;

	[Header("スポーン基準点（Pivot）")]
	[SerializeField] private Transform[] _pivots;

	[Header("スポーン範囲")]
	[SerializeField, Range(0f, 50f)] private float _spawnRadius = 3f;

	[Tooltip("同じ場所に生成しない判定の最小距離（メートル）。大きいほど重複しにくい")]
	[SerializeField, Range(0.1f, 10f)] private float _minDistanceBetweenSpawns = 1.0f;

	[Tooltip("1体生成するために、位置抽選を何回までやり直すか")]
	[SerializeField, Range(1, 200)] private int _maxAttemptsPerSpawn = 40;

	[Header("地面 / 衝突（任意）")]
	[SerializeField] private bool _snapToGround = true;

	[Tooltip("地面判定Rayの開始高さ")]
	[SerializeField, Range(0.1f, 50f)] private float _groundRayStartHeight = 10f;

	[Tooltip("地面判定Rayの長さ")]
	[SerializeField, Range(0.1f, 200f)] private float _groundRayLength = 50f;

	[SerializeField] private LayerMask _groundMask = ~0;

	[Tooltip("このレイヤーに当たる場所には生成しない（壁・障害物など）")]
	[SerializeField] private LayerMask _blockedMask = 0;

	[Tooltip("障害物チェック半径。0ならOverlapチェックしない")]
	[SerializeField, Range(0f, 5f)] private float _blockedCheckRadius = 0.5f;

	[Header("レアリティ確率（%）")]
	[SerializeField, Range(0f, 100f)] private float _rarity1Percent = 60f;
	[SerializeField, Range(0f, 100f)] private float _rarity2Percent = 30f;
	[SerializeField, Range(0f, 100f)] private float _rarity3Percent = 10f;

	[Header("スポーン間隔")]
	[Tooltip("合計で何個生成するか（MaxAliveで止まる方が優先される）")]
	[SerializeField, Min(1)] private int _totalSpawnCount = 30;

	[Tooltip("生成タイミングごとに一気に生む数（例: 5）")]
	[SerializeField, Min(1)] private int _burstSpawnCount = 5;

	[Tooltip("何秒ごとにバースト生成するか")]
	[SerializeField, Range(0f, 60f)] private float _spawnInterval = 3.0f;

	[Header("最大同時存在数（停止条件）")]
	[Tooltip("場に存在できる最大数。到達したらコルーチンを止める")]
	[SerializeField, Min(1)] private int _maxAliveObjects = 20;

	[Tooltip("開始時に自動で生成を開始")]
	[SerializeField] private bool _spawnOnNetworkSpawn = true;

	[Tooltip("Pivotを全体で使い捨てにする（trueだと同じpivotは二度と使わない）")]
	[SerializeField] private bool _useUniquePivotOverall = false;

	[Header("乱数（任意）")]
	[SerializeField] private bool _useFixedSeed = false;
	[SerializeField] private int _fixedSeed = 12345;

	[Header("デバッグ")]
	[SerializeField] private bool _drawGizmos = true;


	// ----- runtime -----
	private Coroutine _spawnRoutine;

    // 「同じ場所に生成しない」用（グリッド化してHashSet管理）
    private HashSet<Vector3Int> _occupiedCells = new HashSet<Vector3Int>();
    private float _cellSize;

    // pivot選択用
    private List<int> _pivotPool = new List<int>();

    // 場に存在しているSpawn済みNetworkObject
    private readonly HashSet<NetworkObject> _aliveObjects = new HashSet<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (_useFixedSeed)
            UnityEngine.Random.InitState(_fixedSeed);

        _cellSize = Mathf.Max(0.05f, _minDistanceBetweenSpawns);
        BuildPivotPool();

        if (_spawnOnNetworkSpawn)
            StartSpawning();
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    [ContextMenu("StartSpawning")]
    public void StartSpawning()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[RandomGenerator] サーバー（Host/Server）でのみ生成します。");
            return;
        }

        if (_database == null)
        {
            Debug.LogError("[RandomGenerator] Databaseが未設定です。");
            return;
        }

        if (_pivots == null || _pivots.Length == 0)
        {
            Debug.LogError("[RandomGenerator] Pivotが未設定です。");
            return;
        }

        _database.Initialize(); // 念のため
        _cellSize = Mathf.Max(0.05f, _minDistanceBetweenSpawns);

        // null掃除（破棄済みをカウントしない）
        _aliveObjects.RemoveWhere(o => o == null);

        BuildPivotPool();

        if (_spawnRoutine != null)
            StopCoroutine(_spawnRoutine);

        _spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    [ContextMenu("StopSpawning")]
    public void StopSpawning()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        int spawnedTotal = 0;
        int failsInARow = 0;

        while (spawnedTotal < _totalSpawnCount)
        {
            _aliveObjects.RemoveWhere(o => o == null);

            // ★ MaxAlive到達で停止（要望）
            if (_aliveObjects.Count >= _maxAliveObjects)
            {
                Debug.Log($"[RandomGenerator] MaxAlive({_maxAliveObjects})に到達したので生成を停止します。");
                break;
            }

            // このタイミングで何個生むか（Burst / Total / MaxAlive の制約）
            int canSpawnByTotal = _totalSpawnCount - spawnedTotal;
            int canSpawnByAlive = _maxAliveObjects - _aliveObjects.Count;
            int burst = Mathf.Min(_burstSpawnCount, canSpawnByTotal, canSpawnByAlive);

            if (burst <= 0)
            {
                Debug.Log("[RandomGenerator] 生成可能数が0のため停止します。");
                break;
            }

            // ★ 同じタイミング内は必ず別pivot（要望）
            var usedPivotThisBurst = new HashSet<int>();

            int succeededThisBurst = 0;

            for (int i = 0; i < burst; i++)
            {
                bool ok = TrySpawnOne(usedPivotThisBurst);
                if (ok)
                {
                    succeededThisBurst++;
                    spawnedTotal++;
                    failsInARow = 0;
                }
                else
                {
                    failsInARow++;

                    // pivotが足りない/置けない状況が続くなら停止（無限防止）
                    if (_useUniquePivotOverall && _pivotPool.Count == 0)
                    {
                        Debug.LogWarning("[RandomGenerator] Pivotを使い切ったため生成を終了します。");
                        _spawnRoutine = null;
                        yield break;
                    }

                    if (failsInARow >= 50)
                    {
                        Debug.LogWarning("[RandomGenerator] 生成失敗が続いたため生成を終了します（配置場所が無い可能性）。");
                        _spawnRoutine = null;
                        yield break;
                    }
                }
            }

            // interval待ち（0でもフリーズしない）
            if (_spawnInterval > 0f) yield return new WaitForSeconds(_spawnInterval);
            else yield return null;

            // このバーストで1個も出せない状況が続くと無限になり得るので保険
            if (succeededThisBurst == 0 && failsInARow >= 10)
            {
                Debug.LogWarning("[RandomGenerator] バーストで生成できない状態が続くため終了します。");
                break;
            }
        }

        _spawnRoutine = null;
    }

    // ===== 1体生成（バースト内pivot重複禁止対応） =====
    private bool TrySpawnOne(HashSet<int> usedPivotThisBurst)
    {
        // 1) レアリティ抽選
        int rarity = RollRarity();

        // 2) そのレアリティのPrefab候補を集める
        var candidates = GetPrefabsByRarity(rarity);
        if (candidates.Count == 0) candidates = GetAllPrefabs();
        if (candidates.Count == 0) return false;

        // 3) 位置抽選してスポーン
        for (int attempt = 0; attempt < _maxAttemptsPerSpawn; attempt++)
        {
            if (!TryPickPivot(usedPivotThisBurst, out var pivot, out var pivotIndex))
                return false;

            if (!TryGetRandomPointAroundPivot(pivot, out var pos))
                continue;

            if (IsOccupied(pos))
                continue;

            if (!IsPlaceable(pos))
                continue;

            // 4) Prefab決定
            var prefab = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (prefab == null) continue;

            var go = Instantiate(prefab, pos, Quaternion.identity);

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[RandomGenerator] Prefab '{prefab.name}' に NetworkObject が付いていません。");
                Destroy(go);
                return false;
            }

            // 追跡用コンポーネントを付与（Despawnでaliveから外す）
            var tracker = go.GetComponent<RandomSpawnTracker>();
            if (tracker == null) tracker = go.AddComponent<RandomSpawnTracker>();
            tracker.Init(this);

            netObj.Spawn();

            _aliveObjects.Add(netObj);
            MarkOccupied(pos);

            // このバースト内で使ったpivotとして記録（必ず別pivot）
            usedPivotThisBurst.Add(pivotIndex);

            return true;
        }

        return false;
    }

    // ===== pivot選択（バースト内の重複を避ける） =====
    private bool TryPickPivot(HashSet<int> usedPivotThisBurst, out Transform pivot, out int pivotIndex)
    {
        pivot = null;
        pivotIndex = -1;

        // pivotPoolは「候補pivotのindex一覧」
        if (_useUniquePivotOverall)
        {
            // 全体でも使い捨て：poolが空なら終了
            if (_pivotPool.Count == 0) return false;

            // バースト内で未使用のものを探す
            for (int t = 0; t < 200; t++)
            {
                int pick = UnityEngine.Random.Range(0, _pivotPool.Count);
                int idx = _pivotPool[pick];
                if (usedPivotThisBurst.Contains(idx)) continue;

                pivot = _pivots[idx];
                pivotIndex = idx;

                // 全体でも使い捨てなのでpoolから削除
                _pivotPool.RemoveAt(pick);
                return pivot != null;
            }

            // バースト内で使えるpivotが残ってない
            return false;
        }
        else
        {
            // 全体では再利用OK：ただしバースト内は別pivot
            if (_pivotPool.Count == 0) BuildPivotPool();
            if (_pivotPool.Count == 0) return false;

            // バースト内で未使用のpivotを選ぶ
            // pivot数よりburstが大きい場合、ここで詰むのでfalseになる（安全）
            int safety = Mathf.Min(500, _pivotPool.Count * 10);
            for (int t = 0; t < safety; t++)
            {
                int pick = UnityEngine.Random.Range(0, _pivotPool.Count);
                int idx = _pivotPool[pick];
                if (usedPivotThisBurst.Contains(idx)) continue;

                pivot = _pivots[idx];
                pivotIndex = idx;
                return pivot != null;
            }

            return false;
        }
    }

    private void BuildPivotPool()
    {
        _pivotPool.Clear();
        if (_pivots == null) return;
        for (int i = 0; i < _pivots.Length; i++)
        {
            if (_pivots[i] != null)
                _pivotPool.Add(i);
        }
    }

    private bool TryGetRandomPointAroundPivot(Transform pivot, out Vector3 pos)
    {
        pos = pivot.position;

        Vector2 r = UnityEngine.Random.insideUnitCircle * _spawnRadius;
        Vector3 candidate = pivot.position + new Vector3(r.x, 0f, r.y);

        if (_snapToGround)
        {
            Vector3 rayStart = candidate + Vector3.up * _groundRayStartHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out var hit, _groundRayLength, _groundMask, QueryTriggerInteraction.Ignore))
            {
                candidate = hit.point;
            }
            else
            {
                return false;
            }
        }

        
        return true;
    }

    private bool IsPlaceable(Vector3 pos)
    {
        if (_blockedMask != 0 && _blockedCheckRadius > 0f)
        {
            var hits = Physics.OverlapSphere(pos, _blockedCheckRadius, _blockedMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0) return false;
        }
        return true;
    }

    // ===== rarity roll =====
    private int RollRarity()
    {
        float a = Mathf.Max(0f, _rarity1Percent);
        float b = Mathf.Max(0f, _rarity2Percent);
        float c = Mathf.Max(0f, _rarity3Percent);
        float sum = a + b + c;
        if (sum <= 0.0001f) return 1;

        float r = UnityEngine.Random.value * sum;
        if (r < a) return 1;
        r -= a;
        if (r < b) return 2;
        return 3;
    }

    // ===== database helpers =====
    private List<GameObject> GetAllPrefabs()
    {
        return _database._entries
            .Where(e => e != null && e._prefab != null)
            .Select(e => e._prefab)
            .ToList();
    }

    private List<GameObject> GetPrefabsByRarity(int rarity)
    {
        rarity = Mathf.Clamp(rarity, 1, 3);

        // Database Entry に「public int Rarity => _rarity;」がある前提
        return _database._entries
            .Where(e => e != null && e._prefab != null && e.Rarity == rarity)
            .Select(e => e._prefab)
            .ToList();
    }

    // ===== occupancy =====
    private Vector3Int ToCell(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / _cellSize);
        int y = Mathf.FloorToInt(pos.y / _cellSize);
        int z = Mathf.FloorToInt(pos.z / _cellSize);
        return new Vector3Int(x, y, z);
    }

    private bool IsOccupied(Vector3 pos)
    {
        return _occupiedCells.Contains(ToCell(pos));
    }

    private void MarkOccupied(Vector3 pos)
    {
        _occupiedCells.Add(ToCell(pos));
    }

    // ===== alive tracking callback =====
    internal void NotifyDespawn(NetworkObject obj)
    {
        if (obj == null) return;
        _aliveObjects.Remove(obj);
    }

    // ===== gizmo =====
    private void OnDrawGizmosSelected()
    {
        if (!_drawGizmos) return;
        if (_pivots == null) return;

        Gizmos.color = Color.cyan;
        foreach (var p in _pivots)
        {
            if (p == null) continue;
            Gizmos.DrawWireSphere(p.position, _spawnRadius);
        }
    }

    // ===== helper component（同じファイルに置いてOK） =====
    private class RandomSpawnTracker : NetworkBehaviour
    {
        private NetworkObjectRandomGenerator _owner;

        public void Init(NetworkObjectRandomGenerator owner)
        {
            _owner = owner;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) _owner?.NotifyDespawn(NetworkObject);
        }

        private void OnDestroy()
        {
            // まれにDespawn経由せずDestroyされる保険
            if (IsServer) _owner?.NotifyDespawn(NetworkObject);
        }
    }
}
