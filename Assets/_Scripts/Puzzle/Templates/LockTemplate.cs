using UnityEngine;

[CreateAssetMenu(fileName = "T_Lock", menuName = "Puzzles/Templates/Lock")]
public class LockTemplate : PuzzleTemplate
{
    public override void Execute(GameObject target, string targetState)
    {
        var interactable = target.GetComponent<InteractableObject>();
        if (interactable == null)
        {
            Debug.LogWarning($"[Template:Lock] InteractableObject не найден на '{target.name}'");
            return;
        }

        interactable.SetLocked(true);
        PuzzleDebugOverlay.Log(
            $"[Template:Lock] '{target.name}' заблокирован",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
