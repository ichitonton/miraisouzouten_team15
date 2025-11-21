using System.Collections.Generic;
using System.Data.Common;
using Unity.VisualScripting;
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
    [SerializeField] private Shader PointReplacementShader; // MapUnlit（URP Unlit）を指定
    [SerializeField] private Shader SurfaceReplacementShader; // MapUnlit（URP Unlit）を指定
    [SerializeField] private Shader HybridReplacementShader; // MapUnlit（URP Unlit）を指定

    [Header("Map Color Settings")]
    [SerializeField] public List<MapColor> _colorSettings = new List<MapColor>();
    [SerializeField] private Color _defaultColor = Color.gray; // ヒットしない時の色

    [Header("Target Layer")]
    [SerializeField] private string mapLayerName = "Map";

    [Header("Player Settings")]
    [SerializeField] private Transform _player;

    [Header("Surface Settings")]
      // 高低差の基準
    [SerializeField, Range(0.0001f, 2f)] private float heightSensitivity = 0.4f;
    [SerializeField] private float _PointMinBrightness = 0.2f;
    [SerializeField] private float _PointMaxBrightness = 1.0f;

    [Header("Point Settings")]            
    [SerializeField, Range(0.0001f, 2f)] private float _pointensitivity = 0.4f;
    [SerializeField] private float _SurfaceMinBrightness = 0.2f;
    [SerializeField] private float _SurfaceMaxBrightness = 1.0f;
    [SerializeField] private float _pointRadius = 1.0f;

    private static readonly List<GameObject> registeredObjects = new();
    private readonly Dictionary<string, Color> _nameToColor = new();
    private MaterialPropertyBlock _mpb;

    private Material drawMat;

    public enum ViewMode
    {
        Point,
        Surface,
        Hybrid
    }

    [SerializeField] private ViewMode _viewMode = ViewMode.Point;

    private void Awake()
    {

        _mpb = new();

        if (_viewMode == ViewMode.Point)
        {
            drawMat = new Material(PointReplacementShader); // ← ここで1回だけ生成！
        }
        else if (_viewMode == ViewMode.Surface)
        {
            drawMat = new Material(SurfaceReplacementShader); // ← ここで1回だけ生成！
        }
        else if (_viewMode == ViewMode.Hybrid)
        {
            drawMat = new Material(HybridReplacementShader); // ← ここで1回だけ生成！
        }
        

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

        mapCamera.enabled = true;
    }

    private void LateUpdate()
    {

        var prevRT = RenderTexture.active;
        RenderTexture.active = mapTexture;

        GL.Clear(true, true, new Color(0,0,0,0));

        GL.invertCulling = true;

        // ========= 行列設定 =========
        GL.PushMatrix();

        Matrix4x4 proj = mapCamera.projectionMatrix;
        Matrix4x4 view = mapCamera.worldToCameraMatrix;

        GL.LoadProjectionMatrix(proj);
        GL.modelview = view;

        if (_viewMode == ViewMode.Point)
        {
            drawMat.SetVector("_PlayerPosition", new Vector4(_player.position.x, _player.position.y, _player.position.z, 0));
            drawMat.SetFloat("_DistSensitivity", heightSensitivity);
            drawMat.SetFloat("_MinBrightness", _PointMinBrightness);
            drawMat.SetFloat("_MaxBrightness", _PointMaxBrightness);
        }
        else if (_viewMode == ViewMode.Surface)
        {
            drawMat.SetFloat("_PlayerHeight", _player.position.y);
            drawMat.SetFloat("_HeightSensitivity", heightSensitivity);
            drawMat.SetFloat("_MinBrightness", _SurfaceMinBrightness);
            drawMat.SetFloat("_MaxBrightness", _SurfaceMaxBrightness);
        }
        else if(_viewMode == ViewMode.Hybrid)
        {

            drawMat.SetFloat("_PlayerHeight", _player.position.y);
            drawMat.SetFloat("_HeightSensitivity", heightSensitivity);
            drawMat.SetFloat("_SurfaceMinBrightness", _SurfaceMinBrightness);
            drawMat.SetFloat("_SurfaceMaxBrightness", _SurfaceMaxBrightness);

            drawMat.SetVector("_PlayerPos", new Vector4(_player.position.x, _player.position.y, _player.position.z, 0));
            drawMat.SetFloat("_DistSensitivity", heightSensitivity);
            drawMat.SetFloat("_PointMinBrightness", _PointMinBrightness);
            drawMat.SetFloat("_PointMaxBrightness", _PointMaxBrightness);
            drawMat.SetFloat("_PointRadius", _pointRadius); // 1m 推奨

        }

        // ========= Mesh描画 =========
        foreach (var go in registeredObjects)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (!mf) continue;

            // ベースカラーをタグから取得
            Color baseColor = Color.red; // デフォルト赤
            string tag = go.tag.ToLower();

            // 子のタグが "Untagged" の場合は親のタグを使う
            if (tag == "untagged")
            {
                if (go.transform.parent != null)
                {
                    tag = go.transform.parent.tag.ToLower();
                }
            }

            if (_nameToColor.TryGetValue(tag, out var colorFromTag))
                baseColor = colorFromTag;

            if (_player == null) return; 
            
            drawMat.SetColor("_Color", baseColor);
            drawMat.SetPass(0);

            Graphics.DrawMeshNow(mf.sharedMesh, go.transform.localToWorldMatrix);
        }

        GL.PopMatrix();
        RenderTexture.active = prevRT;

        GL.invertCulling = false;

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
