using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
    [SerializeField] private GameEvent _event;
    [SerializeField] private UnityEvent _response;

    private void OnEnable()
    {
        if (_event != null)
            _event.AddListener(OnEventRaised);
    }

    private void OnDisable()
    {
        if (_event != null)
            _event.RemoveListener(OnEventRaised);
    }

    private void OnEventRaised()
    {
        _response?.Invoke();
    }
}