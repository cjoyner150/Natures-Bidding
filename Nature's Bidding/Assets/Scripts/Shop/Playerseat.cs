using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// PlayerSeat — Place 2–4 of these in your scene in a ring around the item display.
/// Each seat holds one player's stand-in and nameplate.
/// </summary>
public class PlayerSeat : MonoBehaviour
{
    #region Inspector Fields

    [Header("Seat Config")]
    public int seatIndex;

    [Header("References")]
    public Transform standInSpawnPoint;
    public Transform cameraLookTarget;
    public GameObject seatHighlight;
    public TMP_Text namePlate;

    [Header("Stand-In Prefab")]
    public GameObject standInPrefab;

    #endregion

    #region Private State

    private GameObject _spawnedStandIn;
    private ulong      _occupyingClientId = ulong.MaxValue;

    #endregion

    #region Public Properties

    public bool  IsOccupied        => _occupyingClientId != ulong.MaxValue;
    public ulong OccupyingClientId => _occupyingClientId;

    #endregion

    #region Seat Assignment

    public void AssignPlayer(ulong clientId, string playerName)
    {
        _occupyingClientId = clientId;

        if (_spawnedStandIn != null) Destroy(_spawnedStandIn);
        if (standInPrefab != null && standInSpawnPoint != null)
        {
            _spawnedStandIn = Instantiate(standInPrefab, standInSpawnPoint.position, standInSpawnPoint.rotation);
            _spawnedStandIn.transform.SetParent(standInSpawnPoint);
        }

        if (namePlate != null) namePlate.text = playerName;
        SetHighlight(false);
    }

    public void ClearSeat()
    {
        _occupyingClientId = ulong.MaxValue;
        if (_spawnedStandIn != null) Destroy(_spawnedStandIn);
        if (namePlate != null) namePlate.text = "";
        SetHighlight(false);
    }

    #endregion

    #region Highlight

    public void SetHighlight(bool active)
    {
        if (seatHighlight != null)
            seatHighlight.SetActive(active);
    }

    #endregion

    #region Editor Gizmos

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"Seat {seatIndex}");
#endif
    }

    #endregion
}