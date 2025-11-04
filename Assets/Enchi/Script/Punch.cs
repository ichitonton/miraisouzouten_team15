using UnityEngine;

public class Punch : MonoBehaviour
{
    float _punchForce = 10.0f;
    float _stunTime = 1.0f;
    float _punchDamage = 10.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void OnTriggerEnter(Collider other)
    {
        if (other != this.transform.parent && other.GetComponent<Rigidbody>() != null)
        {
            Debug.Log("punch : " + other.name);

            if (other.GetComponent<MovePlayerKey>() != null)
            {
                other.GetComponent<MovePlayerKey>().ToGetPunch(transform.parent.GetComponent<MovePlayerKey>().GetPunchDamage());
            }

            other.GetComponent<Rigidbody>().AddForce((this.transform.forward + Vector3.up * 0.1f) * _punchForce, ForceMode.Impulse);
        }
    }
}
