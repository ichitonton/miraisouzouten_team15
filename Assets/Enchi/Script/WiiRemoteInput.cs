using System.Collections.Generic;
using UnityEngine;
using WiimoteApi;

public class WiiRemoteInput : MonoBehaviour
{
    private List<Wiimote> wiimote;

    [SerializeField] int _stickValueMax = 90;
    [SerializeField] int _stickValueMin = 10;

    private bool oneTimeRemoteSetting = false;
    void Start()
    {
        bool found = WiimoteManager.FindWiimotes();
        if (WiimoteManager.Wiimotes.Count > 0)
        {
            Debug.Log("remoteCount : " + WiimoteManager.Wiimotes.Count);
            wiimote = WiimoteManager.Wiimotes;
            // 加速度対応レポートモードに設定（必要に応じて）
            //wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL);
            foreach (var wm in wiimote)
            {
                Debug.Log("Wiimote found: " + found);
                //wm.SetupIRCamera(IRDataType.EXTENDED);
                //wm.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL);
            }
        }
        else
        {
            Debug.LogError("Wiimote not found!");
        }
    }
    void Update()
    {
        if (!WiimoteManager.HasWiimote()) return;
        wiimote = WiimoteManager.Wiimotes;
        for (int i = 0; i < wiimote.Count; i++)
        {
            var wm = wiimote[i];
            if (wm == null) continue;

            int ret;
            do { ret = wm.ReadWiimoteData(); } while (ret > 0);

             
            if (wm.Nunchuck != null)
            {
                //// 拡張がNunchuckならデータレポートモードを拡張対応に切り替え
                if (wm.current_ext == ExtensionController.NUNCHUCK && !oneTimeRemoteSetting)
                {
                    wm.SetupIRCamera(IRDataType.BASIC);

                    wm.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6); //←加速度〇
                    //wm.SendDataReportMode(InputDataType.REPORT_BUTTONS_IR10_EXT9); //←加速度×

                    //wm.SetupIRCamera();
                    oneTimeRemoteSetting = true;
                    if (i == 0)
                    {
                        wm.SendPlayerLED(true, false, false, false);
                        oneTimeRemoteSetting = false;
                    }
                    if (i == 1)
                    {
                        wm.SendPlayerLED(true, true, false, false);
                    }

                    //NunchuckData nunchuck = wm.Nunchuck;
                    //Debug.Log($"Stick: {nunchuck.stick}, C:{nunchuck.c}, Z:{nunchuck.z}");
                }
            }

        }
        //int ret;
        //do
        //{
        //    ret = wiimote.ReadWiimoteData();
        //} while (ret > 0);

        //if (wiimote[0].Button.a)
        //    Debug.Log("A button pressed!");
        //if (wiimote.Button.b)
        //    Debug.Log("B button pressed!");
        //if (wiimote.Button.one)
        //    Debug.Log("1 button pressed!");
        //if (wiimote.Button.two)
        //    Debug.Log("2 button pressed!");
        //if (wiimote.Button.minus)
        //    Debug.Log("- button pressed!");
        //if (wiimote.Button.plus)
        //    Debug.Log("+ button pressed!");
        //if (wiimote.Button.home)
        //    Debug.Log("home button pressed!");
        //if (wiimote.Button.d_up)
        //    Debug.Log("↑ button pressed!");
        //if (wiimote.Button.d_down)
        //    Debug.Log("↓ button pressed!");
        //if (wiimote.Button.d_right)
        //    Debug.Log("→ button pressed!");
        //if (wiimote.Button.d_left)
        //    Debug.Log("← button pressed!");

        //// センサーデータ読み出し
        //do { ret = wiimote.ReadWiimoteData(); } while (ret > 0);

        //// 加速度値を取得（0～255）
        //float ax = wiimote.Accel.GetCalibratedAccelData()[0];
        //float ay = wiimote.Accel.GetCalibratedAccelData()[1];
        //float az = wiimote.Accel.GetCalibratedAccelData()[2];

        //Debug.Log($"Accel: X={ax:F2}, Y={ay:F2}, Z={az:F2}");

        //Debug.Log(wiimote);
        //if (wiimote.Nunchuck == null)
        //{
        //    Debug.Log("ヌンチャク接続中");
        //}



        //if (wiimote.Nunchuck != null)
        //{

        //    Debug.Log("ヌンチャク接続完了");
        //    Debug.Log(wiimote.Nunchuck.stick[1]);
        //}

