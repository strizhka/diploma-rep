using UnityEngine;

[CreateAssetMenu(fileName = "T_Unlock", menuName = "Puzzles/Templates/Unlock")]
public class UnlockTemplate : PuzzleTemplate
{
    public override void Execute(GameObject target, string targetState)
    {
        var interactable = target.GetComponent<InteractableObject>();
        if (interactable == null)
        {
            Debug.LogWarning($"[Template:Unlock] InteractableObject не найден на '{target.name}'");
            return;
        }

        interactable.SetLocked(false);
        PuzzleDebugOverlay.Log(
            $"[Template:Unlock] '{target.name}' разблокирован",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
