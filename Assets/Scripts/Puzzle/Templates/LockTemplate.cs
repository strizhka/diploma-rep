using UnityEngine;

[CreateAssetMenu(fileName = "T_Lock", menuName = "Puzzles/Templates/Lock")]
public class LockTemplate : PuzzleTemplate
{
    public override void Execute(InteractableObject target, string targetState)
    {
        target.SetLocked(true);
        PuzzleDebugOverlay.Log(
            $"[Template:Lock] '{target.ObjectId}' заблокирован",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
