using Unity.Netcode;
using UnityEngine;

public class MapDebugStarter : MonoBehaviour
{
    private void Start()
    {
        // Wait one frame to ensure NetworkManager is fully awake
        Invoke(nameof(CheckAndStartNetwork), 0.1f);
    }

    private void CheckAndStartNetwork()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("MapDebugStarter: No active network found. Automatically starting as Host for testing.");
            NetworkManager.Singleton.StartHost();
        }
    }
}