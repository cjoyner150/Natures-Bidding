using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class NetworkedWeaponFactory : NetworkSingleton<NetworkedWeaponFactory>
{
    private Dictionary<ulong, GameObject> _spawnedWeaponVisuals = new();
    private Dictionary<ulong, UniTaskCompletionSource<GameObject>> _pendingEquipRequests = new();
    private ulong _nextRequestId = 1;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        DontDestroyOnLoad(gameObject);
    }

    public async UniTask<GameObject> EquipWeapon(ulong playerId, string weaponId)
    {
        ulong requestId = _nextRequestId++;
        var tcs = new UniTaskCompletionSource<GameObject>();
        _pendingEquipRequests[requestId] = tcs;

        SpawnWeaponServerRpc(playerId, weaponId, requestId, NetworkManager.Singleton.LocalClientId);

        return await tcs.Task;
    }

    public void UnequipWeapon(ulong playerId)
    {
        DespawnWeaponServerRpc(playerId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SpawnWeaponServerRpc(ulong playerId, string weaponId, ulong requestId, ulong requestingClientId)
    {
        if (!NetworkManager.ConnectedClientsIds.Contains(playerId))
        {
            FailEquipClientRpc(requestId,
                NetworkManager.Singleton.RpcTarget.Single(requestingClientId, RpcTargetUse.Temp));
            return;
        }

        SpawnWeaponVisualRpc(playerId, weaponId, requestId, requestingClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DespawnWeaponServerRpc(ulong playerId)
    {
        if (!NetworkManager.ConnectedClientsIds.Contains(playerId)) return;
        DespawnWeaponVisualRpc(playerId);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void SpawnWeaponVisualRpc(ulong playerId, string weaponId, ulong requestId, ulong requestingClientId)
    {
        SpawnWeaponVisualAsync(playerId, weaponId, requestId, requestingClientId).Forget();
    }

    private async UniTaskVoid SpawnWeaponVisualAsync(ulong playerId, string weaponId, ulong requestId, ulong requestingClientId)
    {
        DestroyExistingVisual(playerId);

        GameObject go = null;
        var so = GameDataManager.Instance.GetWeapon(weaponId);

        if (so != null && so.weaponPrefab != null)
        {
            await UniTask.WaitUntil(() =>
                NetworkManager.ConnectedClients.TryGetValue(playerId, out var c) &&
                c.PlayerObject != null
            );

            if (NetworkManager.ConnectedClients.TryGetValue(playerId, out var client) && client.PlayerObject != null)
            {
                var weaponHolder = client.PlayerObject.GetComponentInChildren<WeaponHolder>();
                if (weaponHolder != null)
                {
                    go = Instantiate(so.weaponPrefab, weaponHolder.transform);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    _spawnedWeaponVisuals[playerId] = go;
                }
            }
        }

        if (NetworkManager.Singleton.LocalClientId == requestingClientId)
        {
            if (_pendingEquipRequests.TryGetValue(requestId, out var tcs))
            {
                _pendingEquipRequests.Remove(requestId);
                tcs.TrySetResult(go);
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void FailEquipClientRpc(ulong requestId, RpcParams rpcParams = default)
    {
        if (_pendingEquipRequests.TryGetValue(requestId, out var tcs))
        {
            _pendingEquipRequests.Remove(requestId);
            tcs.TrySetResult(null);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void DespawnWeaponVisualRpc(ulong playerId)
    {
        DestroyExistingVisual(playerId);
    }

    private void DestroyExistingVisual(ulong playerId)
    {
        if (_spawnedWeaponVisuals.TryGetValue(playerId, out var existing) && existing != null)
        {
            Destroy(existing);
            _spawnedWeaponVisuals.Remove(playerId);
        }
    }

    public bool CreateWeaponCollectable(WeaponConfigSO so, Vector3 position, Quaternion rotation)
    {
        if (so != null && so.weaponCollectablePrefab != null)
        {
            var go = Instantiate(so.weaponCollectablePrefab, position, rotation);
            return go != null;
        }
        return false;
    }
}

