using UnityEngine;

public class GoldenWagashiObject : MonoBehaviour
{
    [SerializeField] private float _offset = 0f;

    private int _effectId = 12;
    private bool _doOnce = false;


    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Field" && !_doOnce)
        {
            GameObject child = transform.GetChild(0).gameObject;
            Vector3 childPos = child.transform.position;

            NetworkEffectSpawner.Instance.PlayEffect(_effectId, childPos, Quaternion.identity);
            _doOnce = true;
        }
    }

}
