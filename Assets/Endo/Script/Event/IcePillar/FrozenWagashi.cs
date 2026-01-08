using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 和菓子の Frozen 状態を NetworkVariable で同期し、
/// - Frozen中：NetworkTransform を止めて各端末で pillar 追従（LateUpdate）
/// - Frozen解除：現在の追従位置を確定してから NetworkTransform を復帰（スナップ抑制）
/// </summary>
public class FrozenWagashi : NetworkBehaviour
{
    [Header("Frozen Behavior")]
    [SerializeField] private bool disableColliderWhileFrozen = true;
    [SerializeField] private bool makeRigidbodyKinematicWhileFrozen = true;

    [Header("Network Components")]
    [SerializeField] private NetworkTransform netTransform; // Frozen中OFF, 解除でON

    [Header("Snap Fix (Optional)")]
    [Tooltip("Frozen解除時、サーバーがTeleportで一度だけ確定姿勢を配信してスナップを抑える")]
    [SerializeField] private bool serverTeleportOnUnfreeze = true;

    private readonly NetworkVariable<bool> _frozen = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 追従先（ローカル参照）
    private Transform _followTarget;
    private Vector3 _followLocalPos;
    private Quaternion _followLocalRot;

    // 「解除した瞬間に1回だけ」使うためのフラグ
    private bool _justUnfrozen;

    private void Awake()
    {
        if (netTransform == null) netTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        _frozen.OnValueChanged += OnFrozenChanged;
        ApplyFrozenState(_frozen.Value);
    }

    private void OnDestroy()
    {
        _frozen.OnValueChanged -= OnFrozenChanged;
    }

    private void LateUpdate()
    {
        // Frozen中だけ追従（NetworkTransformは止まっている前提）
        if (_frozen.Value && _followTarget != null)
        {
            var p = _followTarget.TransformPoint(_followLocalPos);
            var r = _followTarget.rotation * _followLocalRot;

            transform.SetPositionAndRotation(p, r);
        }

        // Frozen解除直後に、追従位置を1回だけ確定してから NetworkTransform を復帰
        if (_justUnfrozen)
        {
            _justUnfrozen = false;

            // 追従ターゲットが残っているなら「その位置」を最終確定
            if (_followTarget != null)
            {
                var p = _followTarget.TransformPoint(_followLocalPos);
                var r = _followTarget.rotation * _followLocalRot;
                transform.SetPositionAndRotation(p, r);
            }

            // 追従はここで切る（以後はネット同期/物理に任せる）
            _followTarget = null;

            // NetworkTransform を復帰（全端末で実行される）
            if (netTransform != null) netTransform.enabled = true;

            // 任意：サーバーは1回だけTeleportで確定姿勢を配信して、
            // クライアント側の補間ズレによるスナップをさらに減らす
            if (serverTeleportOnUnfreeze && IsServer && netTransform != null)
            {
                netTransform.Teleport(transform.position, transform.rotation, transform.localScale);
            }
        }
    }

    public void OnFrozenChanged(bool prev, bool next)
    {
        ApplyFrozenState(next);

        // true -> false に変わった瞬間（解除）をマーク
        if (prev && !next)
        {
            // 解除の処理は LateUpdate でまとめて行う（見た目の最終確定が安定）
            _justUnfrozen = true;
        }

        // false -> true（凍結開始）時は特に何もしない（追従情報は AttachFollowClientRpc で入る）
    }

    private void ApplyFrozenState(bool frozen)
    {
        // Frozen中は NetworkTransform を止める（全端末で効く）
        if (netTransform != null)
            netTransform.enabled = !frozen;

        // Rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb != null && makeRigidbodyKinematicWhileFrozen)
        {
            rb.isKinematic = frozen;
            rb.useGravity = !frozen;

            if (frozen)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#endif
            }
        }

        // Collider
        if (disableColliderWhileFrozen)
        {
            var cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) c.enabled = !frozen;
        }
    }

    /// <summary>
    /// サーバーから呼ぶ：追従ターゲットとローカルオフセットを全クライアントに配布
    /// </summary>
    public void AttachFollowServer(Transform pillarRoot, Vector3 localPos, Quaternion localRot)
    {
        if (!IsServer) return;

        var pillarNO = pillarRoot.GetComponent<NetworkObject>();
        if (pillarNO == null)
        {
            Debug.LogWarning("[FrozenWagashi] pillarRoot に NetworkObject がありません");
            return;
        }

        AttachFollowClientRpc(pillarNO.NetworkObjectId, localPos, localRot);
    }

    [ClientRpc]
    private void AttachFollowClientRpc(ulong pillarNetworkObjectId, Vector3 localPos, Quaternion localRot)
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(pillarNetworkObjectId, out var pillarNo))
        {
            _followTarget = pillarNo.transform;
            _followLocalPos = localPos;
            _followLocalRot = localRot;
        }
    }

    /// <summary>
    /// サーバーから凍結状態を変更（クライアントへ同期される）
    /// </summary>
    public void SetFrozenServer(bool frozen)
    {
        if (!IsServer) return;
        _frozen.Value = frozen;
    }
}
