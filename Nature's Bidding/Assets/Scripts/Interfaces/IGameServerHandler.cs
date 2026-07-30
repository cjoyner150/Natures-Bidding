public interface IGameServerHandler
{
    void RequestHitPlayerServerRpc(ulong attackingPlayerId, ulong hitPlayerId, float damage, bool critical = false);
    void RequestHealServerRpc(ulong targetClientId, float amount);
    void HandleInstantKill(PlayerHealth playerHealth);
    void OnPlayerDeath(ulong clientId);
}