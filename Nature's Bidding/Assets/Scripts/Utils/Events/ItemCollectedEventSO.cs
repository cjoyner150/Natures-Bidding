using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "New Item Collected Event", menuName = "Events/Item Collected Event")]
public class ItemCollectedEventSO : ScriptableObject
{
    public UnityAction<CollectableSO> onEventRaised;

    [ContextMenu("Raise Event")]
    public void RaiseEvent(CollectableSO collectable)
    {
        onEventRaised?.Invoke(collectable);
    }
}
