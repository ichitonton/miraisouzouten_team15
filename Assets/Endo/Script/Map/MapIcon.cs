using UnityEngine;

public class MapIcon : MonoBehaviour
{
    [SerializeField] private Camera _mapCamera;
    [SerializeField] RectTransform _mapRect;
    [SerializeField] RectTransform _playerIcon;
    [SerializeField] Transform _player;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        

    }

    // Update is called once per frame
    void Update()
    {

        //mapCameraから見たプレイヤーの位置をRectの0～1の位置に変換
        Vector3 vp = _mapCamera.WorldToViewportPoint(_player.position);

       
        // RectTransform 上の座標へ変換
        float px = (vp.x - 0.5f) * _mapRect.rect.width;
        float pz = (vp.y - 0.5f) * _mapRect.rect.height;

        _playerIcon.anchoredPosition = new Vector2(px, pz);

        // アイコンの角度もつける場合
        _playerIcon.localEulerAngles = new Vector3(0, 0, _player.eulerAngles.y);
    }
}
