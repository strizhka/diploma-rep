using System.Collections.Generic;
using UnityEngine;

public class NetworkGameEventRegistry : MonoBehaviour
{
    [SerializeField] private GameEvent[] _events;

    private static Dictionary<int, GameEvent> _eventMap;

    private void Awake()
    {
        _eventMap = new Dictionary<int, GameEvent>();

        foreach (var e in _events)
        {
            if (!_eventMap.ContainsKey(e.EventId))
                _eventMap.Add(e.EventId, e);
            else
                Debug.LogError($"Duplicate Event ID: {e.EventId}");
        }
    }

    public static GameEvent Get(int id)
    {
        if (_eventMap.TryGetValue(id, out var e))
            return e;

        Debug.LogWarning($"Event ID {id} not found.");
        return null;
    }
}