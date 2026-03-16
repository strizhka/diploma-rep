using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleDirector : NetworkBehaviour
{
    [SerializeField] private PuzzleEntry[] _entries;
    
    private EntryRuntime[] _runtimes;

    [Serializable]
    public class PuzzleEntry
    {
        [Header("Шаблон эффекта")]
        [Tooltip("Тип эффекта: Reveal, Hide, SetState, Unlock, Lock...")]
        public PuzzleTemplate Template;

        [Header("Источники (кто триггерит)")]
        [Tooltip("Объекты, чьё состояние проверяется. Для простых загадок — один элемент.")]
        public InteractableObject[] Sources;

        [Tooltip("Требуемые состояния. По одному на каждый Source, в том же порядке.")]
        public string[] TriggerStates;

        [Header("Цель (на кого действует)")]
        public InteractableObject Target;

        [Tooltip("Дополнительный параметр для SetState шаблона")]
        public string TargetState;

        [Header("Настройки")]
        public float Delay;
        public bool OneShot = true;

        [Tooltip("All = все условия выполнены (порядок не важен).\n" +
                 "Simultaneous = все условия в пределах TimeWindow.")]
        public ConditionMode Mode = ConditionMode.All;

        [Tooltip("Окно в секундах для Simultaneous режима")]
        [Min(0.1f)]
        public float TimeWindow = 3f;
    }

    public enum ConditionMode
    {
        All,
        Simultaneous
    }

    private class EntryRuntime
    {
        public bool HasFired;
        public Dictionary<string, (string state, float time)> SourceStates = new();
    }

    public override void OnStartServer()
    {
        _runtimes = new EntryRuntime[_entries.Length];
        for (int i = 0; i < _entries.Length; i++)
            _runtimes[i] = new EntryRuntime();
    }

    [Server]
    public void ReportInteraction(string objectId, string newState)
    {
        PuzzleDebugOverlay.Log($"[Director] получено: {objectId} = {newState}");

        if (_runtimes == null) return;

        float now = Time.time;

        for (int i = 0; i < _entries.Length; i++)
        {
            var entry = _entries[i];
            var runtime = _runtimes[i];

            if (runtime.HasFired && entry.OneShot) continue;
            if (entry.Template == null || entry.Target == null) continue;
            if (entry.Sources == null || entry.Sources.Length == 0) continue;
            
            bool isRelevant = false;
            for (int s = 0; s < entry.Sources.Length; s++)
            {
                if (entry.Sources[s] != null && entry.Sources[s].ObjectId == objectId)
                {
                    isRelevant = true;
                    break;
                }
            }

            if (!isRelevant) continue;
            
            runtime.SourceStates[objectId] = (newState, now);
            
            if (EvaluateEntry(entry, runtime, now))
            {
                runtime.HasFired = true;
                ExecuteEntry(entry);
            }
        }
    }

    private bool EvaluateEntry(PuzzleEntry entry, EntryRuntime runtime, float now)
    {
        for (int s = 0; s < entry.Sources.Length; s++)
        {
            if (entry.Sources[s] == null) return false;

            string sourceId = entry.Sources[s].ObjectId;
            
            string requiredState = s < entry.TriggerStates.Length
                ? entry.TriggerStates[s]
                : "";

            if (string.IsNullOrEmpty(requiredState)) return false;
            
            if (!runtime.SourceStates.TryGetValue(sourceId, out var recorded))
                return false;

            if (recorded.state != requiredState)
                return false;
        }
        
        if (entry.Mode == ConditionMode.Simultaneous && entry.Sources.Length > 1)
        {
            float earliest = float.MaxValue;
            float latest = float.MinValue;

            for (int s = 0; s < entry.Sources.Length; s++)
            {
                string sourceId = entry.Sources[s].ObjectId;
                float t = runtime.SourceStates[sourceId].time;
                if (t < earliest) earliest = t;
                if (t > latest) latest = t;
            }

            if (latest - earliest > entry.TimeWindow)
                return false;
        }

        return true;
    }

    private void ExecuteEntry(PuzzleEntry entry)
    {
        if (entry.Delay <= 0f)
            DoExecute(entry);
        else
            StartCoroutine(DelayedExecute(entry));
    }

    private IEnumerator DelayedExecute(PuzzleEntry entry)
    {
        yield return new WaitForSeconds(entry.Delay);
        DoExecute(entry);
    }

    [Server]
    private void DoExecute(PuzzleEntry entry)
    {
        if (entry.Target == null)
        {
            Debug.LogWarning("[Director] Target == null при выполнении эффекта.");
            return;
        }

        entry.Template.Execute(entry.Target, entry.TargetState);

        PuzzleDebugOverlay.Log(
            $"[Director] Эффект '{entry.Template.name}' → '{entry.Target.ObjectId}'",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    public struct EntryDebugInfo
    {
        public string TemplateName;
        public string[] SourceIds;
        public string[] RequiredStates;
        public string[] CurrentStates;
        public bool[] ConditionsMet;
        public string TargetId;
        public bool HasFired;
    }

    public IEnumerable<EntryDebugInfo> GetDebugInfo()
    {
        if (_entries == null || _runtimes == null) yield break;

        for (int i = 0; i < _entries.Length; i++)
        {
            var entry = _entries[i];
            var runtime = _runtimes[i];

            int count = entry.Sources?.Length ?? 0;
            var info = new EntryDebugInfo
            {
                TemplateName = entry.Template != null ? entry.Template.name : "null",
                SourceIds = new string[count],
                RequiredStates = new string[count],
                CurrentStates = new string[count],
                ConditionsMet = new bool[count],
                TargetId = entry.Target != null ? entry.Target.ObjectId : "null",
                HasFired = runtime.HasFired
            };

            for (int s = 0; s < count; s++)
            {
                info.SourceIds[s] = entry.Sources[s] != null
                    ? entry.Sources[s].ObjectId : "null";
                info.RequiredStates[s] = s < entry.TriggerStates.Length
                    ? entry.TriggerStates[s] : "—";

                if (runtime.SourceStates.TryGetValue(info.SourceIds[s], out var rec))
                {
                    info.CurrentStates[s] = rec.state;
                    info.ConditionsMet[s] = rec.state == info.RequiredStates[s];
                }
                else
                {
                    info.CurrentStates[s] = "—";
                    info.ConditionsMet[s] = false;
                }
            }

            yield return info;
        }
    }
}
