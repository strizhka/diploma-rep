using UnityEngine;

[CreateAssetMenu(fileName = "T_Unlock", menuName = "Puzzles/Templates/Unlock")]
public class UnlockTemplate : PuzzleTemplate
{
    public override void Execute(InteractableObject target, string targetState)
    {
        target.SetLocked(false);
        PuzzleDebugOverlay.Log(
            $"[Template:Unlock] '{target.ObjectId}' разблокирован",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
