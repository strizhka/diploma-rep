using System.Collections.Generic;
using Mirror;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPuzzleRule", menuName = "Puzzle/Puzzle Rule")]
public class PuzzleRule : ScriptableObject
{
    [Header("Условия")]
    public List<PuzzleCondition> conditions = new List<PuzzleCondition>();

    [Header("Результат")]
    public GameEvent successEvent;

    [Tooltip("Сработать только один раз?")]
    public bool oneShot = true;

    [HideInInspector] public bool isActive = true; 
    
    public bool Check(SyncDictionary<string, PuzzleState> states)
    {
        if (!isActive) return false;

        foreach (var cond in conditions)
        {
            if (!states.TryGetValue(cond.puzzleId, out var state))
                return false;

            if (state.Value != cond.expectedValue)
                return false;

            if (cond.maxAgeSeconds > 0f)
            {
                double age = NetworkTime.time - state.LastChangeServerTime;
                if (age > cond.maxAgeSeconds)
                    return false;
            }
        }

        if (oneShot) isActive = false;
        return true;
    }
}