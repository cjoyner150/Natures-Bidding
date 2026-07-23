using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class UtilityExtensions
{
    public static T[] GetComponentsOnlyInChildren<T>(this MonoBehaviour script) where T : class
    {
        List<T> group = new List<T>();

        //collect only if its an interface or a Component
        if (typeof(T).IsInterface
         || typeof(T).IsSubclassOf(typeof(Component))
         || typeof(T) == typeof(Component))
        {
            foreach (Transform child in script.transform)
            {
                group.AddRange(child.GetComponentsInChildren<T>());
            }
        }

        return group.ToArray();
    }

    public static T GetInParents<T>(GameObject go) where T : class
    {
        return go.GetComponentInParent<T>();
    }

    public static T GetInParents<T>(Collider collider) where T : class =>
        GetInParents<T>(collider.gameObject);

    public static T GetInParents<T>(Component component) where T : class =>
        GetInParents<T>(component.gameObject);

    public static bool TryGetInParents<T>(GameObject go, out T result) where T : class
    {
        result = GetInParents<T>(go);
        return result != null;
    }

    public static bool TryGetInParents<T>(Collider collider, out T result) where T : class =>
        TryGetInParents(collider.gameObject, out result);

    public static bool TryGetInParents<T>(Component component, out T result) where T : class =>
        TryGetInParents(component.gameObject, out result);
}
