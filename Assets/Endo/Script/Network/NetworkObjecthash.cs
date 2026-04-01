using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class NetworkObjecthash : MonoBehaviour
{

    NetworkManager GetNetworkManager()
    {
#if UNITY_6000_0_OR_NEWER
        // Unity 6 以降推奨API：非アクティブも含めて最初の1個を取得
        return Object.FindFirstObjectByType<NetworkManager>(
            FindObjectsInactive.Include
        );
#else
        return Object.FindObjectOfType<NetworkManager>();
#endif
    }


    [ContextMenu("Dump In-Scene NetworkObject hashes")]
    private void Dump()
    {
        var scene = SceneManager.GetActiveScene();

#if UNITY_6000_0_OR_NEWER
        var netObjs = FindObjectsByType<NetworkObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
#else
        var netObjs = FindObjectsOfType<NetworkObject>(true);
#endif

        Debug.Log(
            $"===== In-Scene NetworkObject Hash Dump for Scene: {scene.path} (NetworkObjects: {netObjs.Length}) ====="
        );

        foreach (var no in netObjs)
        {
            if (no == null) continue;
            if (no.gameObject.scene != scene) continue;   // 他シーンや DDoL は無視

            uint hash = 0;

#if UNITY_EDITOR
            // NetworkObject コンポーネントのシリアライズされたフィールドから読む
            var so = new SerializedObject(no);
            // YAML 上では "GlobalObjectIdHash: 123456789" という名前でシリアライズされている
            var prop = so.FindProperty("GlobalObjectIdHash");
            if (prop != null)
            {
                // GlobalObjectIdHash は uint なので intValue を uint にキャスト
                hash = unchecked((uint)prop.intValue);
            }
#endif

            string hashStr = hash == 0 ? "N/A" : hash.ToString();

            Debug.Log(
                $"[InSceneHash] Name={no.name}, Hash={hashStr}, Scene={no.gameObject.scene.path}",
                no.gameObject
            );
        }

        Debug.Log("===== End In-Scene Hash Dump =====");
    }
}
