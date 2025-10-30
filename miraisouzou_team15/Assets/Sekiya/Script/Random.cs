using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class randomScript : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(Warp());
    }

    private IEnumerator Warp()
    {
        while (true)
        {
            // 10秒後ごとにワープ移動する。
            yield return new WaitForSeconds(5f);

            // ランダムな値を取得する。
            float posX = Random.Range(-120, 120);
            float posZ = Random.Range(-200, 200);

            transform.position = new Vector3(posX, 0, posZ);
        }
    }
}