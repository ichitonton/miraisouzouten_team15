using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;


public class GameStartUIManager : MonoBehaviour
{
	public static GameStartUIManager Instance { get; private set; }

	[Header("Root")]
	[SerializeField] private GameObject root;

	[Header("Images")]
	[Tooltip("3/2/1で使うメインImage")]
	[SerializeField] private Image countdownImage;

	[Tooltip("Startだけ別Imageで出したい場合（未設定ならcountdownImageを使う）")]
	[SerializeField] private Image startOnlyImage;

	[Header("Sprites")]
	[SerializeField] private Sprite sprite3;
	[SerializeField] private Sprite sprite2;
	[SerializeField] private Sprite sprite1;
	[SerializeField] private Sprite spriteStart;

	[Header("Timing")]
	[SerializeField, Min(0.05f)] private float _delay = 1.0f;
	[SerializeField, Range(0f, 1f)] private float animationRatio = 0.75f;
	[SerializeField, Min(0f)] private float startHoldSeconds = 0.25f;

	[Header("Scale Animation (3/2/1)")]
	[SerializeField, Min(0.01f)] private float startScale = 1.8f;
	[SerializeField, Min(0.01f)] private float endScale = 1.0f;

	[Header("Scale Animation (Start!)")]
	[SerializeField, Min(0.01f)] private float startStartScale = 2.2f; // Start専用：開始
	[SerializeField, Min(0.01f)] private float startEndScale = 1.0f;   // Start専用：終了

	[Header("Alpha Animation")]
	[SerializeField, Range(0f, 1f)] private float startAlpha = 0.0f;
	[SerializeField, Range(0f, 1f)] private float endAlpha = 1.0f;

	[Header("Options")]
	[SerializeField] private bool hideRootAfterFinish = true;

	[Header("Network Sync (Perfect Sync)")]
	[Tooltip("開始予定時刻を送ったあと、全員が準備する猶予（秒）。0.2-0.4おすすめ")]
	[SerializeField, Min(0f)] private float syncLeadSeconds = 0.25f;

	[SerializeField] private NetworkGameStartMessenger messenger;

	[SerializeField] private UnityEvent onStartSpriteShown;

	[Header("BGM Fade Out (Before Start!)")]
	[SerializeField, Min(0f)] private float bgmFadeLeadSeconds = 2.0f; // Start! の何秒前にフェード開始するか
	[SerializeField, Min(0f)] private float bgmFadeOutSeconds = 2.0f; // フェード時間



	private bool _playing = false;

	private bool HasNet => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
	private bool IsServer => HasNet && NetworkManager.Singleton.IsServer;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;

