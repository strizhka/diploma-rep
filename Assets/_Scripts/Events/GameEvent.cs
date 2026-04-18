using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Events/GameEvent (Void)")]
public class GameEvent : ScriptableObject
{
    [SerializeField] private int _eventId;

    private readonly UnityEvent _event = new();

    public int EventId => _eventId;

    public void Raise()
    {
        _event.Invoke();
    }

    public void AddListener(UnityAction listener)
    {
        _event.AddListener(listener);
    }

    public void RemoveListener(UnityAction listener)
    {
        _event.RemoveListener(listener);
    }

    [ContextMenu("Raise Event")]
    private void RaiseInEditor()
    {
        Raise();
    }
}
