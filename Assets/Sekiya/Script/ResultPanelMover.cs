using System.Collections;
using UnityEngine;

public class ResultPanelMover : MonoBehaviour
{
    [Header("移動先の座標（X, Y）")]
    [SerializeField] private Vector2 targetPosition = Vector2.zero; // 画面中央なら0,0

    [Header("移動にかかる時間（秒）")]
    [SerializeField] private float duration = 1.0f;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // 外部（スポナー）から呼ばれる関数
    public void MoveIn()
    {
        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        float time = 0;

        while (time < duration)
        {
            // 時間経過に合わせて徐々に移動（Lerp）
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPosition, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        // 最後にズレがないようにきっちり合わせる
        rectTransform.anchoredPosition = targetPosition;
    }
}