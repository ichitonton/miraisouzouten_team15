using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombShooter : MonoBehaviour
{
    [SerializeField] private GameObject _shotBombPrefab;
    [SerializeField] private AudioClip sound;
    [SerializeField] private float _shotInterval = 5f;
    [SerializeField] private float _shotForce = 500f;
    [SerializeField] private float _bombLifetime = 2f;

    void Start()
    {
        // 指定したメソッドを、指定した時間（単位；秒）から、指定した間隔（単位；秒）で繰り返し実行する。
        InvokeRepeating("Shot", 0f, _shotInterval);
    }

    void Shot()
    {
        GameObject shotbomb = Instantiate(_shotBombPrefab, transform.position, transform.rotation);
        Rigidbody shotbombRb = shotbomb.GetComponent<Rigidbody>();

        // 水平な方向ベクトルを作成
        Vector3 direction = new Vector3(transform.forward.x, 0.0f, transform.forward.z).normalized;

        // 水平な方向に力を加える
        shotbombRb.AddForce(direction * _shotForce);

        // 発射音を出す
        AudioSource.PlayClipAtPoint(sound, transform.position);

        // ５秒後に砲弾を破壊する
        Destroy(shotbomb, _bombLifetime);
    }
}