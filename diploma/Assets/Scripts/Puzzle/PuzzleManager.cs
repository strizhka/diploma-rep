using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleManager : NetworkBehaviour
{
    [SerializeField] private PuzzleDefinition[] _puzzles;

    // Рантаймовое состояние пазлов (только на сервере)
    private class PuzzleRuntimeState
    {
        public PuzzleDefinition Definition;
        public bool IsCompleted;
        // objectId → (currentState, timestamp последнего изменения)
        public Dictionary<string, (string state, float time)> ConditionStates = new();
    }

    private List<PuzzleRuntimeState> _runtimeStates;

    public override void OnStartServer()
    {
        _runtimeStates = new List<PuzzleRuntimeState>();

        foreach (var def in _puzzles)
        {
            _runtimeStates.Add(new PuzzleRuntimeState { Definition = def });
        }
    }

    /// <summary>
    /// Вызывается PuzzleNetworkBridge'ем когда игрок что-то сделал.
    /// </summary>
    [Server]
    public void ReportInteraction(string objectId, string newState)
    {
        PuzzleDebugOverlay.Log($"[Server] получено: {objectId} = {newState}");

        float now = Time.time;

        foreach (var runtime in _runtimeStates)
        {
            if (runtime.IsCompleted && runtime.Definition.IsOneShot) continue;

            // Обновляем запись о состоянии объекта для этого пазла
            runtime.ConditionStates[objectId] = (newState, now);

            if (EvaluateConditions(runtime, now))
            {
                runtime.IsCompleted = true;
                ApplyEffects(runtime.Definition.Effects);
            }
        }
    }

    private bool EvaluateConditions(PuzzleRuntimeState runtime, float now)
    {
        var def = runtime.Definition;

        foreach (var condition in def.Conditions)
        {
            if (!runtime.ConditionStates.TryGetValue(condition.ObjectId, out var entry))
                return false;

            if (entry.state != condition.RequiredState)
                return false;
        }

        // Все условия выполнены по состоянию.
        // Для Simultaneous проверяем временное окно.
        if (def.Mode == ConditionMode.Simultaneous)
        {
            float earliest = float.MaxValue;
            float latest = float.MinValue;

            foreach (var condition in def.Conditions)
            {
                float t = runtime.ConditionStates[condition.ObjectId].time;
                if (t < earliest) earliest = t;
                if (t > latest) latest = t;
            }

            if (latest - earliest > def.SimultaneousWindow)
                return false;
        }

        return true;
    }

    private void ApplyEffects(PuzzleEffect[] effects)
    {
        foreach (var effect in effects)
        {
            if (effect.Delay <= 0f)
                ExecuteEffect(effect);
            else
                StartCoroutine(DelayedEffect(effect));
        }
    }

    private IEnumerator DelayedEffect(PuzzleEffect effect)
    {
        yield return new WaitForSeconds(effect.Delay);
        ExecuteEffect(effect);
    }

    [Server]
    private void ExecuteEffect(PuzzleEffect effect)
    {
        var target = InteractableObjectRegistry.Get(effect.TargetObjectId);
        if (target == null)
        {
            PuzzleDebugOverlay.Log($"[Effect] ОШИБКА: '{effect.TargetObjectId}' не найден!", PuzzleDebugOverlay.DebugLevel.Error);
            return;
        }
        target.ApplyState(effect.NewState);
        PuzzleDebugOverlay.Log($"[Effect] '{effect.TargetObjectId}' → '{effect.NewState}'", PuzzleDebugOverlay.DebugLevel.Ok);
    }

    public struct PuzzleDebugInfo
    {
        public string PuzzleId;
        public bool IsCompleted;
        public List<ConditionDebugInfo> Conditions;
    }

    public struct ConditionDebugInfo
    {
        public string ObjectId;
        public string RequiredState;
        public string CurrentState;
        public bool IsMet;
    }

    public IEnumerable<PuzzleDebugInfo> GetDebugInfo()
    {
        if (_runtimeStates == null) yield break;

        foreach (var runtime in _runtimeStates)
        {
            var condInfos = new List<ConditionDebugInfo>();

            foreach (var cond in runtime.Definition.Conditions)
            {
                runtime.ConditionStates.TryGetValue(cond.ObjectId, out var entry);
                condInfos.Add(new ConditionDebugInfo
                {
                    ObjectId = cond.ObjectId,
                    RequiredState = cond.RequiredState,
                    CurrentState = entry.state ?? "—",
                    IsMet = entry.state == cond.RequiredState
                });
            }

            yield return new PuzzleDebugInfo
            {
                PuzzleId = runtime.Definition.PuzzleId,
                IsCompleted = runtime.IsCompleted,
                Conditions = condInfos
            };
        }
    }
}