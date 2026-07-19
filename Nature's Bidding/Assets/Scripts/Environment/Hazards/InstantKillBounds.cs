using Unity.Netcode;
using UnityEngine;

public class InstantKillBounds : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        Debug.Log($"[InstantKillBounds] OnTriggerEnter fired. other={other.gameObject.name}, tag={other.gameObject.tag}");

        if (other.gameObject.CompareTag("Player"))
        {
            UtilityExtensions.TryGetInParents<PlayerHealth>(other.gameObject, out var playerHealth);

            if (playerHealth != null)
            {
                Debug.Log($"[InstantKillBounds] Resolved PlayerHealth on {playerHealth.gameObject.name}, OwnerClientId={playerHealth.OwnerClientId}");

                IGameServerHandler serverHandler = CombatServerHandler.Instance;
                serverHandler ??= LobbyServerHandler.Instance;

                if (serverHandler != null)
                {
                    serverHandler.HandleInstantKill(playerHealth);
                }
            }
            else
            {
                Debug.LogWarning($"[InstantKillBounds] No PlayerHealth found in parents of {other.gameObject.name}");
            }
        }
    }
}
