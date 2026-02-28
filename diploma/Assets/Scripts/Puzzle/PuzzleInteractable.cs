using Mirror;
using UnityEngine;

public class PuzzleInteractable : NetworkBehaviour
{
    [Header("Уникальный ID (обязательно!)")]
    [SerializeField] private string puzzleId = "RoomA_LeverRed";

    [Header("Значение, которое установится при взаимодействии")]
    [SerializeField] private int interactValue = 1;

    [Header("Локальное событие (звук, анимация, тряска)")]
    [SerializeField] private GameEvent localInteractEvent;

    private void OnMouseDown()
    {
        if (!isClient) return;

        localInteractEvent?.Raise();
        PuzzleManager.Instance.CmdSetState(puzzleId, interactValue);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.PuzzleStates.OnChange += OnStateChanged;   // ← НОВОЕ
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.PuzzleStates.OnChange -= OnStateChanged;
    }

    // Реакция на изменение состояния (анимация, звук, открытие двери и т.д.)
    private void OnStateChanged(SyncDictionary<string, PuzzleState>.Operation op, string key, PuzzleState oldValue)
    {
        if (key != puzzleId) return;

        if (!PuzzleManager.Instance.PuzzleStates.TryGetValue(puzzleId, out PuzzleState current))
            return;

        Debug.Log($"[Puzzle] {puzzleId} → {current.Value} (возраст {NetworkTime.time - current.LastChangeServerTime:F2} сек)");

        // ← Здесь твои анимации / звук / эффекты
        // Пример:
        // GetComponent<Animator>().SetInteger("State", current.Value);
        // или gameObject.SetActive(current.Value == 1);
    }
}