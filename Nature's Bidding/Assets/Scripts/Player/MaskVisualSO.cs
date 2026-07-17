using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Mask Visual", fileName = "New Mask Visual")]
public class MaskVisualSO : ScriptableObject
{
    public string Id;
    public GameObject MaskPrefab;
}