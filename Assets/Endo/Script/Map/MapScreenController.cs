using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;

public class MapScreenController : MonoBehaviour
{

    [Header("Snapshot")]
    [SerializeField] private Camera _snapshotCamera;
    [SerializeField] private RenderTexture _snapshotRT;

    [Header("MapCamera")]
    [SerializeField] private Camera _mapCamera;

    [Header("UI")]
    [SerializeField] private GameObject mapRoot;   // 全体マップのパネル
    [SerializeField] private RawImage blurBG;      // 背景ブラー用 RawImage

    [Header("OtherUI")]
    [SerializeField] private GameObject _otherUI;   // 全体マップのパネル

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab; // とりあえずTabとか


    private MapColorManager _colorManager;
    private MapIconManager _iconManager;
    private PlayerMapIconManager _playerMapIconManager;

    private bool _isOpen = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        // 念のためここでも紐付け
        if (blurBG != null && _snapshotRT != null)
        {
            blurBG.texture = _snapshotRT;
        }


        // 最初は非表示
        if (mapRoot != null) mapRoot.SetActive(false);
        if (blurBG != null) blurBG.gameObject.SetActive(false);

        // カメラは常にONでRTを更新し続ける
        if (_snapshotCamera != null)
        {
            _snapshotCamera.enabled = true;
            _snapshotCamera.targetTexture = _snapshotRT;
        }

        _colorManager = GetComponent<MapColorManager>();
        _iconManager = GetComponent<MapIconManager>();
        _playerMapIconManager = GetComponent<PlayerMapIconManager>();

        StartCoroutine(SystemOff());

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
        if(_snapshotCamera != null) _snapshotCamera.gameObject.SetActive(true);
        if (_mapCamera != null) _mapCamera.gameObject.SetActive(true);
        if(_colorManager != null) _colorManager.enabled = true;
        if (_colorManager != null) _iconManager.enabled = true;
        if (_colorManager != null) _playerMapIconManager.enabled = true;
        if (blurBG != null) blurBG.gameObject.SetActive(true);
        if (mapRoot != null) mapRoot.SetActive(true);
        if(_otherUI != null) _otherUI.gameObject.SetActive(false);
    }

    private void CloseMap()
    {
        _isOpen = false;

        if (_snapshotCamera != null) _snapshotCamera.gameObject.SetActive(false);
        if (_mapCamera != null) _mapCamera.gameObject.SetActive(false);
        if (_colorManager != null) _colorManager.enabled = false;
        if (_colorManager != null) _iconManager.enabled = false;
        if (_colorManager != null) _playerMapIconManager.enabled = false;
        if (mapRoot != null) mapRoot.SetActive(false);
        if (blurBG != null) blurBG.gameObject.SetActive(false);
        if (_otherUI != null) _otherUI.gameObject.SetActive(true);
    }

    private IEnumerator SystemOff()
    {
        //1秒後にすべてオフ
        yield return  new WaitForSeconds(1.0f);
        CloseMap();
    }

}
