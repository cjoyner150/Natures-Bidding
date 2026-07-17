using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;

public class PlayerMaskVisualManager : MonoBehaviour
{
    [Header("MaskHolders")]
    [SerializeField] Transform faceMaskHolder;

    ulong playerId;
    List<string> maskIds = new List<string>();

    GameObject currentFaceMask;

    public void Initialize(ulong clientId)
    {
        playerId = clientId;
        maskIds = PersistentPlayerRegistry.Instance.GetByClientId(playerId).masks;

        SpawnMasksOnPlayer();
    }

    private void SpawnMasksOnPlayer()
    {
        //await UniTask.WaitUntil(() => NetworkManager.Singleton.ConnectedClientsList.All(p => p.PlayerObject != null));

        List<MaskVisualSO> masks = GameDataManager.Instance.GetMasks(maskIds);

        if (masks.IsNullOrEmpty()) return;

        currentFaceMask = Instantiate(masks[0].MaskPrefab, faceMaskHolder, false);
        currentFaceMask.transform.localPosition = Vector3.zero;
        currentFaceMask.transform.localRotation = Quaternion.identity;
    }
}
