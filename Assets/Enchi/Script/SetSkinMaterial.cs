using Unity.Netcode;
using UnityEngine;

public class SetSkinMaterial : NetworkBehaviour
{
    [SerializeField] Renderer playerSkinHead;
    [SerializeField] Renderer playerSkinarm;
    [SerializeField] Material skinMaterial1;
    [SerializeField] Material skinMaterial2;
    [SerializeField] Material skinMaterial3;
    [SerializeField] Material skinMaterialKanasimi1;
    [SerializeField] Material skinMaterialKanasimi2;
    [SerializeField] Material skinMaterialKanasimi3;
    [SerializeField] Material skinMaterialKizetu1;
    [SerializeField] Material skinMaterialKizetu2;
    [SerializeField] Material skinMaterialKizetu3;
    [SerializeField] Material skinMaterialOkori1;
    [SerializeField] Material skinMaterialOkori2;
    [SerializeField] Material skinMaterialOkori3;
    [SerializeField] Material skinMaterialHappy1;
    [SerializeField] Material skinMaterialHappy2;
    [SerializeField] Material skinMaterialHappy3;
    [SerializeField] Material skinMaterialAori1;
    [SerializeField] Material skinMaterialAori2;
    [SerializeField] Material skinMaterialAori3;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetNormalMaterialClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetNormalMaterialServerRpc()
    {
        SetNormalMaterialClientRpc();
    }

    [ClientRpc]
    void SetNormalMaterialClientRpc()
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


    [ServerRpc(RequireOwnership = false)]
    public void RequestSetKanasimiMaterialServerRpc()
    {
        SetKanasimiMaterialClientRpc();
    }

    [ClientRpc]
    void SetKanasimiMaterialClientRpc()
    {
        if (OwnerClientId == 0)
        {
            playerSkinHead.material = skinMaterialKanasimi1;
            playerSkinarm.material = skinMaterialKanasimi1;
        }
        if (OwnerClientId == 1)
        {
            playerSkinHead.material = skinMaterialKanasimi2;
            playerSkinarm.material = skinMaterialKanasimi2;
        }
        if (OwnerClientId == 2)
        {
            playerSkinHead.material = skinMaterialKanasimi3;
            playerSkinarm.material = skinMaterialKanasimi3;
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void RequestSetKizetuMaterialServerRpc()
    {
        SetKizetuMaterialClientRpc();
    }

    [ClientRpc]
    void SetKizetuMaterialClientRpc()
    {
        if (IsClient)
        {
            if (OwnerClientId == 0)
            {
                playerSkinHead.material = skinMaterialKizetu1;
                playerSkinarm.material = skinMaterialKizetu1;
            }
            if (OwnerClientId == 1)
            {
                playerSkinHead.material = skinMaterialKizetu2;
                playerSkinarm.material = skinMaterialKizetu2;
            }
            if (OwnerClientId == 2)
            {
                playerSkinHead.material = skinMaterialKizetu3;
                playerSkinarm.material = skinMaterialKizetu3;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetOkoriMaterialServerRpc()
    {
        SetOkoriMaterialClientRpc();
    }

    [ClientRpc]
    void SetOkoriMaterialClientRpc()
    {
        if (IsClient)
        {
            if (OwnerClientId == 0)
            {
                playerSkinHead.material = skinMaterialOkori1;
                playerSkinarm.material = skinMaterialOkori1;
            }
            if (OwnerClientId == 1)
            {
                playerSkinHead.material = skinMaterialOkori2;
                playerSkinarm.material = skinMaterialOkori2;
            }
            if (OwnerClientId == 2)
            {
                playerSkinHead.material = skinMaterialOkori3;
                playerSkinarm.material = skinMaterialOkori3;
            }
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void RequestSetHappyMaterialServerRpc()
    {
        SetHappyMaterialClientRpc();
    }

    [ClientRpc]
    void SetHappyMaterialClientRpc()
    {
        if (IsClient)
        {
            if (OwnerClientId == 0)
            {
                playerSkinHead.material = skinMaterialHappy1;
                playerSkinarm.material = skinMaterialHappy1;
            }
            if (OwnerClientId == 1)
            {
                playerSkinHead.material = skinMaterialHappy2;
                playerSkinarm.material = skinMaterialHappy2;
            }
            if (OwnerClientId == 2)
            {
                playerSkinHead.material = skinMaterialHappy3;
                playerSkinarm.material = skinMaterialHappy3;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetAoriMaterialServerRpc()
    {
        SetAoriMaterialClientRpc();
    }

    [ClientRpc]
    void SetAoriMaterialClientRpc()
    {
        if (IsClient)
        {
            if (OwnerClientId == 0)
            {
                playerSkinHead.material = skinMaterialAori1;
                playerSkinarm.material = skinMaterialAori1;
            }
            if (OwnerClientId == 1)
            {
                playerSkinHead.material = skinMaterialAori2;
                playerSkinarm.material = skinMaterialAori2;
            }
            if (OwnerClientId == 2)
            {
                playerSkinHead.material = skinMaterialAori3;
                playerSkinarm.material = skinMaterialAori3;
            }
        }
    }

}
