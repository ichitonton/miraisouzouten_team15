using Unity.Netcode;
using UnityEngine;

public class ResultSetSkinMaterial : MonoBehaviour
{
    [SerializeField] Renderer playerSkinHead;
    [SerializeField] Renderer playerSkinarm;
    [SerializeField] Material skinMaterial1;
    [SerializeField] Material skinMaterial2;
    [SerializeField] Material skinMaterial3;
    [SerializeField] Material skinMaterialWin1;
    [SerializeField] Material skinMaterialWin2;
    [SerializeField] Material skinMaterialWin3;
    [SerializeField] Material skinMaterialLose1;
    [SerializeField] Material skinMaterialLose2;
    [SerializeField] Material skinMaterialLose3;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (FinalScore.MyPlayerID == 0)
        {
            playerSkinHead.material = skinMaterial1;
            playerSkinarm.material = skinMaterial1;
        }
        if (FinalScore.MyPlayerID == 1)
        {
            playerSkinHead.material = skinMaterial2;
            playerSkinarm.material = skinMaterial2;
        }
        if (FinalScore.MyPlayerID == 2)
        {
            playerSkinHead.material = skinMaterial3;
            playerSkinarm.material = skinMaterial3;
        }
    }

    public void SetSkinMaterialWin()
    {
        if (FinalScore.MyPlayerID == 0)
        {
            playerSkinHead.material = skinMaterialWin1;
            playerSkinarm.material = skinMaterialWin1;
        }
        if (FinalScore.MyPlayerID == 1)
        {
            playerSkinHead.material = skinMaterialWin2;
            playerSkinarm.material = skinMaterialWin2;
        }
        if (FinalScore.MyPlayerID == 2)
        {
            playerSkinHead.material = skinMaterialWin3;
            playerSkinarm.material = skinMaterialWin3;
        }
    }
    public void SetSkinMaterialLose()
    {
        if (FinalScore.MyPlayerID == 0)
        {
            playerSkinHead.material = skinMaterialLose1;
            playerSkinarm.material = skinMaterialLose1;
        }
        if (FinalScore.MyPlayerID == 1)
        {
            playerSkinHead.material = skinMaterialLose2;
            playerSkinarm.material = skinMaterialLose2;
        }
        if (FinalScore.MyPlayerID == 2)
        {
            playerSkinHead.material = skinMaterialLose3;
            playerSkinarm.material = skinMaterialLose3;
        }
    }

}