		if (root == null) root = gameObject;
		if (messenger == null) messenger = GetComponent<NetworkGameStartMessenger>();
	}

	private void Start()
	{
		SetRootVisible(false);
		HideAllImages();
	}

	// =========================
	// Net Perfect Sync Start
	// =========================
	public void Play()
	{
		if (!HasNet)
		{
			PlayLocal();
			return;
		}

		if (!IsServer)
		{
			Debug.LogWarning("[GameStartUIManager] ClientはPlay()しないでOK（Hostが開始合図を出す）");
			return;
		}

		double startServerTime = NetworkManager.Singleton.ServerTime.Time + syncLeadSeconds;

		// Host自身も同時刻開始
		PlayAtServerTime(startServerTime);

		// 全員へ開始予定時刻を送る
		messenger?.SendStartToAll(startServerTime);
	}

	public void PlayLocal()
	{
		if (_playing) return;
		StartCoroutine(PlayRoutine());
	}

	public void PlayAtServerTime(double startServerTime)
	{
		if (_playing) return;
		StartCoroutine(WaitAndPlayRoutine(startServerTime));
	}

	public void Stop()
	{
		StopAllCoroutines();
		_playing = false;
		HideAllImages();
		if (hideRootAfterFinish) SetRootVisible(false);
	}

	private IEnumerator WaitAndPlayRoutine(double startServerTime)
	{
		// ここで「準備だけしておく」
		SetRootVisible(true);
		HideAllImages();

		while (HasNet && NetworkManager.Singleton.ServerTime.Time < startServerTime)
			yield return null;

		PlayLocal();
	}

	// =========================
	// Main Countdown Routine
	// =========================
	private IEnumerator PlayRoutine()
	{
		_playing = true;
		SetRootVisible(true);

		if (countdownImage == null)
		{
			Debug.LogError("[GameStartUIManager] countdownImage が未設定です");
			_playing = false;
			yield break;
		}

		HideAllImages();

		// 3/2/1 は countdownImage を使う
		yield return PlayOne(countdownImage, sprite3, startScale, endScale);
		yield return PlayOne(countdownImage, sprite2, startScale, endScale);
		yield return PlayOne(countdownImage, sprite1, startScale, endScale);

		// Start! が出るまでの残り時間（= 直前の "1" 表示が終わった直後なので、基本 _delay だけ先）
		float timeUntilStartSprite = _delay;

		// 0の数秒前からフェードしたい：Start! までの残り - lead
		// lead が _delay より大きい場合は「もう今すぐフェード開始」でOK
		float fadeWait = timeUntilStartSprite - bgmFadeLeadSeconds;
		StartCoroutine(FadeOutBgmBeforeStartSprite(fadeWait));

		// Start! は startOnlyImage があればそっち、無ければ countdownImage を使う
		var startImg = (startOnlyImage != null) ? startOnlyImage : countdownImage;

		// もし別Imageを使うなら、3/2/1のImageは消してから出す
		if (startImg != countdownImage)
			countdownImage.enabled = false;

		// Start! が出た瞬間イベント（既存）
		onStartSpriteShown?.Invoke();

		NetworkSoundManager.Instance.PlaySfx("UI_Start", NetworkSoundManager.SoundScope.LocalOnly, false);

		yield return PlayOne(startImg, spriteStart, startStartScale, startEndScale);


		if (startHoldSeconds > 0f)
			yield return new WaitForSecondsRealtime(startHoldSeconds);

		HideAllImages();

		_playing = false;

		if (hideRootAfterFinish)
			SetRootVisible(false);
	}

	/// <summary>
	/// 1枚のImageを delay の間表示する
	/// 「アニメ区間」と「静止表示区間」を animationRatio で分ける
	/// </summary>
	private IEnumerator PlayOne(Image img, Sprite sprite, float fromScale, float toScale)
	{
		if (img == null)
		{
			yield return new WaitForSecondsRealtime(_delay);
			yield break;
		}

		img.enabled = true;

		if (sprite == null)
		{
			// Spriteが無いなら表示せず待つ
			img.enabled = false;
			yield return new WaitForSecondsRealtime(_delay);
			yield break;
		}

		img.sprite = sprite;

		// 初期化（大きく＆透明）
		SetScale(img, fromScale);
		SetAlpha(img, startAlpha);

		float animTime = Mathf.Max(0.01f, _delay * animationRatio);
		float holdTime = Mathf.Max(0f, _delay - animTime);

		float t = 0f;
		while (t < animTime)
		{
			t += Time.unscaledDeltaTime;

			float p = Mathf.Clamp01(t / animTime);
			float eased = EaseOutCubic(p);

			float s = Mathf.Lerp(fromScale, toScale, eased);
			float a = Mathf.Lerp(startAlpha, endAlpha, eased);

			SetScale(img, s);
			SetAlpha(img, a);

			yield return null;
		}

		// 最終固定
		SetScale(img, toScale);
		SetAlpha(img, endAlpha);

		// 読ませる区間
		if (holdTime > 0f)
			yield return new WaitForSecondsRealtime(holdTime);
	}

	// =========================
	// Helpers
	// =========================
	private void SetRootVisible(bool visible)
	{
		if (root != null) root.SetActive(visible);
	}

	private void HideAllImages()
	{
		if (countdownImage != null) countdownImage.enabled = false;
		if (startOnlyImage != null) startOnlyImage.enabled = false;
	}

	private void SetScale(Image img, float scale)
	{
		if (img == null) return;
		img.rectTransform.localScale = Vector3.one * scale;
	}

	private void SetAlpha(Image img, float alpha01)
	{
		if (img == null) return;
		var c = img.color;
		c.a = alpha01;
		img.color = c;
	}

	private float EaseOutCubic(float x)
	{
		float a = 1f - x;
		return 1f - (a * a * a);
	}

	private IEnumerator FadeOutBgmBeforeStartSprite(float secondsBeforeStart)
	{
		// ネットワーク中はホストだけが指示（全員同期）
		if (HasNet && !IsServer) yield break;

		float wait = Mathf.Max(0f, secondsBeforeStart);
		if (wait > 0f)
			yield return new WaitForSecondsRealtime(wait);

		if (NetworkSoundManager.Instance != null)
		{
			NetworkSoundManager.Instance.StopBgm(
				NetworkSoundManager.SoundScope.AllClients,
				bgmFadeOutSeconds
			);
		}
	}

}
