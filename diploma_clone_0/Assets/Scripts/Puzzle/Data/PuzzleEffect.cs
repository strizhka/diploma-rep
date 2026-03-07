using UnityEngine;

[System.Serializable]
public class PuzzleEffect
{
    [Tooltip("ID целевого объекта, которому мы хотим изменить состояние")]
    public string TargetObjectId;

    [Tooltip("Новое состояние целевого объекта")]
    public string NewState;

    [Tooltip("Задержка в секундах перед применением эффекта. 0 = мгновенно")]
    [Min(0f)]
    public float Delay;
}