using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Player Start Config", menuName = "Game/Player Start Config")]
public class PlayerStartConfigSO : ScriptableObject
{
    public int gold;
    public List<StatusEffectorSO> masks;
    public List<StatusEffectorSO> tarots;
    public List<StatusEffectorSO> artifacts;
}