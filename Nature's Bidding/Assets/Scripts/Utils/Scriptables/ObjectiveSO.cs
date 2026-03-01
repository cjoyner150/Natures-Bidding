using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Objective", menuName = "Objective")]
public class ObjectiveSO : ScriptableObject
{
    public string Name;
    public string Description;
    public bool Hidden;
    public bool Completed;
}