        if (GetButtonZ(0))
        {
            Debug.Log("ZZZZZZZ");
        }
    }

    public bool GetButtonA(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.a;
    }

    public bool GetButtonB(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.b;
    }

    public bool GetButtonOne(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.one;
    }
    public bool GetButtonTwo(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.two;
    }
    public bool GetButtonPlus(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.plus;
    }
    public bool GetButtonMinus(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.minus;
    }
    public bool GetButtonHome(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.home;
    }
    public bool GetButtonUp(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.d_up;
    }
    public bool GetButtonDown(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.d_down;
    }
    public bool GetButtonRight(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.d_right;
    }

    public bool GetButtonLeft(int wiiRemoteNum)
    {
        return wiimote[wiiRemoteNum].Button.d_left;
    }

    public Vector3 GetAccel(int wiiRemoteNum)
    {
        Vector3 vector3 = new Vector3();
        vector3.x = wiimote[wiiRemoteNum].Accel.GetCalibratedAccelData()[0];
        vector3.y = wiimote[wiiRemoteNum].Accel.GetCalibratedAccelData()[1];
        vector3.z = wiimote[wiiRemoteNum].Accel.GetCalibratedAccelData()[2];
        return vector3;
    }

    public Vector2 GetIR(int wiiRemoteNum)
    {
        Vector2 vector2 = new Vector2();
        vector2.x = wiimote[wiiRemoteNum].Ir.GetPointingPosition()[0] * Screen.width;
        vector2.y = wiimote[wiiRemoteNum].Ir.GetPointingPosition()[1] * Screen.height;
        return vector2;
    }

    public bool GetButtonC(int wiiRemoteNum)
    {
        bool C = false;
        if (wiimote[wiiRemoteNum].Nunchuck != null)
        {
            if (wiimote[wiiRemoteNum].Nunchuck.c)
            {
                C = true;
            }
        }
        if (wiimote[wiiRemoteNum].Nunchuck == null)
        {
            C = false;
        }
        return C;
    }

    public bool GetButtonZ(int wiiRemoteNum)
    {
        bool Z = false;
        if (wiimote[wiiRemoteNum].Nunchuck != null)
        {
            if (wiimote[wiiRemoteNum].Nunchuck.z)
            {
                Z = true;
            }
        }
        if (wiimote[wiiRemoteNum].Nunchuck == null)
        {
            Z = false;
        }
        return Z;
    }

    public Vector2Int GetStick(int wiiRemoteNum)
    {
        Vector2Int vector2 = new Vector2Int(0, 0);
        Vector2Int vectorWii = new Vector2Int(0, 0);
        if (wiimote[wiiRemoteNum].Nunchuck != null)
        {
            vectorWii.x = wiimote[wiiRemoteNum].Nunchuck.stick[0] - 128;
            vectorWii.y = wiimote[wiiRemoteNum].Nunchuck.stick[1] - 128;
            if (vectorWii.x > 0)
            {
                if (vectorWii.x >= _stickValueMax)
                {
                    vector2.x = _stickValueMax;
                }
                else if (vectorWii.x >= _stickValueMin)
                {
                    vector2.x = vectorWii.x;
                }
                else
                {
                    vector2.x = 0;
                }
            }
            else if (vectorWii.x <= 0)
            {
                if (vectorWii.x <= -_stickValueMax)
                {
                    vector2.x = -_stickValueMax;
                }
                else if (vectorWii.x <= -_stickValueMin)
                {
                    vector2.x = vectorWii.x;
                }
                else
                {
                    vector2.x = 0;
                }
            }

            if (vectorWii.y > 0)
            {
                if (vectorWii.y >= _stickValueMax)
                {
                    vector2.y = _stickValueMax;
                }
                else if (vectorWii.y >= _stickValueMin)
                {
                    vector2.y = vectorWii.y;
                }
                else
                {
                    vector2.y = 0;
                }
            }
            else if (vectorWii.y <= 0)
            {
                if (vectorWii.y <= -_stickValueMax)
                {
                    vector2.y = -_stickValueMax;
                }
                else if (vectorWii.y <= -_stickValueMin)
                {
                    vector2.y = vectorWii.y;
                }
                else
                {
                    vector2.y = 0;
                }
            }

            return vector2;
        }
        return vector2;
    }

    void OnApplicationQuit()
    {
        if (wiimote != null)
        {
            for (int i = wiimote.Count - 1; i >= 0; i--)
            {
                var wm = wiimote[i];
                if (wm != null)
                    WiimoteManager.Cleanup(wm);
            }
        }

    }

}
