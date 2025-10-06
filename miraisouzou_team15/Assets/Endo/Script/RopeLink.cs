using UnityEngine;

public class RopeLink : MonoBehaviour
{
    [Header("ƒWƒ‡ƒCƒ“ƒg‘ŠŽè")]
    [SerializeField] private GameObject _connectedPbject;

    private Rigidbody _rb;
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
