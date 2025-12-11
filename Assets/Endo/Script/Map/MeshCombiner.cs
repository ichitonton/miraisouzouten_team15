using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MeshCombiner : MonoBehaviour
{
    [Header("結合したいオブジェクトをここに登録")]
    public GameObject[] sourceObjects;

    [Tooltip("生成されるオブジェクトに適用するマテリアル")]
    public Material targetMaterial;

    [Tooltip("結合後に元オブジェクトを非表示にする")]
    public bool deactivateOriginals = true;

    [Tooltip("サブメッシュを 1 つにまとめるか")]
    public bool mergeSubMeshes = true;

    [Tooltip("結合結果をこの GameObject に入れるか？")]
    public bool useThisObjectAsResult = true;

    [Header("新規オブジェクトとして作る場合の名前")]
    public string newObjectName = "Combined";

    [Tooltip("生成されたオブジェクトにMeshColliderをつけるか")]
    public bool addMeshCollider = false;


    private void Start()
    {
        Combine();
    }


    [ContextMenu("Combine Meshes Now")]
    public void Combine()
    {
        if (sourceObjects == null || sourceObjects.Length == 0 || targetMaterial == null)
        {
            Debug.LogError("設定が不完全です (オブジェクト配列またはマテリアルが空です)。");
            return;
        }

        var combineList = new List<CombineInstance>();

        // ★ このオブジェクトの「ワールド→ローカル」行列
        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

        int validMeshCount = 0;

        foreach (var obj in sourceObjects)
        {
            if (obj == null) continue;

            var mf = obj.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning($"{obj.name} に有効な MeshFilter / Mesh がありません。");
                continue;
            }

            if (!mf.sharedMesh.isReadable)
            {
                Debug.LogError($"[MeshCombiner] {obj.name} の Mesh は Read/Write 無効です。インポート設定で ON にして下さい。");
                continue;
            }

            var ci = new CombineInstance();
            ci.mesh = mf.sharedMesh;

            // ★ 元オブジェクトのローカル → ワールド → このオブジェクトのローカル
            //    つまり「このオブジェクトのローカル空間」で結合
            ci.transform = worldToLocal * mf.transform.localToWorldMatrix;

            combineList.Add(ci);
            validMeshCount++;

            if (deactivateOriginals)
            {
                obj.SetActive(false);
            }
        }

        Debug.Log($"CombineInstance に追加されたメッシュ数: {validMeshCount}");

        if (validMeshCount == 0)
        {
            Debug.LogError("有効なメッシュがありません。処理中断。");
            return;
        }

        // --- 実際に結合 ---
        var combinedMesh = new Mesh();
        combinedMesh.name = newObjectName + "_Mesh";
        combinedMesh.CombineMeshes(combineList.ToArray(), mergeSubMeshes, true);

        Debug.Log($"結合後の頂点数: {combinedMesh.vertexCount}");

        // --- ★ 完全に新しい GameObject を作成 ---
        var newGo = new GameObject(newObjectName);

        // 親はこのコンポーネントの親と同じにする（完全に別オブジェクト）
        newGo.transform.SetParent(transform.parent, worldPositionStays: false);

        // ワールド位置・回転・スケールは、このオブジェクトと同じ
        newGo.transform.position = transform.position;
        newGo.transform.rotation = transform.rotation;
        newGo.transform.localScale = transform.localScale;

        var mfNew = newGo.AddComponent<MeshFilter>();
        mfNew.sharedMesh = combinedMesh;

        var mrNew = newGo.AddComponent<MeshRenderer>();
        mrNew.sharedMaterial = targetMaterial;

        if (addMeshCollider)
        {
            var mc = newGo.AddComponent<MeshCollider>();
            mc.sharedMesh = combinedMesh;
        }

        Debug.Log($"生成完了: {newGo.name} を作成しました。");
    }
}
