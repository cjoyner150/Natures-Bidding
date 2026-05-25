using Unity.Netcode;
using UnityEngine;

/// <summary>
/// PlayerInventory — Convenience wrapper around the local player's PlayerData.
/// Add this to the same prefab as PlayerData.
/// Access via PlayerInventory.Local from any UI script.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    #region Static Accessor

    private static PlayerInventory _local;

    public static PlayerInventory Local
    {
        get
        {
            if (_local == null) _local = FindLocalInventory();
            return _local;
        }
    }

    static PlayerInventory FindLocalInventory()
    {
        if (NetworkManager.Singleton == null) return null;
        var playerData = PlayerData.GetPlayer(NetworkManager.Singleton.LocalClientId);
        return playerData != null ? playerData.GetComponent<PlayerInventory>() : null;
    }

    #endregion

    #region Instance Properties

    private PlayerData _playerData;

    public System.Collections.Generic.IReadOnlyList<string> Items =>
        _playerData != null ? _playerData.Items : new System.Collections.Generic.List<string>();

    public int Coins =>
        _playerData != null ? _playerData.Coins.Value : 0;

    public string PlayerName =>
        _playerData != null ? _playerData.PlayerName.Value.Value : "Unknown";

    #endregion

    #region Lifecycle

    void Awake()
    {
        _playerData = GetComponent<PlayerData>();
        if (_playerData == null)
            Debug.LogError("[PlayerInventory] Must be on the same GameObject as PlayerData!");
    }

    void OnEnable()
    {
        if (_playerData != null && _playerData.IsOwner)
            _local = this;
    }

    void OnDisable()
    {
        if (_local == this) _local = null;
    }

    #endregion
}