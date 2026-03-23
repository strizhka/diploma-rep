using UnityEngine;

[CreateAssetMenu(fileName = "T_Hide", menuName = "Puzzles/Templates/Hide")]
public class HideTemplate : PuzzleTemplate
{
    public override void Execute(GameObject target, string targetState)
    {
        var interactable = target.GetComponent<InteractableObject>();
        if (interactable != null)
        {
            interactable.SetHidden(true);
        }
        else
        {
            target.SetActive(false);
        }

        PuzzleDebugOverlay.Log(
            $"[Template:Hide] '{target.name}' скрыт",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
