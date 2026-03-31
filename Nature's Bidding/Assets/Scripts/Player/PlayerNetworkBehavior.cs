using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

public class PlayerNetworkBehavior : NetworkBehaviour
{
    [SerializeField] PlayerContext ctx;
    private PlayerInputManager playerInput;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        TestingGameManager.OnSessionStarted.AddListener(OnSessionStarted);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        TestingGameManager.OnSessionStarted.RemoveListener(OnSessionStarted);
    }

    public void OnSessionStarted()
    {
        if (IsOwner)
        {
            playerInput = gameObject.AddComponent<PlayerInputManager>();
            playerInput.InitializePlayer(ctx);
            
            NetworkSessionManager.Instance.RegisterClientIdRpc(OwnerClientId, AuthenticationService.Instance.PlayerId, AuthenticationService.Instance.PlayerName);
        }
        
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + (transform.up * .125f), .2f);
    }
}
