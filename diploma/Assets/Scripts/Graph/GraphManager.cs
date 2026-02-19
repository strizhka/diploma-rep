using UnityEngine;

public class GraphManager : MonoBehaviour
{
    [SerializeField] private GameEvent onDoorOpened;

    private void CheckGoal()
    {
        //onDoorOpened?.Raise();
        //Debug.Log("Дверь открыта! Уровень пройден.");
    }

    private void Update()
    {
        CheckGoal();
    }
}