using UnityEngine;

public class GoldenWagashiObject : MonoBehaviour
{
    [SerializeField] private float _offset = 0f;

    private int _effectId = 12;
    private bool _doOnce = false;

    [Header("Drag SFX")]
    [SerializeField] private float zeroEpsilon = 0.0001f; // ほぼ0判定用

    private Vector3 _prevPos;
    private bool _isGrounded = false;
    private bool _isDragPlaying = false;

    private const string DragTag = "SE_Gorilla_Drag"; // ずりずり
    private const string LandTag = "SE_Gorilla_Land"; // ずどーん

    private void Start()
    {
        _prevPos = transform.position;
    }

    private void Update()
    {
        Vector3 now = transform.position;
        float moveDist = Vector3.Distance(now, _prevPos);
        _prevPos = now;

        bool isMoving = moveDist > zeroEpsilon;

        // ===== 判定ここだけ =====
        if (_isGrounded && isMoving)
        {
            if (!_isDragPlaying)
            {
                _isDragPlaying = true;
                NetworkSoundManager.Instance.StartLoopSfx(
                    DragTag,
                    NetworkSoundManager.SoundScope.AllClients,
                    true,
                    transform.position
                );
            }
        }
        else
        {
            if (_isDragPlaying)
            {
                _isDragPlaying = false;
                NetworkSoundManager.Instance.StopLoopSfx(
                    DragTag,
                    NetworkSoundManager.SoundScope.AllClients
                );
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Field")) return;

        _isGrounded = true;

        if (!_doOnce)
        {
            GameObject child = transform.GetChild(0).gameObject;
            Vector3 childPos = child.transform.position;

            NetworkEffectSpawner.Instance.PlayEffect(_effectId, childPos, Quaternion.identity);

            // 着地SE（ずどーん）
            NetworkSoundManager.Instance.PlaySfx(
                LandTag,
                NetworkSoundManager.SoundScope.AllClients,
                true,
                childPos
            );

            _doOnce = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Field")) return;

        _isGrounded = false;

        // 空中に出たら即止め
        if (_isDragPlaying)
        {
            _isDragPlaying = false;
            NetworkSoundManager.Instance.StopLoopSfx(
                DragTag,
                NetworkSoundManager.SoundScope.AllClients
            );
        }
    }
}
