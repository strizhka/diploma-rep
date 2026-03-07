using UnityEngine;

[System.Serializable]
public class PuzzleCondition
{
    [Tooltip("ID объекта Ч должен совпадать с objectId на компоненте InteractableObject")]
    public string ObjectId;

    [Tooltip("—осто€ние, в котором должен быть объект (например: 'turned', 'open', 'pressed')")]
    public string RequiredState;
}