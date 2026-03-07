using UnityEngine;

public class TestRaiser : MonoBehaviour
{
    [SerializeField] private GameEvent onLevelLoaded;

    private void Start()
    {
        onLevelLoaded?.Raise();
    }
}