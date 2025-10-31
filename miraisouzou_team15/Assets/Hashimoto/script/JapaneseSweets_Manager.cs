using UnityEngine;

public class JapaneseSweets_Manager : MonoBehaviour
{
	// Start is called once before the first execution of Update after the MonoBehaviour is created

	[SerializeField] private SpawnManager SpawnManager;

    [SerializeField] private float weight = 0.0f;               // オブジェクトの重さ


	private void Start()
	{
	
	}

	public float GetWeight() {
        return weight;
    }

	public void SetReset()
	{
		Destroy(this.gameObject);
	}


}
