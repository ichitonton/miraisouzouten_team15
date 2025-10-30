using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SwampArea : MonoBehaviour
{
	[SerializeField] float dampingValue = 0.5f;

	void OnTriggerEnter(Collider other)
	{
		if (other.GetComponent<MovePlayerKey>() != null)
		{
			other.GetComponent<MovePlayerKey>().SetMoveSpeedDamp(dampingValue);
        }
	}
	void OnTriggerExit(Collider other)
	{
		if (other.GetComponent<MovePlayerKey>() != null)
		{
			other.GetComponent<MovePlayerKey>().SetMoveSpeedInitial();
		}
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
	{
		var col = GetComponent<BoxCollider>();
		if (!col) return;
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawWireCube(col.center, col.size);
	}
#endif
}
