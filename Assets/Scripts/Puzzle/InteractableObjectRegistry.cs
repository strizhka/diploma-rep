using System.Collections.Generic;
using UnityEngine;

public static class InteractableObjectRegistry
{
    private static readonly Dictionary<string, InteractableObject> _objects = new();

    public static void Register(string id, InteractableObject obj)
    {
        if (_objects.TryGetValue(id, out var existing))
        {
            if (existing == obj) return;

            Debug.LogError($"[Registry] Дублирующийся objectId: '{id}'. Два РАЗНЫХ объекта имеют одинаковый ID.");
            return;
        }

        _objects[id] = obj;
    }
    public static void Unregister(string id)
    {
        _objects.Remove(id);
    }

    public static InteractableObject Get(string id)
    {
        if (_objects.TryGetValue(id, out var obj))
            return obj;

        Debug.LogWarning($"[Registry] Объект с ID '{id}' не найден.");
        return null;
    }

    public static bool TryGet(string id, out InteractableObject obj)
    {
        return _objects.TryGetValue(id, out obj);
    }

    public static IEnumerable<KeyValuePair<string, InteractableObject>> GetAll()
    {
        return _objects;
    }

    public static void ClearAll() => _objects.Clear();

    public static int Count => _objects.Count;
}