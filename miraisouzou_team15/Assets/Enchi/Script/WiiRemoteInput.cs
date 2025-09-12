using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.tvOS;
using WiimoteApi;

public class WiiRemoteInput : MonoBehaviour
{
    private Wiimote wiimote;

    void Start()
    {
        bool found = WiimoteManager.FindWiimotes();
        Debug.Log("Wiimote found: " + found); 
        if (WiimoteManager.Wiimotes.Count > 0)
        {
            wiimote = WiimoteManager.Wiimotes[0];
            // 加速度対応レポートモードに設定（必要に応じて）
            wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL);
            wiimote.SetupIRCamera();
            wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL_EXT16);
        }
        else
        {
            Debug.LogError("Wiimote not found!");
        }
    }
    void Update()
    {
        if (!WiimoteManager.HasWiimote()) return;

        wiimote = WiimoteManager.Wiimotes[0];

        int ret;
        do
        {
            ret = wiimote.ReadWiimoteData();
        } while (ret > 0);

        //if (wiimote.Button.a)
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
        if (wiimote.Nunchuck == null)
        {
            Debug.Log("ヌンチャク接続中");
        }



        if (wiimote.Nunchuck != null)
        {

            Debug.Log("ヌンチャク接続完了");
            Debug.Log(wiimote.Nunchuck.stick[1]);
        }
    }

    public bool GetButtonA()
    {
        return wiimote.Button.a;
    }

    public bool GetButtonB()
    {
        return wiimote.Button.b;
    }

    public bool GetButtonOne()
    {
        return wiimote.Button.one;
    }
    public bool GetButtonTwo()
    {
        return wiimote.Button.two;
    }
    public bool GetButtonPlus()
    {
        return wiimote.Button.plus;
    }
    public bool GetButtonMinus()
    {
        return wiimote.Button.minus;
    }
    public bool GetButtonHome()
    {
        return wiimote.Button.home;
    }
    public bool GetButtonUp()
    {
        return wiimote.Button.d_up;
    }
    public bool GetButtonDown()
    {
        return wiimote.Button.d_down;
    }
    public bool GetButtonRight()
    {
        return wiimote.Button.d_right;
    }

    public bool GetButtonLeft()
    {
        return wiimote.Button.d_left;
    }

    public Vector3 GetAccel()
    {
        Vector3 vector3 = new Vector3();
        vector3.x = wiimote.Accel.GetCalibratedAccelData()[0];
        vector3.y = wiimote.Accel.GetCalibratedAccelData()[1];
        vector3.z = wiimote.Accel.GetCalibratedAccelData()[2];
        return vector3;
    }

    public Vector2 GetIR()
    {
        Vector2 vector2 = new Vector2();
        vector2.x = wiimote.Ir.GetPointingPosition()[0];
        vector2.y = wiimote.Ir.GetPointingPosition()[1];
        return vector2;
    }

    public bool GetButtonC()
    {
        bool C = false;
        if (wiimote.Nunchuck != null)
        {
            if (wiimote.Nunchuck.c)
            {
                C = true;
            }
        }
        if (wiimote.Nunchuck == null)
        {
            C = false;
        }
        return C;
    }

    public bool GetButtonZ()
    {
        bool Z = false;
        if (wiimote.Nunchuck != null)
        {
            if (wiimote.Nunchuck.c)
            {
                Z = true;
            }
        }
        if (wiimote.Nunchuck == null)
        {
            Z = false;
        }
        return Z;
    }

    public Vector2Int GetStick()
    {
        Vector2Int vector2 = new Vector2Int(0,0);
        if (wiimote.Nunchuck != null)
        {
            vector2.x = wiimote.Nunchuck.stick[0];
            vector2.y = wiimote.Nunchuck.stick[1];
            return vector2;
        }
        return vector2;
    }



    void OnApplicationQuit()
    {
        if (wiimote != null)
            WiimoteManager.Cleanup(wiimote);
    }
}
