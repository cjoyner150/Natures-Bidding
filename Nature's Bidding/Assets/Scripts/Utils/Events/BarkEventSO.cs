using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "New Bark Event", menuName = "Events/Bark Event")]
public class BarkEventSO : ScriptableObject
{
    [Serializable]
    public enum DialogueTrigger
    {
        enemyBark1,
        enemyBark2,
        enemyBark3
    }

    public UnityAction<DialogueTrigger> onEventRaised;

    [ContextMenu("Raise Event")]
    public void RaiseEvent(int val)
    {
        onEventRaised?.Invoke((DialogueTrigger)val);
    }
}
