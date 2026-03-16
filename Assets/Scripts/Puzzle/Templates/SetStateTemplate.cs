using UnityEngine;

[CreateAssetMenu(fileName = "T_SetState", menuName = "Puzzles/Templates/SetState")]
public class SetStateTemplate : PuzzleTemplate
{
    public override void Execute(InteractableObject target, string targetState)
    {
        if (string.IsNullOrEmpty(targetState))
        {
            Debug.LogWarning($"[Template:SetState] targetState пуст для '{target.ObjectId}'");
            return;
        }

        target.ApplyState(targetState);
        PuzzleDebugOverlay.Log(
            $"[Template:SetState] '{target.ObjectId}' → '{targetState}'",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
