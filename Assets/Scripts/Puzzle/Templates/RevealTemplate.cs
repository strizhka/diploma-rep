using UnityEngine;

[CreateAssetMenu(fileName = "T_Reveal", menuName = "Puzzles/Templates/Reveal")]
public class RevealTemplate : PuzzleTemplate
{
    public override void Execute(InteractableObject target, string targetState)
    {
        target.SetHidden(false);
        PuzzleDebugOverlay.Log(
            $"[Template:Reveal] '{target.ObjectId}' показан",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
