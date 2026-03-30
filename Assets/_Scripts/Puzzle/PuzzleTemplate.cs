using UnityEngine;

public abstract class PuzzleTemplate : ScriptableObject
{
    public abstract void Execute(GameObject target, string targetState);
}
