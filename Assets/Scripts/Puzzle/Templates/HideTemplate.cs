using UnityEngine;

[CreateAssetMenu(fileName = "T_Hide", menuName = "Puzzles/Templates/Hide")]
public class HideTemplate : PuzzleTemplate
{
    public override void Execute(InteractableObject target, string targetState)
    {
        target.SetHidden(true);
        PuzzleDebugOverlay.Log(
            $"[Template:Hide] '{target.ObjectId}' скрыт",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
