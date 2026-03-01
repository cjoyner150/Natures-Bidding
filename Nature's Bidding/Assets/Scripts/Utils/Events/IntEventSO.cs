using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "New Int Event", menuName = "Events/Int Event")]
public class IntEventSO : ScriptableObject
{
    public UnityAction<int> onEventRaised;

    [ContextMenu("Raise Event")]
    public void RaiseEvent(int val)
    {
        onEventRaised?.Invoke(val);
    }
}
