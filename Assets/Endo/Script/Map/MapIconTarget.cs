using UnityEngine;

public class MapIconTarget : MonoBehaviour
{
    [Header("アイコン検索用の名前。空なら gameObject.name を使う")]
    [SerializeField] private string overrideName;

    [Header("一時的にアイコンを無効にしたい場合")]
    public bool useIcon = true;

    public string GetSearchName()
    {
        if (!string.IsNullOrWhiteSpace(overrideName))
            return overrideName;

        return gameObject.name;
    }

    private void OnEnable()
    {
        MapIconManager.Register(this);
    }

    private void OnDisable()
    {
        MapIconManager.Unregister(this);
    }
}
