using UnityEngine;

public class Punch : MonoBehaviour
{
    float _punchForce = 10.0f;
    float _stunTime = 1.0f;
    int _punchDamage = 10;
    MovePlayerKey _player;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _player = transform.parent.GetComponent<MovePlayerKey>();
        if (_player != null)
        {
            _punchForce = _player.GetPunchForce();
            _stunTime = _player.GetStunTime();
            _punchDamage = _player.GetPunchDamage();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other != this.transform.parent && other.GetComponent<Rigidbody>() != null)
        {
            Debug.Log("punch : " + other.name);

            if (other.GetComponent<MovePlayerKey>() != null)
            {
                other.GetComponent<MovePlayerKey>().ToGetPunch(_punchDamage, _stunTime);
            }

            other.GetComponent<Rigidbody>().AddForce((this.transform.forward + Vector3.up * 0.1f) * _punchForce, ForceMode.Impulse);
        }
    }
}
