using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class AudioDebugZone : MonoBehaviour
{
    public AudioDebugController controller;
    [Min(0)] public int stationIndex;
    [Min(0f)] public float reentryCooldownSeconds = 0.3f;

    private readonly Dictionary<int, HashSet<Collider>> occupants = new Dictionary<int, HashSet<Collider>>();
    private readonly Dictionary<Collider, int> colliderOwners = new Dictionary<Collider, int>();
    private float nextEntryTime;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (controller == null)
            controller = FindFirstObjectByType<AudioDebugController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetLocalPlayer(other, out GameObject player))
            return;

        int playerId = player.GetInstanceID();
        if (!occupants.TryGetValue(playerId, out HashSet<Collider> playerColliders))
        {
            playerColliders = new HashSet<Collider>();
            occupants.Add(playerId, playerColliders);
        }

        bool firstCollider = playerColliders.Count == 0;
        playerColliders.Add(other);
        colliderOwners[other] = playerId;

        if (!firstCollider || Time.unscaledTime < nextEntryTime)
            return;

        nextEntryTime = Time.unscaledTime + reentryCooldownSeconds;
        if (controller != null)
            controller.PlayStation(stationIndex, player);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!colliderOwners.TryGetValue(other, out int playerId))
            return;

        colliderOwners.Remove(other);
        if (!occupants.TryGetValue(playerId, out HashSet<Collider> playerColliders))
            return;

        playerColliders.Remove(other);
        if (playerColliders.Count == 0)
            occupants.Remove(playerId);
    }

    private static bool TryGetLocalPlayer(Collider other, out GameObject player)
    {
        player = null;
        if (other.GetComponentInParent<EnemyDummy>() != null)
            return false;

        AudioDebugPlayerController debugPlayer = other.GetComponentInParent<AudioDebugPlayerController>();
        if (debugPlayer != null)
        {
            player = debugPlayer.gameObject;
            return true;
        }

        PlayerNetworkBehavior networkPlayer = other.GetComponentInParent<PlayerNetworkBehavior>();
        if (networkPlayer == null || networkPlayer.GetComponent<PlayerAudioFeedback>() == null)
            return false;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening && (!networkPlayer.IsSpawned || !networkPlayer.IsOwner))
            return false;

        player = networkPlayer.gameObject;
        return true;
    }

    private void OnDisable()
    {
        occupants.Clear();
        colliderOwners.Clear();
        nextEntryTime = 0f;
    }
}
