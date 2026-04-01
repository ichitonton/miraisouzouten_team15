using UnityEngine;

public class DontDestroyScene : MonoBehaviour
{

    
    private void Awake()
    {

        DontDestroyOnLoad(gameObject);
    }
}
