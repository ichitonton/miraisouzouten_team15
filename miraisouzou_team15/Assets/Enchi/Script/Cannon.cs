using UnityEngine;

public class Cannon : MonoBehaviour
{
    [SerializeField] GameObject _bullet;
    [SerializeField] Transform _bulletTransform;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateBullet();
    }

    void GenerateBullet()
    {
        Debug.Log("ƒEƒ“ƒ`");
        Invoke("GenerateBullet", 3.0f);
    }




}
