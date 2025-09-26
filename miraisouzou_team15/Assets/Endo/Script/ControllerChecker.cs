using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ControllerChecker : MonoBehaviour
{
    [SerializeField] private TMP_Text _ui;
    [SerializeField] private GameObject _playerManager;
    [SerializeField] private GameObject _rope;
    private int _playerCount = 0;
    // Update is called once per frame
    void Update()
    {
        //コントローラの接続数が二つ以上
        if(Gamepad.all.Count < 2)
        {

            if (_ui != null)
            {
                //UIを出す
                if(_ui.gameObject.activeSelf == false)
                {
                    _ui.gameObject.SetActive(true);
                }
                _ui.text = "Please 2 Join Controller";
            }

            Debug.Log("コントローラーが接続されていません");

            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Debug.Log($"Gamepad {i + 1};{Gamepad.all[i].displayName}");
                
            }

        }//プレイヤーがゲーム内に二人いるかどうか
        else if(PlayerInput.all.Count < 2)
        {
            if (_ui != null)
            {
                //UIを出す
                if (_ui.gameObject.activeSelf == false)
                {
                    _ui.gameObject.SetActive(true);
                }
                Debug.Log("ボタンを押してはよ入れや");
                _ui.text = "Please Press Button";
            }
                    
        }
        else//プレイヤーが二人いる
        {
            if (_ui != null)
            {
                //UIを出さない
                if(_ui.gameObject.activeSelf == true)
                {
                    _ui.gameObject.SetActive(false);
                }
            }
            //プレイヤー同士をつなげる
            _playerManager.SetActive(true);
            //プレイヤーをつなげる紐を描画
            _rope.SetActive(true);
            
        }

        Debug.Log("現在の人数 : " + PlayerInput.all.Count);
    }
}
