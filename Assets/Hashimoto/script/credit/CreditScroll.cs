using UnityEngine;

public class CreditScroll : MonoBehaviour
{
    public RectTransform content;
    public float speed = 50f;

    void Update()
    {
        content.anchoredPosition += Vector2.up * speed * Time.deltaTime;
    }
}
