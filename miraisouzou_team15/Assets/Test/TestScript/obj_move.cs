using UnityEngine;

public class obj_move : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    [SerializeField] float speed = 3.0f;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.position += speed * transform.forward * Time.deltaTime;
            // Time.deltaTime フレームレートに関わらず一定の速度でオブジェクトを移動させることができる
            // 要はフレームレートに依存させない仕組み
        }
		if (Input.GetKey(KeyCode.A))
		{
			transform.position -= speed * transform. right* Time.deltaTime;
		}
		if (Input.GetKey(KeyCode.S))
		{
			transform.position -= speed * transform.forward * Time.deltaTime;
		}
		if (Input.GetKey(KeyCode.D))
		{
			transform.position += speed * transform.right * Time.deltaTime;
		}
	}
}
