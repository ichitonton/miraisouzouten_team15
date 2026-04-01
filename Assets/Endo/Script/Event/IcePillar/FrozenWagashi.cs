using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// FrozenWagashi
/// - Frozen中：柱(NetworkObject)に親子付けして固定（NetworkTransformは切らない）
/// - 重要：子にRigidbodyがあると親移動に追従しないため、Frozen中は全RigidbodyをKinematic化する
/// - スケール：lossyScale補正で「親のスケール継承」を打ち消す
/// </summary>
public class FrozenWagashi : NetworkBehaviour
{
    [Header("Start State")]
    [SerializeField] private bool startFrozen = false;

    [Header("Frozen Behavior")]
    [SerializeField] private bool disableColliderWhileFrozen = true;

    [Header("Scale Fix")]
    [SerializeField, Min(0.01f)] private float frozenScaleMultiplier = 1.0f;

    private readonly NetworkVariable<bool> _frozen = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 子Rigidbody問題対策：元のKinematic状態を保存
    private readonly Dictionary<Rigidbody, bool> _rbOriginalKinematic = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer) _frozen.Value = startFrozen;

        _frozen.OnValueChanged += OnFrozenChanged;
        ApplyFrozenState(_frozen.Value);
    }

    private void OnDestroy()
    {
        _frozen.OnValueChanged -= OnFrozenChanged;
    }

    private void OnFrozenChanged(bool prev, bool next)
    {
        ApplyFrozenState(next);
    }

    private void ApplyFrozenState(bool frozen)
    {
        // Collider
        if (disableColliderWhileFrozen)
        {
            var cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) c.enabled = !frozen;
        }

        // Rigidbody（重要：子も含めて全部）
        var rbs = GetComponentsInChildren<Rigidbody>(true);

        if (frozen)
        {
            _rbOriginalKinematic.Clear();

            foreach (var rb in rbs)
            {
                if (rb == null) continue;

                // 元状態保存
                _rbOriginalKinematic[rb] = rb.isKinematic;

                rb.isKinematic = true;
                rb.useGravity = false;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#endif
            }
        }
        else
        {
            // 元に戻す（保存がないRbは触らない）
            foreach (var rb in rbs)
            {
                if (rb == null) continue;

                if (_rbOriginalKinematic.TryGetValue(rb, out var wasKinematic))
                {
                    rb.isKinematic = wasKinematic;
                    rb.useGravity = !wasKinematic;
                }
            }
        }
    }

    /// <summary>
    /// 柱へ固定（サーバー）
    /// localPos/localRot は柱Root基準
    /// </summary>
    public void AttachToPillarServer(NetworkObject pillarRootNO, Vector3 localPos, Quaternion localRot)
    {
        if (!IsServer) return;
        if (pillarRootNO == null) return;

        // 見た目サイズを維持するために、親子付け前の見た目スケールを保持
        Vector3 desiredWorldScale = transform.lossyScale;

        _frozen.Value = true; // ← 先にFrozenにして物理を止める

        // 親子付け（ここが失敗すると絶対くっつかない）
        bool ok = NetworkObject.TrySetParent(pillarRootNO, true);
        if (!ok)
        {
            Debug.LogWarning($"[FrozenWagashi] TrySetParent failed. pillar={pillarRootNO.name}", this);
            return;
        }

        // 位置・回転をローカルで固定
        transform.localPosition = localPos;
        transform.localRotation = localRot;

        // 親の見た目スケールを打ち消して、和菓子の見た目サイズを維持
        Vector3 parentWorldScale = pillarRootNO.transform.lossyScale;
        transform.localScale = SafeDivide(desiredWorldScale, parentWorldScale) * frozenScaleMultiplier;
    }

    /// <summary>
    /// 解放（サーバー）
    /// </summary>
    public void DetachServer()
    {
        if (!IsServer) return;

        // 解除前の見た目サイズを保持
        Vector3 desiredWorldScale = transform.lossyScale;

        NetworkObject.TryRemoveParent(true);

        // 親が外れるので、そのまま見た目サイズに合わせる
        transform.localScale = desiredWorldScale;

        _frozen.Value = false;
    }

    public void SetFrozenServer(bool frozen)
    {
        if (!IsServer) return;
        _frozen.Value = frozen;
    }

    private static Vector3 SafeDivide(Vector3 a, Vector3 b)
    {
        return new Vector3(
            b.x != 0f ? a.x / b.x : a.x,
            b.y != 0f ? a.y / b.y : a.y,
            b.z != 0f ? a.z / b.z : a.z
        );
    }
}
