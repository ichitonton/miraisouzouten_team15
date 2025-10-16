using UnityEngine;
using System.Collections;

//
// 動く床（スタート位置 ↔ Endオブジェクト）を往復移動させる
// - End は床と同じ階層に置くよ（Colliderは切ってね）
// - 速度は「平均時間の目安」。イージングで瞬間速度は変化するよ
// - 端到達後に待ち時間、Ping-Pong 往復に対応
// - シーン上に経路のガイド（Gizmos）も表示
//
public class MovingPlatformEased : MonoBehaviour
{
	[Header("Waypoints")]
	[SerializeField] private Transform endPoint;   // 目的地。きょうだいの End を入れる

	[Header("Motion")]
	[SerializeField, Min(0.01f)]
	private float speed = 2f;          // 平均移動スピード（m/s の目安）
	[SerializeField] private bool pingPong = true; // true=往復、false=片道で停止
	[SerializeField] private float waitAtEnds = 0.2f; // 端で少し待つ秒数
	[SerializeField] private float arriveEpsilon = 0.01f; // 到着判定のしきい値（m）

	[Header("Easing")]
	[Tooltip("イージングカーブ：横軸=経過(0→1) / 縦軸=補間係数(0→1)")]
	[SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
	// 例）もっとメリハリ付けたいなら Inspector でカーブをS字強めに。

	// 内部キャッシュ
	private Vector3 startPos;  // スタートの世界座標（実行開始時の床位置を固定）
	private Vector3 endPos;    // End の世界座標（実行開始時に固定。ゴールが逃げない）
	private bool forward = true;

	private void Awake()
	{
		// 必須チェック
		if (!endPoint)
		{
			Debug.LogError("[MovingPlatformEased] endPoint 未設定。End を割り当てて。", this);
			enabled = false;
			return;
		}

		// 実行開始時の位置を固定しておく
		startPos = transform.position;
		endPos = endPoint.position;

		// 乗り物として安定させるなら Kinematic 推奨
		var rb = GetComponent<Rigidbody>();
		if (rb)
		{
			rb.isKinematic = true;
			rb.interpolation = RigidbodyInterpolation.Interpolate;
		}
	}

	private IEnumerator Start()
	{
		while (true)
		{
			// 今回のレグ（片道）の from/to を決める
			Vector3 from = forward ? startPos : endPos;
			Vector3 to = forward ? endPos : startPos;

			// 片道にかかる時間を距離と速度から見積もる
			float dist = Vector3.Distance(from, to);
			float duration = Mathf.Max(0.0001f, dist / speed);

			// 0→1 の経過にイージングをかけて補間
			float t = 0f;
			while (t < 1f)
			{
				t += Time.deltaTime / duration;
				float eased = easeCurve.Evaluate(Mathf.Clamp01(t)); // 0→1 をカーブで変換
				transform.position = Vector3.LerpUnclamped(from, to, eased);
				yield return null;
			}
			transform.position = to; // 誤差を潰す

			// 端で一息
			if (waitAtEnds > 0f) yield return new WaitForSeconds(waitAtEnds);

			// 片道で終了 or 方向反転
			if (!pingPong) break;
			forward = !forward;
		}
	}

#if UNITY_EDITOR
	// 経路ガイド（シーンビュー用）
	private void OnDrawGizmos()
	{
		if (!endPoint) return;
		// 再生前：現在位置をスタート扱い / 再生中：起点はキャッシュした startPos
		Vector3 sp = Application.isPlaying ? startPos : transform.position;
		Vector3 ep = endPoint.position;

		Gizmos.color = new Color(1f, 0f, 0f, 0.7f);
		Gizmos.DrawLine(sp, ep);
		Gizmos.DrawWireSphere(sp, 0.12f);
		Gizmos.DrawWireSphere(ep, 0.12f);
	}
#endif
}
