using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement; // LoadSceneMode 用

public class NetworkSceneManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    

    public static NetworkSceneManager Instance;

    [Header("SceneIdからシーン名の対応")]
    [SerializeField] private SceneDatabase sceneDatabase;

    public enum SceneId
    {
        None,
        MainGame,
        Result,
        Title
    }

    private void Awake()
    {
        //シングルトンのインスタンス生成
        if (Instance == null)
        {
            Instance = this;
            //シーンの切り替えで消えない
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// コードから使う用：SceneId を渡してシーン遷移
    /// </summary>
    public void LoadSceneRequest(SceneId sceneId)
    {

        if (sceneDatabase == null)
        {
            Debug.LogError("SceneDatabase が設定されていません");
            return;
        }

        if (!sceneDatabase.TryGetSceneName(sceneId, out string sceneName))
        {
            Debug.LogError($"SceneId {sceneId} に対応するシーン名が SceneDatabase にありません");
            return;
        }

        // Netcode を使っている場合は、サーバー(Host)だけがシーン遷移を命令する
        var nm = NetworkManager.Singleton;

        if (nm != null && nm.IsServer)
        {
            //ネットワークがつながってるときはクライアントも一緒にシーンが変わる
            nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else if (nm == null)
        {
            // オフライン / 非ネットワーク時は通常のシーンロード
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else
        {
            // クライアント側から直接呼ばれた場合
            Debug.LogWarning("Clientごときがシーン変えようとしてんじゃねーよ");
            // 必要ならここから ServerRpc を飛ばしてサーバー側で ChangeScene を呼ぶ形にしても良い
        }
    }

    /// <summary>
    /// コードから使う用：SceneId を渡してシーン遷移
    /// </summary>
    public void LoadSceneRequest(string sceneName)
    {

        if (sceneDatabase == null)
        {
            Debug.LogError("SceneDatabase が設定されていません");
            return;
        }

       
        // Netcode を使っている場合は、サーバー(Host)だけがシーン遷移を命令する
        var nm = NetworkManager.Singleton;

        if (nm != null && nm.IsServer)
        {
            //ネットワークがつながってるときはクライアントも一緒にシーンが変わる
            nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else if (nm == null)
        {
            // オフライン / 非ネットワーク時は通常のシーンロード
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else
        {
            // クライアント側から直接呼ばれた場合
            Debug.LogWarning("Clientごときがシーン変えようとしてんじゃねーよ");
            // 必要ならここから ServerRpc を飛ばしてサーバー側で ChangeScene を呼ぶ形にしても良い
        }
    }

    // NetworkSceneManager.cs に追加
    public bool TryResolveSceneName(SceneId sceneId, out string sceneName)
    {
        sceneName = null;
        if (sceneDatabase == null) return false;
        return sceneDatabase.TryGetSceneName(sceneId, out sceneName);
    }

}
