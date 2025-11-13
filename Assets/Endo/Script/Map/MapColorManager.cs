using System.Collections.Generic;
using System.Data.Common;
using UnityEngine;

[System.Serializable]
public class MapColor
{
    public string _tagName;          // ここは「名前キー」として使う（例：Field, fall）
    public Color _color = Color.white;
}

public class MapColorManager : MonoBehaviour
{
    [Header("Camera Setting")]
    [SerializeField] private Camera mapCamera;
    [SerializeField] private RenderTexture mapTexture;

    [Header("Shader Settings")]
    [SerializeField] private Shader replacementShader; // MapUnlit（URP Unlit）を指定

    [Header("Map Color Settings")]
    [SerializeField] public List<MapColor> _colorSettings = new List<MapColor>();
    [SerializeField] private Color _defaultColor = Color.gray; // ヒットしない時の色

    [Header("Target Layer")]
    [SerializeField] private string mapLayerName = "Map";

    [Header("Height Settings")]
    [SerializeField] private Transform _player;                 // 高低差の基準
    [SerializeField, Range(0.0f, 2f)] private float heightSensitivity = 0.4f;
    [SerializeField] private float minBrightness = 0.6f;
    [SerializeField] private float maxBrightness = 1.4f;

    private static readonly List<GameObject> registeredObjects = new();
    private readonly Dictionary<string, Color> _nameToColor = new();
    private MaterialPropertyBlock _mpb;

    private Material drawMat;

    private void Awake()
    {

        _mpb = new();
        drawMat = new Material(replacementShader); // ← ここで1回だけ生成！

        // 色テーブル初期化
        _nameToColor.Clear();
        foreach (var e in _colorSettings)
        {
            if (!string.IsNullOrEmpty(e._tagName))
            {
                var key = e._tagName.ToLower();
                _nameToColor[key] = e._color;
            }
        }
    }

    private void LateUpdate()
    {


        if (mapCamera == null || mapTexture == null || drawMat == null) return;

        

        var prevRT = RenderTexture.active;
        RenderTexture.active = mapTexture;

        //ここ重要：全クリア
        GL.Clear(true, true, Color.black);

        // MapCamera の行列を使って描画
        GL.PushMatrix();
        GL.LoadProjectionMatrix(mapCamera.projectionMatrix);
        GL.modelview = mapCamera.worldToCameraMatrix;

        Debug.Log("登録されてるMapオブジェクト" + registeredObjects.Count);

        foreach (var go in registeredObjects)
        {
            if (go == null) continue;

            var rend = go.GetComponent<MeshRenderer>();
            var mf = go.GetComponent<MeshFilter>();
            //レンダラーとフィルターがないなら
            if (rend == null || mf == null) continue;

            // タグ色取得
            Color baseColor;
            if (!_nameToColor.TryGetValue(go.tag.ToLower(), out baseColor))
                baseColor = Color.white;

        }


        //MapTextureをマゼンタに統一描画
        GL.PopMatrix();
        RenderTexture.active = prevRT;

        /*
        if (mapCamera == null || mapTexture == null || replacementShader == null) return;

        Debug.Log("カラー変更されるオブジェクトの数" + registeredObjects.Count);
        Debug.Log("色の数"+_nameToColor.Count);
        // 1) 各オブジェクトの色（名前ベース）＋ 高低差の明暗 を PropertyBlock で設定
        float playerY = _player != null ? _player.position.y : 0f;

        for (int i = 0; i < registeredObjects.Count; i++)
        {
            
            var go = registeredObjects[i];
            if (go == null) continue;

            // 名前で色決定（前方一致・部分一致どちらでもOKなように小文字でContainsチェック）
            var lowerName = go.name.ToLower();
            Color baseColor = _defaultColor;
            //デバッグ用
            string matchedKey = "(none)";

            foreach (var kv in _nameToColor)
            {
                //Debug.Log($"Compare: name={lowerName}, key={kv.Key}");

                var lowerTag = go.tag.ToLower().Trim();
                if (_nameToColor.TryGetValue(lowerTag, out var tagColor))
                {
                    baseColor = tagColor;
                    matchedKey = lowerTag;
                }
            }

            Debug.Log($"[MapColorManager] Object:{go.name}, Key:{matchedKey}, BaseColor:{baseColor}");

            // 高低差で明暗
            float heightDiff = go.transform.position.y - playerY;
            float brightness = Mathf.Clamp(1f + heightDiff * heightSensitivity, minBrightness, maxBrightness);
            Color finalColor = baseColor * brightness;

            // ここでグローバル変数として送る
            Shader.SetGlobalColor("_GlobalColor", finalColor);

            // その1体だけをMapCameraに描かせる
            var rends = go.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r == null) continue;
                mapCamera.cullingMask = 1 << r.gameObject.layer;
                mapCamera.RenderWithShader(replacementShader, null);
            }

            // 子も含めて全Rendererに _Color をセット（Unlit側で読む）
            /*var rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", finalColor);
                r.SetPropertyBlock(_mpb);
            }
        }

        // 2) 置換シェーダで強制描画（全ての描画をMapUnlitに）
        mapCamera.cullingMask = LayerMask.GetMask(mapLayerName);
        mapCamera.targetTexture = mapTexture;
        mapCamera.RenderWithShader(replacementShader, null);

        */

        // 3) PropertyBlockは残っていてもOK（Game側Toonは_CoIorを読まない）※念のため消したいなら下を有効化
        // ResetPropertyBlocks();
    }

    //タグを取得
    private bool TryGetBaseColor(GameObject go, out Color baseColor)
    {
        baseColor = Color.white;

        string key = go.tag.ToLower();

        if (_nameToColor.TryGetValue(key, out var c))
        {
            baseColor = c;
            return true;
        }
        return false;
    }

    //高さによる明暗補正
    private Color ApplyHeightBrightness(Color baseColor, Transform obj)
    {
        float heightDiff = obj.position.y - _player.position.y;
        float brightness = Mathf.Clamp01(1f + heightDiff * heightSensitivity);
        return baseColor * brightness;
    }


    private void ResetPropertyBlocks()
    {
        // ゲーム表示側に絶対影響させたくないなら呼ぶ
        for (int i = 0; i < registeredObjects.Count; i++)
        {
            var go = registeredObjects[i];
            if (go == null) continue;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
                r.SetPropertyBlock(null);
        }
    }

    // ───────── 登録制（バグ修正済み）─────────
    public static void Register(GameObject obj)
    {
        if (obj == null) return;
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
        {
            var target = r.gameObject;          // ← ここが重要：r.gameObject基準で重複判定
            if (!registeredObjects.Contains(target))
                registeredObjects.Add(target);
        }
    }

    public static void Unregister(GameObject obj)
    {
        if (obj == null) return;
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
        {
            var target = r.gameObject;
            registeredObjects.Remove(target);
        }
    }
}
