using System.Collections.Generic;
using System.Data.Common;
using Unity.VisualScripting;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using System;

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
    [SerializeField] private Transform[] _players;

    //配列はキャッシュして毎回メモリを確保しない
    private Vector4[] _playerPosCache;

    [Header("Surface Settings")]
      // 高低差の基準
    [SerializeField, Range(0.0001f, 2f)] private float heightSensitivity = 0.4f;
    [SerializeField] private float _SurfaceMinBrightness = 0.2f;
    [SerializeField] private float _SurfaceMaxBrightness = 1.0f;
    

    [Header("Point Settings")]            
    [SerializeField, Range(0.0001f, 2f)] private float _pointSensitivity = 0.4f;
    [SerializeField] private float _PointMinBrightness = 0.2f;
    [SerializeField] private float _PointMaxBrightness = 1.0f;
    [SerializeField] private float _pointRadius = 1.0f;

    private static readonly List<GameObject> registeredObjects = new();
    private readonly Dictionary<string, Color> _nameToColor = new();
    private MaterialPropertyBlock _mpb;

    //タグ名 → index に変換
    //その index を Renderer の materialPropertyBlock にセット
    private static Dictionary<string, int> _tagToId = new Dictionary<string, int>();
    private Color[] _colorArray = new Color[32];

    //マテリアルのキャッシュ
    private Material drawMat;
    //MapTextureに描くオブジェクトがあるなら一つもメッシュにする
    private Mesh _combinedMesh;

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

        // Shader 選択
        drawMat =
            _viewMode == ViewMode.Point ? new Material(PointReplacementShader) :
            _viewMode == ViewMode.Surface ? new Material(SurfaceReplacementShader) :
            new Material(HybridReplacementShader);


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

        //名前からIndex番号に変換
        int idx = 0;
        foreach (var e in _colorSettings)
        {
            if (!string.IsNullOrEmpty(e._tagName))
            {
                var key = e._tagName.ToLower();
                _nameToColor[key] = e._color;

                _tagToId[key] = idx;
                _colorArray[idx] = e._color;

                idx++;
            }
        }

        mapCamera.enabled = true;
    }

    private async UniTaskVoid Start()
    {
        //シェーダーに前職をセット
        drawMat.SetInt("_ColorCount", _colorSettings.Count);
        drawMat.SetColorArray("_Colors", _colorArray);

        CombineMeshes();
        //自動停止
        var token = this.GetCancellationTokenOnDestroy();

        while (!token.IsCancellationRequested)
        {
            RenderMap();
            await UniTask.Delay(TimeSpan.FromSeconds(0.05f), cancellationToken: token); // 20FPS
        }
    }

    //オブジェクトのメッシュを結合して一つにする
    private void CombineMeshes()
    {
        List<CombineInstance> combineList = new();

        foreach (var go in registeredObjects)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (!mf) continue;

            CombineInstance ci = new();
            ci.mesh = mf.sharedMesh;
            ci.transform = go.transform.localToWorldMatrix;

            combineList.Add(ci);
        }

        _combinedMesh = new Mesh();
        _combinedMesh.CombineMeshes(combineList.ToArray(), true, true);
    }

    private void RenderMap()
    {
        var prevRT = RenderTexture.active;
        RenderTexture.active = mapTexture;

        GL.Clear(true, true, new Color(0, 0, 0, 0));

        GL.invertCulling = true;

        // ========= 行列設定 =========
        GL.PushMatrix();

        Matrix4x4 proj = mapCamera.projectionMatrix;
        Matrix4x4 view = mapCamera.worldToCameraMatrix;

        GL.LoadProjectionMatrix(proj);
        GL.modelview = view;

        //シェーダー側の更新
        UpdateShaderParams();

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

            if (_players == null) return;

            drawMat.SetColor("_Color", baseColor);
            drawMat.SetPass(0);
            Graphics.DrawMeshNow(mf.sharedMesh, go.transform.localToWorldMatrix);
        }

        

        GL.PopMatrix();
        RenderTexture.active = prevRT;

        GL.invertCulling = false;

    }

    void UpdateShaderParams()
    {
        int count = _players.Length;

        if (_playerPosCache == null || _playerPosCache.Length != count)
            _playerPosCache = new Vector4[count];

        for (int i = 0; i < count; i++)
        {
            var p = _players[i].position;
            _playerPosCache[i] = new Vector4(p.x, p.y, p.z, 0);
        }

        drawMat.SetInt("_PlayerCount", count);
        drawMat.SetVectorArray("_PlayerPos", _playerPosCache);

        drawMat.SetFloat("_HeightSensitivity", heightSensitivity);
        drawMat.SetFloat("_SurfaceMinBrightness", _SurfaceMinBrightness);
        drawMat.SetFloat("_SurfaceMaxBrightness", _SurfaceMaxBrightness);

        drawMat.SetFloat("_DistSensitivity", _pointSensitivity);
        drawMat.SetFloat("_PointMinBrightness", _PointMinBrightness);
        drawMat.SetFloat("_PointMaxBrightness", _PointMaxBrightness);
        drawMat.SetFloat("_PointRadius", _pointRadius);
    }


    // ───────── 登録制（バグ修正済み）─────────
    public static void Register(GameObject obj)
    {
        if (obj == null) return;
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
        {
            var target = r.gameObject;
            if (!registeredObjects.Contains(target))
                registeredObjects.Add(target);

            var tag = target.tag.ToLower();

            if (_tagToId.TryGetValue(tag, out int id))
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetInt("_TagId", id);
                r.SetPropertyBlock(mpb);
            }
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
