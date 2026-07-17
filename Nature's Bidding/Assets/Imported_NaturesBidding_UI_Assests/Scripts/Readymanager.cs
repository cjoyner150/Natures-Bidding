using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ReadyManager — Tracks which players have pressed Ready.
/// When all connected players are ready, triggers the next phase.
/// Attach to a NetworkObject in the scene.
/// </summary>
public class ReadyManager : BaseGameServerHandler<ReadyManager>, IGameServerHandler
{
    #region Inspector Fields

    [Header("Ready Button — place one in each PlayerShopPanel")]
    public Button   readyButton;
    public TMP_Text readyButtonText;

    [Header("Ready Status Display")]
    public TMP_Text readyCountText;   // "2 / 4 Ready"

    #endregion

    #region Network Variables

    // Packed as "clientId,clientId,..." — NetworkList not used to avoid complexity
    public NetworkVariable<int> ReadyCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Private State

    private bool              _localReady    = false;
    private HashSet<ulong>    _readyPlayers  = new HashSet<ulong>();

    #endregion

    #region Lifecycle

    void Awake() { }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ReadyCount.OnValueChanged += (_, count) => RefreshUI(count);
        RefreshUI(ReadyCount.Value);
    }

    #endregion

    #region Phase Reset

    /// <summary>Call when shop phase starts to reset all ready states.</summary>
    public void ResetForNewPhase()
    {
        if (IsServer)
        {
            _readyPlayers.Clear();
            ReadyCount.Value = 0;
        }
        _localReady = false;
        RefreshUI(0);
    }

    #endregion

    #region Ready Button

    public void OnReadyClicked()
    {
        if (_localReady) return;
        _localReady = true;

        if (readyButton)     readyButton.interactable = false;
        if (readyButtonText) readyButtonText.text     = "Ready ✓";

        SubmitReadyRpc();
    }

    [Rpc(SendTo.Server)]
    void SubmitReadyRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (_readyPlayers.Contains(sender)) return;

        _readyPlayers.Add(sender);
        ReadyCount.Value = _readyPlayers.Count;

        int totalPlayers = NetworkManager.Singleton.ConnectedClients.Count;
        if (_readyPlayers.Count >= totalPlayers)
            AllReadyRpc();
    }

    [Rpc(SendTo.Everyone)]
    void AllReadyRpc()
    {
        if (IsServer)
            PersistentGameStateManager.Instance?.BeginCombatPhaseServer();
    }

    [Rpc(SendTo.Server)]
    public void StartCombatPhaseRpc()
    {
        if (!IsServer) return;

        PersistentGameStateManager.Instance?.BeginCombatPhaseServer();
    }

    public void OnPlayerDeath(ulong clientId) { }

    #endregion

    #region UI

    void RefreshUI(int count)
    {
        int total = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClients.Count
            : 0;

        if (readyCountText)
            readyCountText.text = $"{count} / {total} Ready";
    }

    #endregion
}