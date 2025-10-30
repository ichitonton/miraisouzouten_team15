using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class MousePositionTest : MonoBehaviour
{
    private Transform player;
    private Camera mainCamera;
    [SerializeField] GameObject target;

    private Vector3 currentPosition = Vector3.zero;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            var distance = Vector3.Distance(player.transform.position, mainCamera.transform.position);
            var mousePosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance);

            currentPosition = mainCamera.ScreenToWorldPoint(mousePosition);
            if(target != null)
            target.transform.position = currentPosition;
        }
    }
}