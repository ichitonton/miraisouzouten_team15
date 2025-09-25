using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; 

public class Scene : MonoBehaviour
{

    [SerializeField] private string LoadScene;

    public void SceneChange()
    {
        SeManager seManager = SeManager.Instance;
        seManager.SettingPlaySE();
        SceneManager.LoadScene(LoadScene);
    }

}
