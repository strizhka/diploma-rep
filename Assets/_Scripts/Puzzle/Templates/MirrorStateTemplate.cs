using UnityEngine;

/// <summary>
/// Зеркалирует состояние источника на цель.
/// Картина стала "upwards" → шкаф тоже "upwards".
///
/// НАСТРОЙКА В PUZZLEDIRECTOR:
/// - Template = T_MirrorState
/// - TriggerStates = ["*"]     ← срабатывает на ЛЮБОЕ состояние источника
/// - TargetState = (ПУСТО)     ← берётся состояние источника автоматически
/// - OneShot = false           ← работает каждый раз
/// </summary>
[CreateAssetMenu(fileName = "T_MirrorState", menuName = "Puzzles/Templates/MirrorState")]
public class MirrorStateTemplate : PuzzleTemplate
{
    public override void Execute(GameObject target, string targetState)
    {
        var interactable = target.GetComponent<InteractableObject>();
        if (interactable == null)
        {
            Debug.LogWarning($"[Template:MirrorState] InteractableObject не найден на '{target.name}'");
            return;
        }

        interactable.ApplyState(targetState);
        PuzzleDebugOverlay.Log(
            $"[Template:MirrorState] '{target.name}' → '{targetState}'",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }
}
