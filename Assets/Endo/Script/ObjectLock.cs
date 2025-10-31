using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectLock : MonoBehaviour
{
    [SerializeField] private float _maxDistance = 100f;
    [SerializeField] private LayerMask _layermask = ~0;

    private bool _locked = false;
    private GameObject _lockedObject;

    private float _currentTime;
    [SerializeField]private float _unLockTime = 10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (_locked)
        {
            _currentTime += Time.deltaTime;
            if(_currentTime > _unLockTime)
            {
                _lockedObject.GetComponent<Rigidbody>().isKinematic = false;
                _locked = false;
                _currentTime = 0f;
            }

            return;
        }
        

        //マウスの左クリックを押したら
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            //マウスカーソルのスクリーン座標を取得
            Vector2 mousePos = Mouse.current.position.ReadValue();

            //カメラからマウス位置に向かってRayを飛ばす
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            //Raycastでヒットチェックを行う
            if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _layermask))
            {
                Debug.Log($"Hit: {hit.collider.name} at {hit.point}");
                _lockedObject =  hit.collider.gameObject;

                _lockedObject.GetComponent<Rigidbody>().isKinematic = true;
                _locked = true;

            }
        }

        

    }
}
