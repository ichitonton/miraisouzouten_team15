using System.Collections;
using UnityEngine;

public class CameraMover : MonoBehaviour
{
    [Header("移動先の場所（空のオブジェクトを置いて指定）")]
    [SerializeField] private Transform targetTransform;

    [Header("移動にかかる時間（秒）")]
    [SerializeField] private float duration = 2.0f;

    // 監督から呼ばれる
    public void MoveCamera()
    {
        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float time = 0;

        while (time < duration)
        {
            // 現在の進行度（0〜1）
            float t = time / duration;

            // 滑らかに変化させる（EaseInOut的な動き）
            // ※もっと単純な等速移動がいいなら t をそのまま使ってください
            float smoothT = t * t * (3f - 2f * t);

            // 位置と回転を徐々に変化
            transform.position = Vector3.Lerp(startPos, targetTransform.position, smoothT);
            transform.rotation = Quaternion.Lerp(startRot, targetTransform.rotation, smoothT);

            time += Time.deltaTime;
            yield return null;
        }

        // 最後にズレを補正してピッタリ合わせる
        transform.position = targetTransform.position;
        transform.rotation = targetTransform.rotation;
    }
}