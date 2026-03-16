using UnityEngine;

public abstract class PuzzleTemplate : ScriptableObject
{
    public abstract void Execute(InteractableObject target, string targetState);
}
