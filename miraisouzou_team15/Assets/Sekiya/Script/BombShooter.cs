using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombShooter : MonoBehaviour
{
    [SerializeField] private GameObject _shotBombPrefab;
    [SerializeField] private AudioClip sound;

    void Start()
    {
        // 指定したメソッドを、指定した時間（単位；秒）から、指定した間隔（単位；秒）で繰り返し実行する。
        InvokeRepeating("Shot", 0f, 1f);
    }

    void Shot()
    {
        GameObject shotbomb = Instantiate(_shotBombPrefab, transform.position, Quaternion.identity);
        Rigidbody shotbombRb = shotbomb.GetComponent<Rigidbody>();

        // 弾速は自由に設定
        shotbombRb.AddForce(transform.forward * 500);

        // 発射音を出す
        AudioSource.PlayClipAtPoint(sound, transform.position);

        // ５秒後に砲弾を破壊する
        Destroy(shotbomb, 2.0f);
    }
}