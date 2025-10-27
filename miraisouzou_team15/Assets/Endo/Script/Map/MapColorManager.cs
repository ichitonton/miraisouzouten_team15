using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapColor 
{
    //Mapオブジェクトに適応する名前
    public string _tagName;
    //初手の色は全部白
    public Color _color = Color.white;
}

public class MapColorManager : MonoBehaviour
{
    

    [SerializeField] private Camera mapCamera;
    [SerializeField] private RenderTexture mapTexture;
    [SerializeField] private Material replaceMaterial; // マップ用に色を統一するマテリアル

    [Header("Map Color Settings")]
    [SerializeField]
    public List<MapColor> _colorSettings = new List<MapColor>();

    [Header("Target Layer")]
    [SerializeField] private string mapLayerName = "Map";

   

    [Header("Height Settings")]
    [SerializeField] private Transform _player;//マップの彩度を決めるための変数
    [SerializeField, Range(0.1f, 2f)] private float heightSensitivity = 0.5f;//彩度の変更幅
    [SerializeField] private float minBrightness = 0.5f;
    [SerializeField] private float maxBrightness = 1.5f;

    // 登録されているマップオブジェクト
    private static readonly List<GameObject> registeredObjects = new();
    // タグ→マテリアルキャッシュ
    private Dictionary<string, Material> _tagToMaterial = new();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 各タグに対応するUnlitマテリアルを作成してキャッシュ
        foreach (var entry in _colorSettings)
        {
            var mat = new Material(Shader.Find("Unlit/Color"))
            {
                color = entry._color
            };
            _tagToMaterial[entry._tagName] = mat;
        }
    }

    // Update is called once per frame
    private void LateUpdate()
    {
        if (_player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return;
            _player = GameObject.FindGameObjectWithTag("Player").transform;
            
        }

        float playerY = _player.position.y;

        Debug.Log(registeredObjects.Count);

        foreach (var obj in registeredObjects)
        {
            if (obj == null) continue;

            if (_tagToMaterial.TryGetValue(obj.tag, out var baseMat))
            {
                var renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 高低差による明暗補正
                    float heightDiff = obj.transform.position.y - playerY;
                    float brightness = Mathf.Clamp(1f + heightDiff * heightSensitivity, minBrightness, maxBrightness);

                    // 一時的なマテリアルを作らずに色だけ変更
                    Color c = baseMat.color * brightness;
                    renderer.material.color = c;
                }
            }
        }

        // 描画
        if (mapCamera != null && mapTexture != null)
        {
            //マップUIに移す
            mapCamera.targetTexture = mapTexture;
            mapCamera.cullingMask = LayerMask.GetMask(mapLayerName);
            mapCamera.Render();
        }
    }

    public static void Register(GameObject obj)
    {
        //マップUIに移すオブジェクトの登録
        foreach (var r in obj.GetComponentsInChildren<Renderer>())
        {
            if (obj != null && !registeredObjects.Contains(obj))
                registeredObjects.Add(r.gameObject);
        }
            
    }

    public static void Unregister(GameObject obj)
    {
        //マップUIに移すオブジェクトの解除
        foreach (var r in obj.GetComponentsInChildren<Renderer>())
        {
            if (obj != null)
                registeredObjects.Remove(obj);
        }
            
    }

}
