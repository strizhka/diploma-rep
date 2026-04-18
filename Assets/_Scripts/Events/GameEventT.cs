using UnityEngine;
using UnityEngine.Events;

public abstract class GameEventT<T> : ScriptableObject
{
    private readonly UnityEvent<T> _event = new();

    public void Raise(T value)
    {
        _event.Invoke(value);
    }

    public void AddListener(UnityAction<T> listener)
    {
        _event.AddListener(listener);
    }

    public void RemoveListener(UnityAction<T> listener)
    {
        _event.RemoveListener(listener);
    }
}