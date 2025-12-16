using UnityEngine;

public class SnapShotCameraController : MonoBehaviour
{
    [SerializeField] private GameObject _cameraObject;
    private Camera _otherCamera;
    private Camera _camera;

    // Update is called once per frame
    private void Start()
    {

        if (_cameraObject == null) return;
        //スナップショットのカメラ
        _camera = GetComponent<Camera>();
        //メインカメラ
        _otherCamera = _cameraObject.GetComponent<Camera>();
    }
    void Update()
    {
        //メインカメラと同じ位置と回転にする
        transform.position = _cameraObject.transform.position;
        transform.rotation = _cameraObject.transform.rotation;

        //viewPortも同じにする
        _camera.fieldOfView = _otherCamera.fieldOfView;
    }
}
