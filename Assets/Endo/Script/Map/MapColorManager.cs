using System.Collections.Generic;
using System.Data.Common;
using Unity.VisualScripting;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using System;
using UnityEngine.InputSystem;
using Unity.Netcode;

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
    [SerializeField] private Transform[] _playerManual;

    // 自動検出用のリスト
    private readonly List<Transform> _playerList = new();

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

    [Header("高さでの色変化")]
    [SerializeField] private float _globalHeightRef = 10f;
    [SerializeField] private float _globalHeightSensitivity = 0.01f;
    [SerializeField] private float _globalMinScale = 0.9f;
    [SerializeField] private float _globalMaxScale = 1.1f;

    [SerializeField, Range(0.0001f, 1f)] private float _heightRange = 0.3f;

    [Header("Alpha")]
    [SerializeField,Range(0.1f,1)] private float _alpha = 1f;

    [Header("Delay")]
    [SerializeField,Range(0.01f,1f)] private float __updateInterval = 0.05f;

    //MapObjectがついているオブジェクトリスト
    private static readonly List<GameObject> registeredObjects = new();
    // 追加：結合後メッシュ（静的マップ専用）
    private readonly List<GameObject> _combinedStaticObjects = new();
    // 追加：動的オブジェクトだけを保持（＝Combineしないやつ）
    private readonly List<GameObject> _dynamicObjects = new();

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

        //メッシュの結合
        CombineMeshes();
        //自動停止
        var token = this.GetCancellationTokenOnDestroy();

        while (!token.IsCancellationRequested)
        {

            //まず死んだプレイヤーを掃除
            CleanupPlayers();

            if (_playerList.Count == 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(__updateInterval), cancellationToken: token);
                continue;
            }

            RenderMap();
            await UniTask.Delay(TimeSpan.FromSeconds(__updateInterval), cancellationToken: token);
        }
    }

    // オブジェクトのメッシュを結合して一つにする
    private void CombineMeshes()
    {
        _combinedStaticObjects.Clear();
        _dynamicObjects.Clear();

        // タグごとに MeshFilter を集める（キーは小文字タグ）
        var tagToMeshFilters = new Dictionary<string, Dictionary<string, List<MeshFilter>>>();

        // この MapColorManager のワールド→ローカル行列
        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

        foreach (var go in registeredObjects)
        {
            if (go == null) continue;

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var mesh = mf.sharedMesh;

            // ★ Read/Write チェック（無効なら結合には使えない）
            if (!mesh.isReadable)
            {
                Debug.LogError($"[MapColorManager] '{go.name}' の Mesh は Read/Write 無効です。インポート設定で ON にしてください。");
                continue;
            }

            var mapObj = go.GetComponent<MapObject>();

            // ★ 動く系 or Combineしちゃダメなやつは dynamic 側へ
            if (mapObj != null && !mapObj.CombineToStatic)
            {
                _dynamicObjects.Add(go);
                continue;
            }

            // 色判定用キー（小文字タグ）
            string originalKey = GetTagOrParentTag(go);    // 例: "Field"
            string key = originalKey.ToLower();  // 例: "field"

            if (!tagToMeshFilters.TryGetValue(originalKey, out var dic))
            {
                dic = new Dictionary<string, List<MeshFilter>>();
                var list = new List<MeshFilter>(); 
                dic[key]= list;
                tagToMeshFilters[originalKey] = dic;
            }
            dic[key].Add(mf);

        }

        // ==== タグごとに 1 メッシュへ結合 ====
        int mapLayer = LayerMask.NameToLayer(mapLayerName); // 例: "Map"

        foreach (var kv in tagToMeshFilters)
        {
            var dic = kv.Value;

            string key = "untagget";
            List<MeshFilter> list = null;
            foreach (var d in dic)
            {
                key = d.Key;
                list = d.Value; 
            }
            
            Debug.Log("メッシュの数" + list.Count);
            if (list.Count == 0) continue;

            // 1) CombineInstance 配列を作成
            var combines = new CombineInstance[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var mf = list[i];
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                combines[i] = new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = 0,
                    // 各メッシュのローカル→ワールド を MapColorManager のローカル空間に変換
                    transform = worldToLocal * mf.transform.localToWorldMatrix
                };
            }

            // 2) 結合メッシュを作る
            var combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combines, true, true);

            if (combinedMesh.vertexCount == 0)
            {
                Debug.LogError($"[MapColorManager] CombineMeshes の結果、'{kv.Key}' グループの頂点数が 0 でした。スキップします。");
                continue;
            }

            // 3) 結合結果用 GameObject を作成
            var combinedGO = new GameObject($"MapCombined_{kv.Key}");
            combinedGO.transform.SetParent(this.transform, worldPositionStays: false);

            // Layer を Map に（見つからなければそのまま）
            if (mapLayer != -1) combinedGO.layer = mapLayer;

            combinedGO.tag = kv.Key;

            var mfCombined = combinedGO.AddComponent<MeshFilter>();
            mfCombined.sharedMesh = combinedMesh;

            var mrCombined = combinedGO.AddComponent<MeshRenderer>();
            mrCombined.sharedMaterial = drawMat; // Map 用シェーダーマテリアル

            // 4) タグIDを MaterialPropertyBlock に設定（シェーダー側で色分け用）
            if (_tagToId.TryGetValue(key, out int id))
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetInt("_TagId", id);
                mrCombined.SetPropertyBlock(mpb);
            }

            _combinedStaticObjects.Add(combinedGO);
            Debug.Log($"[CombineMeshes] '{kv.Key}' -> verts={combinedMesh.vertexCount}");

            //MapObjectをつけておく
        }

        // 動的オブジェクトだけ registeredObjects に残す
        registeredObjects.Clear();
        registeredObjects.AddRange(_dynamicObjects);

        Debug.Log($"[MapColorManager] Combined Static: {_combinedStaticObjects.Count}, Dynamic: {_dynamicObjects.Count}");
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
        Debug.Log("登録されてる動的オブジェクトの数" + registeredObjects.Count);
        Debug.Log("登録されてる静的オブジェクトの数" + _combinedStaticObjects.Count);
        // ==========================
        // 1) 結合済み静的メッシュの描画
        // ==========================
        foreach (var go in _combinedStaticObjects)
        {
            if (go == null) continue;

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null) continue;

            var mesh = mf.sharedMesh;
            if (mesh == null) continue;   // ここ追加

            
            // ---- タグから TagId を決める ----
            string unityTag = GetTagOrParentTag(go); // "Field", "Iwa" など
            string key = unityTag.ToLower();

            int tagId = 0;
            if (!_tagToId.TryGetValue(key, out tagId))
            {
                // 未登録タグなら 0 番にフォールバック
                tagId = 0;
            }

            drawMat.SetInt("_TagId", tagId);
            drawMat.SetPass(0);

            Debug.Log("結合オブジェクト描画");

            Graphics.DrawMeshNow(mf.sharedMesh, go.transform.localToWorldMatrix);
        }

        // ==========================
        // 2) 動的オブジェクトの描画
        // ==========================
        foreach (var go in registeredObjects)
        {
            if (go == null) continue;

            var mf = go.GetComponent<MeshFilter>();
            if (!mf) continue;

            var mesh = mf.sharedMesh;
            if (mesh == null) continue;   // ★ ここ追加

            // ---- タグ（子が Untagged なら親） ----
            string unityTag = GetTagOrParentTag(go);
            string key = unityTag.ToLower();

            int tagId = 0;
            if (!_tagToId.TryGetValue(key, out tagId))
            {
                tagId = 0;
            }

            drawMat.SetInt("_TagId", tagId);
            drawMat.SetPass(0);

            Graphics.DrawMeshNow(mf.sharedMesh, go.transform.localToWorldMatrix);
        }

        GL.PopMatrix();
        RenderTexture.active = prevRT;
        GL.invertCulling = false;

    }

    void UpdateShaderParams()
    {

        // 1) プレイヤーの掃除（DestroyされたTransform除去）
        //CleanupPlayers();
        int count = _playerList.Count;

        
        Debug.Log("プレイヤーの数" + count);
        if (count == 0)
            return; // いないなら終了

        if (_playerPosCache == null || _playerPosCache.Length != count)
            _playerPosCache = new Vector4[32];

        for (int i = 0; i < count; i++)
        {
            Transform t = _playerList[i];
            if (t == null)
            {
                _playerPosCache[i] = Vector4.zero;
                continue;
            }

            var p = t.position;
            _playerPosCache[i] = new Vector4(p.x, p.y, p.z, 0);
        }

        //Debug.Log($"[ShaderSend] PlayerCount={count}");
        for (int i = 0; i < count; i++)
        {
            //Debug.Log($"[ShaderSend] P{i}={_playerPosCache[i]}");
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

        // 追加：高さ係数用
        drawMat.SetFloat("_GlobalHeightRef", _globalHeightRef);
        drawMat.SetFloat("_GlobalHeightSensitivity", _globalHeightSensitivity);
        drawMat.SetFloat("_GlobalMinScale", _globalMinScale);
        drawMat.SetFloat("_GlobalMaxScale", _globalMaxScale);

        drawMat.SetFloat("_HeightAffectRange", _heightRange);

        drawMat.SetFloat("_Alpha", _alpha);

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


    /// <summary>
    /// Destroy された Player を _players 配列から取り除く
    /// </summary>
    private void CleanupPlayers()
    {
        _playerList.Clear();

        //ネットワークマネージャーなかったら処理しない
        if (NetworkManager.Singleton != null)
        {
            if (GameManager.Instance._IsLanModeActive == true)
            {

                int i = 0;

                foreach (var playerRef in GameManager.Instance._networkObjectList)
                {
                    //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
                    //成功した場合 playerObj に GameObject が入る。

                    if (playerRef.TryGet(out var playerObj))
                    {
                        //プレイヤーのタグを持っているかつ所有権があるなら
                        if (playerObj.gameObject.CompareTag("Player") && playerObj.IsOwner)
                        {
                            //Debug.Log("所有権を持ったプレイヤーです");
                            _playerList.Add(playerObj.gameObject.transform);
                            i++;
                        }
                    }

                }
                Debug.Log("オンラインプレイヤーの数" + i);
            }
            else
            {
                // 1) 手動登録があれば優先
                if (_playerManual != null && _playerManual.Length > 0)
                {
                    foreach (var t in _playerManual)
                    {
                        if (t != null) _playerList.Add(t);
                    }
                }
                else
                {
                    // 2) なければタグで自動検出
                    var found = GameObject.FindGameObjectsWithTag("Player");
                    foreach (var go in found)
                    {
                        if (go != null)
                            _playerList.Add(go.transform);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Untagged の場合は親のタグをたどって Tag を取得する
    /// </summary>
    private string GetTagOrParentTag(GameObject go)
    {
        Transform t = go.transform;

        while (t != null)
        {
            string tag = t.gameObject.tag;

            if (!string.Equals(tag, "Untagged", StringComparison.OrdinalIgnoreCase))
                return tag; // 見つかったタグ（小文字）

            t = t.parent;
        }

        return "untagged"; // 最後までなければ untagged
    }

}