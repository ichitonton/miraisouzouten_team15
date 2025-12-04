using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PlayerItemUI : MonoBehaviour
{
    [Header("このUIはどっちのプレイヤー用？")]
    // Inspectorで P1 か P2 を指定してね
    [SerializeField] private MovePlayerKey.PlayerNumber targetNumber;

    [Header("画像設定")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite[] itemSprites;

    private MovePlayerKey _targetPlayer;
    private MovePlayerKey.ItemType _lastItemType = MovePlayerKey.ItemType.Max;

    void Update()
    {
        // 1. まだターゲットが見つかってないなら、全力で探す
        if (_targetPlayer == null)
        {
            FindMyLocalPlayer();
            return;
        }

        // 2. 見つかったらアイテムを監視する
        CheckPlayerItem();
    }

    private void FindMyLocalPlayer()
    {
        // ネットにつながってないなら探さない
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        // シーンにいる「MovePlayerKey」を持ってる人を全員集める
        var allPlayers = FindObjectsByType<MovePlayerKey>(FindObjectsSortMode.None);

        foreach (var p in allPlayers)
        {
            // ネットワークオブジェクトを持っていて...
            if (p.TryGetComponent<NetworkObject>(out var netObj))
            {
                // 「俺の権限(IsOwner)」があり、かつ「指定した番号(P1/P2)」と一致するか？
                if (netObj.IsOwner && p.GetPlayerNumber() == targetNumber)
                {
                    _targetPlayer = p;
                    // Debug.Log($"発見！ 俺の {targetNumber} はこいつだ！: {p.name}");

                    // 見つかった瞬間、初期表示を更新
                    UpdateIconDisplay(_targetPlayer.GetHaveItem());
                    break;
                }
            }
        }
    }

    private void CheckPlayerItem()
    {
        // アイテムが変わった時だけ更新
        var currentItem = _targetPlayer.GetHaveItem();

        if (currentItem != _lastItemType)
        {
            UpdateIconDisplay(currentItem);
            _lastItemType = currentItem;
        }
    }

    private void UpdateIconDisplay(MovePlayerKey.ItemType itemType)
    {
        int index = (int)itemType;

        // 条件チェック：
        // 1. 配列の範囲内か？ (エラー防止)
        // 2. その場所に画像がセットされているか？ (Noneじゃないか)
        if (index >= 0 && index < itemSprites.Length && itemSprites[index] != null)
        {
            // 画像がある時だけ表示ON
            iconImage.sprite = itemSprites[index];
            iconImage.enabled = true;
        }
        else
        {
            // 画像がない(None)なら表示OFF
            iconImage.enabled = false;
            iconImage.sprite = null;
        }
    }
}