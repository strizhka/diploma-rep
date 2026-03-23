using UnityEngine;

[CreateAssetMenu(fileName = "T_SetState", menuName = "Puzzles/Templates/SetState")]
public class SetStateTemplate : PuzzleTemplate
{
    public override void Execute(GameObject target, string targetState)
    {
        var interactable = target.GetComponent<InteractableObject>();
        if (interactable == null)
        {
            Debug.LogWarning($"[Template:SetState] InteractableObject не найден на '{target.name}'");
            return;
        }

        if (string.IsNullOrEmpty(targetState))
        {
            Debug.LogWarning($"[Template:SetState] targetState пуст для '{target.name}'");
            return;
        }

        interactable.ApplyState(targetState);
        PuzzleDebugOverlay.Log(
            $"[Template:SetState] '{target.name}' → '{targetState}'",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
