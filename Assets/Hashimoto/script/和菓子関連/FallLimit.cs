using UnityEngine;

public class FallLimit : MonoBehaviour
{
    [SerializeField] SpawnManager spawnManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag=="Sweets")
        {
            spawnManager.DestroySweets(other.gameObject);
            Destroy(other.gameObject);
        }
    }
}
