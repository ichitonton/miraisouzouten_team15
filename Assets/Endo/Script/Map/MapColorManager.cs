using System.Collections;
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

    [Header("Camera Setting")]
    [SerializeField] private Camera mapCamera;
    [SerializeField] private RenderTexture mapTexture;
    [SerializeField] private Material replaceMaterial; // マップ用に色を統一するマテリアル


    [Header("Shader Settings")]
    [SerializeField] private Shader replacementShader; // Unlit系を指定（例：Unlit/Color）

    [Header("Map Color Settings")]
    [SerializeField]
    public List<MapColor> _colorSettings = new List<MapColor>();
    [Header("Color Settings")]
    public Color mapColor = Color.red;

    [Header("Target Layer")]
    [SerializeField] private string mapLayerName = "Map";

   

    [Header("Height Settings")]
    [SerializeField] private Transform _player;//マップの彩度を決めるための変数
    [SerializeField, Range(0.1f, 2f)] private float heightSensitivity = 0.5f;//彩度の変更幅
    [SerializeField] private float minBrightness = 0.5f;
    [SerializeField] private float maxBrightness = 1.5f;

    // 登録されているマップオブジェクト
    private static readonly List<GameObject> registeredObjects = new();


    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       
    }

    // Update is called once per frame
    private void LateUpdate()
    {

        if (mapCamera == null || mapTexture == null || replacementShader == null)
            return;

        // Replacement Shaderを適用
        mapCamera.RenderWithShader(replacementShader, null);

        // マップ色をShaderに渡す（すべて同じ色で描画）
        Shader.SetGlobalColor("_Color", mapColor);

        // 実際にRenderTextureへ描画
        mapCamera.targetTexture = mapTexture;
        //mapCamera.cullingMask = LayerMask.GetMask(mapLayerName);

        Debug.Log("ReplacementShader applied");

        mapCamera.Render();

        // Shader設定を解除（ゲーム描画に影響しないように）
        mapCamera.ResetReplacementShader();
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
