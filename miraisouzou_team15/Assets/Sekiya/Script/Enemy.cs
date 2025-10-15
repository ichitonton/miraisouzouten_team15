using System.Collections;
using System.Collections.Generic;
using ExplosionSample;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

public class Enemy : MonoBehaviour
{
    public Transform target;
    private NavMeshAgent agent;
    public Transform random;
    bool sensor;
    public float speed;
    [Header("îöïóÇÃPrefab")][SerializeField] private Explosion _explosionPrefab;


    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (sensor == false)
        {
            agent.destination = random.transform.position;
        }
        else
        {
            agent.destination = target.transform.position;
        }
    }
    public void tuiseki()
    {
        sensor = true;
    }

    public void haikai()
    {
        sensor = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            Explode();
        }
    }
    private void Explode()
    {
        // îöî≠Çê∂ê¨
        var explosion = Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
        explosion.Explode();

        // é©êgÇÕè¡Ç¶ÇÈ
        Destroy(gameObject);
    }
}