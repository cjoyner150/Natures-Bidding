using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public enum OperatorType { Addition, Multiplication, Division, Subtraction, SetEqual }

public abstract class StatusEffectorSO : ScriptableObject
{
    [SerializeField] private string _id;

    public string Id { get { return _id; } }
    public string Title;
    public string Description;
    public float Duration = -1;
    public abstract List<StatusEffect> GetStatusEffects();

#if UNITY_EDITOR
    private void OnValidate()
    {
        bool changed = false;

        string assetName = System.IO.Path.GetFileNameWithoutExtension(
            UnityEditor.AssetDatabase.GetAssetPath(this)
        );

        if (string.IsNullOrEmpty(assetName)) return;

        if (string.IsNullOrEmpty(_id))
        {
            _id = assetName.ToLower().Replace(" ", "_");
            changed = true;
        }

        if (string.IsNullOrEmpty(Title))
        {
            Title = assetName;
            changed = true;
        }

        if (changed)
            UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}



