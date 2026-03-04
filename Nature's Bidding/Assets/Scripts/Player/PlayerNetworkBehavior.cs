using Unity.Netcode;
using UnityEngine;

public class PlayerNetworkBehavior : NetworkBehaviour
{
    [SerializeField] PlayerContext ctx;
    private PlayerInputManager playerInput;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            playerInput = gameObject.AddComponent<PlayerInputManager>();
            playerInput.InitializePlayer(ctx);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + (transform.up * .125f), .2f);
    }
}
