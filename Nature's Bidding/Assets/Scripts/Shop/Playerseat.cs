using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Linq;

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
    private string[] colorHexByIndex = new string[]
    {
        "#FF0700",
        "#00FF0B",
        "#007BFF",
        "#FFF209"
    };

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

            SkinnedMeshRenderer[] meshes = _spawnedStandIn.GetComponentsInChildren<SkinnedMeshRenderer>();
            var skinnedMeshRenderer = meshes.First(m => !m.CompareTag("Leaves"));

            if (skinnedMeshRenderer == null)
            {
                GameLogger.Log(LogSeverity.Error, "mesh found on spawned player obj is null");
                return;
            }

            GameLogger.Log(LogSeverity.Debug, $"mesh.materials.Length={skinnedMeshRenderer.materials.Length}");

            var player = PersistentPlayerRegistry.Instance.GetByClientId(clientId);
            if (player == null)
            {
                GameLogger.Log(LogSeverity.Error, $"no registry entry found for client {clientId}");
                return;
            }

            GameLogger.Log(LogSeverity.Debug, $"player.playerIndex={player.playerIndex}, colorHexByIndex.Length={colorHexByIndex.Length}");

            if (player.playerIndex < 0 || player.playerIndex >= colorHexByIndex.Length)
            {
                GameLogger.Log(LogSeverity.Error, $"playerIndex {player.playerIndex} out of bounds for colorHexByIndex (length {colorHexByIndex.Length}). Using fallback color.");
                skinnedMeshRenderer.materials[2].SetColor("_Tint", Color.white);
            }
            else
            {
                string hex = colorHexByIndex[player.playerIndex];
                bool parsed = ColorUtility.TryParseHtmlString(hex, out var c);
                GameLogger.Log(LogSeverity.Debug, $"hex='{hex}', parsed={parsed}, result={c}");
                Color playerColor = ColorUtility.TryParseHtmlString(colorHexByIndex[player.playerIndex], out c) ? c : Color.white;
                GameLogger.Log(LogSeverity.Debug, $"Has _Tint property: {skinnedMeshRenderer.materials[2].HasProperty("_Tint")}");
                skinnedMeshRenderer.materials[2].SetColor("_Tint", playerColor);
                GameLogger.Log(LogSeverity.Debug, $"Color after set: {skinnedMeshRenderer.materials[2].GetColor("_Tint")}");
            }

            Animator anim = _spawnedStandIn.GetComponentInChildren<Animator>();
            if (anim == null) {
                GameLogger.Log(LogSeverity.Error, "No animator found on seated player obj");
                return;
            }

            RandomizeIdleAnimation(anim);
        }

        if (namePlate != null) namePlate.text = playerName;
        SetHighlight(false);
    }

    private void RandomizeIdleAnimation(Animator animator)
    {
        animator.speed = Random.Range(1f, 1.2f);

        float randomOffset = Random.Range(0f, 1f);
        animator.Play("Idle", 0, randomOffset);
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