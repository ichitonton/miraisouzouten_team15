using UnityEngine;

public class Item : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }


    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<MovePlayerKey>() != null)
        {
            Destroy(this.gameObject);
        }
    }
}
