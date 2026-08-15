using Unity.Netcode;
using UnityEngine;

public class InstantKillBounds : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.gameObject.CompareTag("Player"))
        {
            UtilityExtensions.TryGetInParents<PlayerHealth>(other.gameObject, out var playerHealth);

            if (playerHealth != null)
            {
                GameLogger.Log(LogSeverity.Info, $"Player clientId={playerHealth.OwnerClientId} entered instant kill bounds.");

                IGameServerHandler serverHandler = CombatServerHandler.Instance;
                serverHandler ??= LobbyServerHandler.Instance;

                if (serverHandler != null)
                {
                    serverHandler.HandleInstantKill(playerHealth);
                }
            }
            else
            {
                GameLogger.Log(LogSeverity.Warning, $"No PlayerHealth found in parents of {other.gameObject.name}");
            }
        }
    }
}
