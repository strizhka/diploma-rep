using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleManager : NetworkBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    public readonly SyncDictionary<string, PuzzleState> PuzzleStates = new();

    [Header("Все правила пазлов (перетащи ScriptableObject'ы сюда)")]
    [SerializeField] private List<PuzzleRule> rules = new List<PuzzleRule>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        PuzzleStates.Clear();
        PuzzleStates.OnChange += OnAnyStateChanged;   // ← НОВОЕ
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        PuzzleStates.OnChange -= OnAnyStateChanged;
    }

    // Вызывается на сервере при ЛЮБОМ изменении словаря
    private void OnAnyStateChanged(SyncDictionary<string, PuzzleState>.Operation op, string key, PuzzleState oldValue)
    {
        if (!isServer) return;
        CheckAllRules(key);
    }

    [Command(requiresAuthority = false)]
    public void CmdSetState(string puzzleId, int newValue)
    {
        PuzzleState state = PuzzleStates.TryGetValue(puzzleId, out var s) 
            ? s 
            : new PuzzleState(newValue);

        state.Value = newValue;
        state.LastChangeServerTime = NetworkTime.time;
        PuzzleStates[puzzleId] = state;
    }

    private void CheckAllRules(string changedId)
    {
        foreach (var rule in rules)
        {
            if (rule.Check(PuzzleStates))
            {
                NetworkGameEventDispatcher.Raise(rule.successEvent);
            }
        }
    }

    public static int GetState(string puzzleId, int defaultValue = 0)
    {
        if (Instance == null || !Instance.PuzzleStates.TryGetValue(puzzleId, out var state))
            return defaultValue;
        return state.Value;
    }
}