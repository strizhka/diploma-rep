using UnityEngine;

[CreateAssetMenu(fileName = "T_Reveal", menuName = "Puzzles/Templates/Reveal")]
public class RevealTemplate : PuzzleTemplate
{
    public override void Execute(GameObject target, string targetState)
    {
        var interactable = target.GetComponent<InteractableObject>();
        if (interactable != null)
        {
            interactable.SetHidden(false);
        }
        else
        {
            target.SetActive(true);
        }

        PuzzleDebugOverlay.Log(
            $"[Template:Reveal] '{target.name}' показан",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
