using Unity.VisualScripting;
using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] GameObject _blast;
    [SerializeField] float _blastTimer = 1.0f;
    bool _isTimerOn = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    void BlastGenerate()
    {
        if (gameObject.activeSelf)
        {
            bool _isChild = false;

            for (int i = 0; i < transform.childCount; i++)
            {
                //非アクティブの子オブジェクト検索
                Transform _kari = transform.parent.GetChild(i);
                if (_kari.gameObject.GetComponent<Blast>() != null &&
                    !_kari.gameObject.activeSelf)
                {
                    _kari.gameObject.SetActive(true);
                    _kari.position = transform.position;
                    _kari.rotation = transform.rotation;

                    _isChild = true;
                    break;
                }
            }

            //子オブジェクトが足りなければ新規作成
            if (!_isChild)
            {
                Instantiate(_blast, transform.position, transform.rotation, transform.parent);
            }
            gameObject.SetActive(false);
        }
    }

    void OnCollisionEnter(Collision other)
    {
        if (!_isTimerOn)
        {
            Invoke("BlastGenerate", _blastTimer);
            _isTimerOn = true;
        }

    }
}
