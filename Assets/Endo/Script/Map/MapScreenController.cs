using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;

public class MapScreenController : MonoBehaviour
{

    [Header("Snapshot")]
    [SerializeField] private Camera snapshotCamera;
    [SerializeField] private RenderTexture snapshotRT;

    [Header("UI")]
    [SerializeField] private GameObject mapRoot;   // 全体マップのパネル
    [SerializeField] private RawImage blurBG;      // 背景ブラー用 RawImage

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab; // とりあえずTabとか

    private bool _isOpen = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        // 念のためここでも紐付け
        if (blurBG != null && snapshotRT != null)
        {
            blurBG.texture = snapshotRT;
        }


        // 最初は非表示
        if (mapRoot != null) mapRoot.SetActive(false);
        if (blurBG != null) blurBG.gameObject.SetActive(false);

        // カメラは常にONでRTを更新し続ける
        if (snapshotCamera != null)
        {
            snapshotCamera.enabled = true;
            snapshotCamera.targetTexture = snapshotRT;
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMap();
        }
    }

    public void ToggleMap()
    {
        if (_isOpen)
            CloseMap();
        else
            OpenMap().Forget(); // UniTask を fire-and-forget
    }

    private async UniTaskVoid OpenMap()
    {
        _isOpen = true;

        // 1フレーム待ってカメラ位置などが最新になるのを待つ
        await UniTask.WaitForEndOfFrame();

       
        // 背景ブラーUIとマップUIを表示
        if (blurBG != null) blurBG.gameObject.SetActive(true);
        if (mapRoot != null) mapRoot.SetActive(true);
    }

    private void CloseMap()
    {
        _isOpen = false;

        if (mapRoot != null) mapRoot.SetActive(false);
        if (blurBG != null) blurBG.gameObject.SetActive(false);
    }

}
