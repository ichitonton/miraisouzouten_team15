using UnityEngine;

public class UICursor : MonoBehaviour
{
    /// マウスポインターを投影するCanvasコンポーネントの参照
    private Canvas _canvas;

    /// マウスポインターを投影するCanvasのRectTransformコンポーネントの参照
    private RectTransform _canvasTransform;

    /// マウスポインターのRectTransformコンポーネントの参照
    private RectTransform _cursorTransform;

    //プレイヤーとすでに同期されているか
    bool _isConnectPlayer = false;

    void Start()
    {
        _canvas = Object.FindFirstObjectByType<Canvas>();
        _canvasTransform = _canvas.GetComponent<RectTransform>();
        _cursorTransform = this.GetComponent<RectTransform>();

    }
    void Update()
    {
        if (_cursorTransform != null)
        {
            // CanvasのRectTransform内にあるマウスの座標をローカル座標に変換する
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasTransform,
                Input.mousePosition,
                _canvas.worldCamera,
                out var mousePosition);

            // ポインターをマウスの座標に移動する
            _cursorTransform.anchoredPosition = new Vector2(mousePosition.x, mousePosition.y);
        }
    }

    public void SetIsConnectPlayer(bool connected)
    {
        _isConnectPlayer = connected;
    }

    public bool GetIsConnectPlayer()
    {
        return _isConnectPlayer;
    }


    void SetCursorTrans(RectTransform rectTransform)
    {
        _cursorTransform = rectTransform;
    }
}
