using UnityEngine;

public class WindowModeChanger : MonoBehaviour
{
    [SerializeField] private bool _fullWindow = false;

    // Update is called once per frame
    private void OnGUI()
    {
        if (GUI.Button(new Rect(1300,20,150,40), "スクリーン切り替え")) _fullWindow = !_fullWindow;
    }
    void Update()
    {
        
        
        if(_fullWindow)
        {
            // ウィンドウモードに変更
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(1920, 1080, false);
        }
        else
        {
            // ウィンドウモードに変更
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(960, 540, false);
        }

    }
}
