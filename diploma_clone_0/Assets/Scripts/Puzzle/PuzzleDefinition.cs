using UnityEngine;

[CreateAssetMenu(fileName = "NewPuzzle", menuName = "Puzzles/PuzzleDefinition")]
public class PuzzleDefinition : ScriptableObject
{
    [Header("Идентификатор")]
    [Tooltip("Уникальная строка. Используется только для дебага")]
    public string PuzzleId;

    [Header("Настройки срабатывания")]
    [Tooltip("Деактивировать пазл после первого выполнения")]
    public bool IsOneShot = true;

    [Tooltip("All = все условия выполнены. Simultaneous = все в рамках timeWindow")]
    public ConditionMode Mode = ConditionMode.All;

    [Tooltip("Окно в секундах для режима Simultaneous")]
    [Min(0.1f)]
    public float SimultaneousWindow = 3f;

    [Header("Условия (И-логика)")]
    public PuzzleCondition[] Conditions;

    [Header("Эффекты при выполнении")]
    public PuzzleEffect[] Effects;
}