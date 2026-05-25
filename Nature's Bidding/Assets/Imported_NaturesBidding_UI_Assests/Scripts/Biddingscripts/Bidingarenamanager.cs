using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// BiddingArenaManager — Manages the 3D side of the bidding phase.
/// Assigns players to seats once, spawns and animates the item each round.
/// The PointerNPC is now purely decorative (idle animation only).
/// </summary>
public class BiddingArenaManager : NetworkBehaviour
{
    public static BiddingArenaManager Instance { get; private set; }

    #region Inspector Fields

    [Header("Seats (assign in order 0–3)")]
    public List<PlayerSeat> seats = new List<PlayerSeat>();

    [Header("Item Display")]
    public Transform itemDisplayAnchor;
    public float     itemBobSpeed  = 1f;
    public float     itemBobHeight = 0.15f;
    public float     itemSpinSpeed = 30f;

    [Header("Item Pool — drag BiddableItem SOs here")]
    public List<BiddableItem> itemPool = new List<BiddableItem>();

    [Header("2D Overlay UI")]
    public TMP_Text                  itemNameText;
    public TMP_Text                  itemDescText;
    public UnityEngine.UI.Image      itemIconImage;
    public TMP_Text                  itemRarityText;

    #endregion

    #region Private State

    private GameObject   _spawnedItemDisplay;
    private BiddableItem _currentItem;
    private int          _itemPoolIndex = 0;
    private Dictionary<ulong, PlayerSeat> _playerSeatMap = new Dictionary<ulong, PlayerSeat>();

    #endregion

    #region Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    #endregion

    #region Seat Assignment

    /// <summary>Called once at the start of the bidding phase.</summary>
    public void AssignPlayersToSeats()
    {
        if (!IsServer) return;

        int seatIdx = 0;
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            if (seatIdx >= seats.Count) break;
            var    pd   = PlayerData.GetPlayer(kvp.Key);
            string name = pd != null ? pd.PlayerName.Value.Value : $"Player {kvp.Key}";
            AssignSeatRpc(kvp.Key, seatIdx, name);
            seatIdx++;
        }
    }

    [Rpc(SendTo.Everyone)]
    void AssignSeatRpc(ulong clientId, int seatIndex, string playerName)
    {
        if (seatIndex >= seats.Count) return;
        var seat = seats[seatIndex];
        seat.AssignPlayer(clientId, playerName);
        _playerSeatMap[clientId] = seat;
    }

    #endregion

    #region Item Display

    public BiddableItem PickNextItem()
    {
        if (itemPool == null || itemPool.Count == 0)
        {
            Debug.LogWarning("[BiddingArenaManager] Item pool is empty!");
            return null;
        }

        _currentItem = itemPool[_itemPoolIndex % itemPool.Count];
        _itemPoolIndex++;
        ShowItemRpc(_itemPoolIndex - 1);
        return _currentItem;
    }

    public BiddableItem GetCurrentItem() => _currentItem;

    [Rpc(SendTo.Everyone)]
    void ShowItemRpc(int poolIndex)
    {
        if (itemPool == null || itemPool.Count == 0) return;

        var item = itemPool[poolIndex % itemPool.Count];
        _currentItem = item;

        if (_spawnedItemDisplay != null) Destroy(_spawnedItemDisplay);

        if (item.displayPrefab != null && itemDisplayAnchor != null)
        {
            _spawnedItemDisplay = Instantiate(
                item.displayPrefab,
                itemDisplayAnchor.position,
                Quaternion.Euler(item.displayRotation));
            _spawnedItemDisplay.transform.localScale = Vector3.one * item.displayScale;
        }

        if (itemNameText)  { itemNameText.text  = item.itemName; itemNameText.color = item.RarityColor(); }
        if (itemDescText)    itemDescText.text   = item.description;
        if (itemIconImage)   itemIconImage.sprite = item.icon;
        if (itemRarityText)
        {
            string[] labels = { "", "Common", "Rare", "Legendary" };
            int r = Mathf.Clamp(item.rarity, 1, 3);
            itemRarityText.text  = labels[r];
            itemRarityText.color = item.RarityColor();
        }

        PointerNPC.Instance?.CelebrateOne();
        PointerNPC.Instance?.SayItemReveal(item.itemName, item.description);
    }

    public void ClearItemDisplay()
    {
        if (_spawnedItemDisplay != null) Destroy(_spawnedItemDisplay);
        _spawnedItemDisplay = null;
    }

    #endregion

    #region Item Animation

    void Update()
    {
        if (_spawnedItemDisplay == null || itemDisplayAnchor == null) return;

        float newY = itemDisplayAnchor.position.y
                   + Mathf.Sin(Time.time * itemBobSpeed) * itemBobHeight;

        _spawnedItemDisplay.transform.position = new Vector3(
            itemDisplayAnchor.position.x,
            newY,
            itemDisplayAnchor.position.z);

        _spawnedItemDisplay.transform.Rotate(
            Vector3.up, itemSpinSpeed * Time.deltaTime, Space.World);
    }

    #endregion
}