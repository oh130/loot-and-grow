using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class DieMarker : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if(IsServer)
        {
            AutoDestroy();
        }
    }

    private void AutoDestroy()
    {
        DOVirtual.DelayedCall(60f, () =>
        {
            gameObject.GetComponent<NetworkObject>().Despawn();
        }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }
}
