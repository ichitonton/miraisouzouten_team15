using UnityEngine;

public class UICursor : MonoBehaviour
{
    /// マウスポインターを投影するCanvasコンポーネントの参照
    [SerializeField] private Canvas _canvas;

    /// マウスポインターを投影するCanvasのRectTransformコンポーネントの参照
    [SerializeField] private RectTransform _canvasTransform;

    /// マウスポインターのRectTransformコンポーネントの参照
    [SerializeField] private RectTransform _cursorTransform;

    void Update()
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
