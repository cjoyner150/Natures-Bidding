using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "New String Event", menuName = "Events/String Event")]
public class StringEventSO : ScriptableObject
{
    public UnityAction<string> onEventRaised;

    [ContextMenu("Raise Event")]
    public void RaiseEvent(string val)
    {
        onEventRaised?.Invoke(val);
    }
}
