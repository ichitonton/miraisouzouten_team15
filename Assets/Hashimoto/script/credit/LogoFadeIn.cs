using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LogoFadeIn : MonoBehaviour
{
	[SerializeField] private float fadeInSeconds = 1.5f;

	private Image image;

	void Awake()
	{
		image = GetComponent<Image>();
	}

	void Start()
	{
		StartCoroutine(FadeIn());
	}

	IEnumerator FadeIn()
	{
		float t = 0f;
		Color c = image.color;

		while (t < fadeInSeconds)
		{
			t += Time.deltaTime;
			c.a = Mathf.Lerp(0f, 1f, t / fadeInSeconds);
			image.color = c;
			yield return null;
		}

		c.a = 1f;
		image.color = c;
	}
}
