using Unity.Netcode;
using UnityEngine;

public class SetSkinMaterial : NetworkBehaviour
{
    [SerializeField] Renderer playerSkin;
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
                playerSkin.material = skinMaterial1;
            }
            if (OwnerClientId == 1)
            {
                playerSkin.material = skinMaterial2;
            }
            if (OwnerClientId == 2)
            {
                playerSkin.material = skinMaterial3;
            }
        }
    }
}
