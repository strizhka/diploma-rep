using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Events/GameEvent (Void)")]
public class GameEvent : ScriptableObject
{
    private UnityEvent _event = new UnityEvent();

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
