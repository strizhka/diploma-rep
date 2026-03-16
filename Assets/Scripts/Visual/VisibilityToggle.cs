using UnityEngine;

public class VisibilityToggle : MonoBehaviour
{
    [SerializeField] private GameObject _target;

    [Tooltip("При каком состоянии объект становится видимым")]
    [SerializeField] private string _visibleState = "visible";
    
    public void OnStateChanged(string newState)
    {
        if (_target != null)
            _target.SetActive(newState == _visibleState);
    }
}