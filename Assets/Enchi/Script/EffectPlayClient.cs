using Unity.Netcode;
using UnityEngine;

public class EffectPlayClient : NetworkBehaviour
{
    ParticleSystem _effect;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        //子オブジェクトがParticleSystemを持っているか確認
        if(GetComponentInChildren<ParticleSystem>() != null)
        {
            //Debug.Log("ぷれいエフェクト");
            _effect = GetComponentInChildren<ParticleSystem>();
            PlayEffectClientRpc();
        }
    }
    public override void OnNetworkSpawn()
    {
        PlayEffectClientRpc();

    }

    [ClientRpc]
    public void PlayEffectClientRpc()
    {
        if (_effect != null)
        {
            _effect.Play();
        }
    }
}
