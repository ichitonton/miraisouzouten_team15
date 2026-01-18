using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreditsSequence : MonoBehaviour
{
	[Header("Logo (Game Logo)")]
	[SerializeField] private Image logoImage;                 // ゲームロゴImage
	[SerializeField] private RectTransform logoRect;          // ゲームロゴRect
	[SerializeField] private float logoFadeInSeconds = 1.5f;  // フェードイン時間
	[SerializeField] private float logoHoldSeconds = 0.8f;    // フェード後の静止
	[SerializeField] private float logoMoveUpDeltaY = 500f;   // ロゴをどれだけ上に逃がすか

	[Header("Credits")]
	[SerializeField] private RectTransform creditsRect;       // スタッフロールのRect
	[SerializeField] private float creditsScrollSpeed = 80f;  // px/秒（ロゴもこれで動く）

	[Header("Team Logo (After Credits)")]
	[SerializeField] private Image teamLogoImage;             // チームロゴImage
	[SerializeField] private float teamLogoTargetY = 254.5f;  // ここで停止
	[SerializeField] private float teamLogoHoldSeconds = 2.0f;// 中央で静止時間
	[SerializeField] private float teamLogoFadeInSeconds = 0.3f; // チームロゴを軽くフェードインするなら

	[Header("Fade to Black + Title")]
	[SerializeField] private Image fadeOverlayImage;          // 全画面黒Image
	[SerializeField] private float fadeToBlackSeconds = 1.0f;
	[SerializeField] private string titleSceneName = "TitleScene";

	[Header("BGM")]
	[SerializeField] private AudioSource bgm;
	[SerializeField] private float bgmStartVolume = 1f;
	[SerializeField] private float bgmFadeOutSeconds = 1.5f;

	void Awake()
	{
		// ゲームロゴは最初透明
		SetAlpha(logoImage, 0f);

		// チームロゴは最初は非表示運用でもOK
		if (teamLogoImage)
		{
			// 最初FalseにしたいならここでfalseにしとけばOK（InspectorでFalseでもOK）
			// teamLogoImage.gameObject.SetActive(false);
			SetAlpha(teamLogoImage, 0f);
		}

		// 黒オーバーレイも最初透明（最初FalseでもOK）
		if (fadeOverlayImage)
		{
			// fadeOverlayImage.gameObject.SetActive(false); // InspectorでFalseでもOK
			SetAlpha(fadeOverlayImage, 0f);
		}
	}

	void Start()
	{
		StartCoroutine(Sequence());
	}

	IEnumerator Sequence()
	{
		// BGM開始
		if (bgm)
		{
			bgm.volume = Mathf.Clamp01(bgmStartVolume);
			if (!bgm.isPlaying) bgm.Play();
		}

		// 1) ゲームロゴ フェードイン
		yield return StartCoroutine(FadeImageAlpha(logoImage, 0f, 1f, logoFadeInSeconds));

		// 2) ロゴ表示しきってから静止
		if (logoHoldSeconds > 0f)
			yield return new WaitForSeconds(logoHoldSeconds);

		// 3) ゲームロゴ上移動（クレジットと同じ速度） + クレジットスクロール（同時）
		bool logoDone = false;
		bool creditsDone = false;

		StartCoroutine(MoveUpAtSpeed_WithDone(logoRect, logoMoveUpDeltaY, creditsScrollSpeed, () => logoDone = true));
		StartCoroutine(ScrollCreditsUp_UntilOffscreen_WithDone(creditsRect, creditsScrollSpeed, () => creditsDone = true));

		while (!logoDone || !creditsDone)
			yield return null;

		// 4) クレジット終わったら、表示整理（好み）
		if (creditsRect) creditsRect.gameObject.SetActive(false);
		if (logoRect) logoRect.gameObject.SetActive(false);

		// 5) チームロゴを表示（最初FalseでもここでONにする）
		if (teamLogoImage && !teamLogoImage.gameObject.activeSelf)
			teamLogoImage.gameObject.SetActive(true);

		// チームロゴをフェードイン（軽く）
		if (teamLogoImage)
			yield return StartCoroutine(FadeImageAlpha(teamLogoImage, teamLogoImage.color.a, 1f, teamLogoFadeInSeconds));

		// 6) チームロゴを下から上げて targetY で停止
		//    その瞬間にBGMフェードアウト開始
		yield return StartCoroutine(
			MoveToY_AndTriggerBgmFade(
				teamLogoImage ? teamLogoImage.rectTransform : null,
				teamLogoTargetY,
				creditsScrollSpeed,
				bgm,
				bgmFadeOutSeconds
			)
		);

		// 7) 中央で静止
		if (teamLogoHoldSeconds > 0f)
			yield return new WaitForSeconds(teamLogoHoldSeconds);

		// 8) 黒フェード（最初FalseでもここでONにする）→ タイトルへ
		if (fadeOverlayImage && !fadeOverlayImage.gameObject.activeSelf)
			fadeOverlayImage.gameObject.SetActive(true);

		yield return StartCoroutine(FadeImageAlpha(fadeOverlayImage, 0f, 1f, fadeToBlackSeconds));
		SceneManager.LoadScene(titleSceneName);
	}

	// ---- helpers ----

	static void SetAlpha(Image img, float a)
	{
		if (!img) return;
		var c = img.color;
		c.a = Mathf.Clamp01(a);
		img.color = c;
	}

	IEnumerator FadeImageAlpha(Image img, float from, float to, float seconds)
	{
		if (!img) yield break;

		if (seconds <= 0f)
		{
			SetAlpha(img, to);
			yield break;
		}

		float t = 0f;
		Color c = img.color;
		while (t < seconds)
		{
			t += Time.deltaTime;
			c.a = Mathf.Lerp(from, to, t / seconds);
			img.color = c;
			yield return null;
		}
		c.a = to;
		img.color = c;
	}

	IEnumerator FadeOutBgm(AudioSource source, float seconds)
	{
		if (!source) yield break;

		float start = source.volume;
		if (seconds <= 0f)
		{
			source.volume = 0f;
			source.Stop();
			yield break;
		}

		float t = 0f;
		while (t < seconds)
		{
			t += Time.deltaTime;
			source.volume = Mathf.Lerp(start, 0f, t / seconds);
			yield return null;
		}

		source.volume = 0f;
		source.Stop();
	}

	IEnumerator MoveUpAtSpeed_WithDone(RectTransform rt, float deltaY, float speed, System.Action onDone)
	{
		if (!rt) { onDone?.Invoke(); yield break; }

		float startY = rt.anchoredPosition.y;
		float targetY = startY + deltaY;

		while (rt.anchoredPosition.y < targetY)
		{
			var p = rt.anchoredPosition;
			p.y += speed * Time.deltaTime;
			rt.anchoredPosition = p;
			yield return null;
		}

		var end = rt.anchoredPosition;
		end.y = targetY;
		rt.anchoredPosition = end;

		onDone?.Invoke();
	}

	// ★「y=900で止める」仕様を撤廃：画面外へ完全に抜けるまで流す
	IEnumerator ScrollCreditsUp_UntilOffscreen_WithDone(RectTransform rt, float speed, System.Action onDone)
	{
		if (!rt) { onDone?.Invoke(); yield break; }

		// 画面外判定を安定させるため、毎フレームcornersを見る
		var corners = new Vector3[4];

		while (true)
		{
			var p = rt.anchoredPosition;
			p.y += speed * Time.deltaTime;
			rt.anchoredPosition = p;

			// 全コーナーが画面の上より上に行ったら終了（完全に見えなくなった）
			rt.GetWorldCorners(corners);
			bool allAbove = true;
			for (int i = 0; i < 4; i++)
			{
				Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
				if (sp.y <= Screen.height)
				{
					allAbove = false;
					break;
				}
			}

			if (allAbove) break;

			yield return null;
		}

		onDone?.Invoke();
	}

	IEnumerator MoveToY_AndTriggerBgmFade(RectTransform rt, float targetY, float speed, AudioSource bgm, float bgmFadeSeconds)
	{
		if (!rt) yield break;

		bool fadeStarted = false;

		while (rt.anchoredPosition.y < targetY)
		{
			var p = rt.anchoredPosition;
			p.y += speed * Time.deltaTime;
			rt.anchoredPosition = p;

			// target到達の瞬間に一度だけBGMフェード開始
			if (!fadeStarted && p.y >= targetY)
			{
				fadeStarted = true;
				if (bgm) StartCoroutine(FadeOutBgm(bgm, bgmFadeSeconds));
			}

			yield return null;
		}

		// ピタ止め
		var end = rt.anchoredPosition;
		end.y = targetY;
		rt.anchoredPosition = end;

		// 保険
		if (!fadeStarted)
		{
			if (bgm) StartCoroutine(FadeOutBgm(bgm, bgmFadeSeconds));
		}
	}
}
