using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks when ALL connected clients have finished loading a networked scene,
/// on server and clients alike. Wraps NetworkSceneManager.OnLoadEventCompleted
/// and caches results so a late subscriber still gets the right answer.
///
/// Hook Attach() when a session connects and Detach() when it ends
/// (NetworkSessionManager.OnSessionConnected / OnSessionDisconnected).
///
/// Usage:
///   await SceneReadiness.WaitForAllPlayersLoaded();          // current active scene
///   await SceneReadiness.WaitForAllPlayersLoaded("Shop", ct);
///   if (SceneReadiness.IsFullyLoaded("Shop")) ...
/// </summary>
public static class SceneReadiness
{
    private static readonly HashSet<string> _completedScenes = new HashSet<string>();
    private static readonly Dictionary<string, UniTaskCompletionSource> _waiters = new Dictionary<string, UniTaskCompletionSource>();
    private static bool _attached;

    public static void Attach()
    {
        if (_attached || NetworkManager.Singleton?.SceneManager == null) return;
        _attached = true;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
    }

    public static void Detach()
    {
        if (!_attached) return;
        _attached = false;

        if (NetworkManager.Singleton?.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }

        Reset();
    }

    public static bool IsFullyLoaded(string sceneName = null)
    {
        sceneName ??= SceneManager.GetActiveScene().name;
        return _completedScenes.Contains(sceneName);
    }

    /// <summary>Completes when every connected client has finished loading the scene. Returns immediately if that already happened.</summary>
    public static async UniTask WaitForAllPlayersLoaded(string sceneName = null, CancellationToken ct = default)
    {
        sceneName ??= SceneManager.GetActiveScene().name;

        if (_completedScenes.Contains(sceneName)) return;

        if (!_waiters.TryGetValue(sceneName, out var tcs))
        {
            tcs = new UniTaskCompletionSource();
            _waiters[sceneName] = tcs;
        }

        await tcs.Task.AttachExternalCancellation(ct);
    }

    private static void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (clientsTimedOut.Count > 0)
            GameLogger.Log(LogSeverity.Warning, $"Scene '{sceneName}' load completed, but clients timed out: {string.Join(", ", clientsTimedOut)}. Proceeding without them.");
        else
            GameLogger.Log(LogSeverity.Debug, $"Scene '{sceneName}' loaded by all {clientsCompleted.Count} clients.");

        MarkLoaded(sceneName);
    }

    private static void OnSceneEvent(SceneEvent e)
    {
        switch (e.SceneEventType)
        {
            // A new single-mode load is starting: previous scenes' completion no longer applies.
            case SceneEventType.Load when e.LoadSceneMode == LoadSceneMode.Single:
                Reset();
                break;

            // Late joiner: the server never sends LoadEventCompleted for a scene that was
            // already loaded before we connected. SynchronizeComplete is our "loaded" signal.
            case SceneEventType.SynchronizeComplete when e.ClientId == NetworkManager.Singleton.LocalClientId:
                MarkLoaded(SceneManager.GetActiveScene().name);
                break;
        }
    }

    private static void MarkLoaded(string sceneName)
    {
        _completedScenes.Add(sceneName);

        if (_waiters.TryGetValue(sceneName, out var tcs))
        {
            _waiters.Remove(sceneName);
            tcs.TrySetResult();
        }
    }

    private static void Reset()
    {
        _completedScenes.Clear();
        // Don't complete pending waiters on reset — they're waiting on a scene that's
        // now being replaced. Their cancellation token is the way out.
        _waiters.Clear();
    }
}