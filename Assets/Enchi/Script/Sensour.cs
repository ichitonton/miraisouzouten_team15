using System.Collections.Generic;
using UnityEngine;

public class Sensour : MonoBehaviour
{
    List<Transform> _players = new List<Transform>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<MovePlayerKey>() != null)
        {
            _players.Add(other.transform);
        } 
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<MovePlayerKey>() != null)
        {
            _players.Remove(other.transform);
        }
    }

    public List<Transform>GetPlayers()
    {
        return _players;
    }
}
