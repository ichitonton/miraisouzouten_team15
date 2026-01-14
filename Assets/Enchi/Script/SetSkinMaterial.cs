using Unity.Netcode;
using UnityEngine;

public class SetSkinMaterial : NetworkBehaviour
{
    [SerializeField] Renderer playerSkinHead;
    [SerializeField] Renderer playerSkinarm;
    [SerializeField] Material skinMaterial1;
    [SerializeField] Material skinMaterial2;
    [SerializeField] Material skinMaterial3;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        if (IsClient)
        {
            if (OwnerClientId == 0)
            {
                playerSkinHead.material = skinMaterial1;
                playerSkinarm.material = skinMaterial1;
            }
            if (OwnerClientId == 1)
            {
                playerSkinHead.material = skinMaterial2;
                playerSkinarm.material = skinMaterial2;
            }
            if (OwnerClientId == 2)
            {
                playerSkinHead.material = skinMaterial3;
                playerSkinarm.material = skinMaterial3;
            }
        }
    }
}
