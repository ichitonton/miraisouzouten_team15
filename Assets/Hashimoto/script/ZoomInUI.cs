using System.Collections;
using UnityEngine;

public class ZoomInUI : MonoBehaviour
{
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;

    private Coroutine _routine;

    private void OnEnable()
    {
        // ï\é¶Ç≥ÇÍÇΩèuä‘Ç…ÉYÅ[ÉÄIN
        transform.localScale = startScale;

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(CoZoomIn());
    }

    private IEnumerator CoZoomIn()
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float rate = t / duration;

            // easeOutBack Ç¡Ç€Ç¢ãììÆ
            float eased = EaseOutBack(rate);

            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        transform.localScale = endScale;
        _routine = null;
    }

    // ÇøÇÂÇ¢íeÇﬁån
    private float EaseOutBack(float x)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
