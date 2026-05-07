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
        [Tooltip("Название загадки")]
        public string Name;

        [Header("Шаблон эффекта")]
        public PuzzleTemplate Template;

        [Header("Источник триггера")]
        public InteractableObject[] Sources;
        public string[] TriggerStates;

        [Header("Условия")]
        public StateCheck[] Conditions;

        [Header("На кого действует")]
        public GameObject[] Targets;

        [Tooltip("Итоговое состояние цели")]
        public string TargetState;

        [Header("Параметры")]
        public float Delay;
        public bool OneShot = true;

        public ConditionMode Mode = ConditionMode.All;

        [Min(0.1f)]
        public float TimeWindow = 3f;

        [Header("При неудаче")]
        [Tooltip("Звук при провале проверки условий")]
        public AudioClip FailSound;

        [Tooltip("Сбросить источник в начальное состояние при провале")]
        public bool ResetSourcesOnFail;

        [Tooltip("Состояние для сброса источника")]
        public string ResetState = "default";

        [Header("Звук при срабатывании")]
        public AudioClip Sound;

        [Range(0f, 1f)]
        public float SoundVolume = 1f;
    }

    [Serializable]
    public class StateCheck
    {
        public InteractableObject Object;
        public string RequiredState;
    }

    public enum ConditionMode
    {
        All,
        Simultaneous,
        Mirror
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
            if (entry.Template == null) continue;
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

            // Mirror mode
            if (entry.Mode == ConditionMode.Mirror)
            {
                for (int s = 0; s < entry.Sources.Length; s++)
                {
                    if (entry.Sources[s] != null && entry.Sources[s].ObjectId == objectId)
                    {
                        if (s < entry.Targets.Length && entry.Targets[s] != null)
                        {
                            entry.Template.Execute(entry.Targets[s], newState);

                            if (entry.Targets[s].GetComponent<InteractableObject>() == null)
                                RpcSyncTargetActive(i, s, entry.Targets[s].activeSelf);

                            if (entry.Sound != null)
                                RpcPlaySound(i);
                        }
                        break;
                    }
                }
                continue;
            }

            runtime.SourceStates[objectId] = (newState, now);

            if (EvaluateSources(entry, runtime, now))
            {
                if (CheckConditions(entry))
                {
                    runtime.HasFired = true;
                    ExecuteEntry(entry, i, newState);
                }
                else
                {
                    OnConditionsFailed(entry, i);
                }
            }
        }
    }

    private bool EvaluateSources(PuzzleEntry entry, EntryRuntime runtime, float now)
    {
        for (int s = 0; s < entry.Sources.Length; s++)
        {
            if (entry.Sources[s] == null) return false;

            string sourceId = entry.Sources[s].ObjectId;
            string requiredState = s < entry.TriggerStates.Length
                ? entry.TriggerStates[s] : "";

            if (string.IsNullOrEmpty(requiredState)) return false;

            if (!runtime.SourceStates.TryGetValue(sourceId, out var recorded))
                return false;

            if (requiredState != "*" && recorded.state != requiredState)
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

    [Server]
    private bool CheckConditions(PuzzleEntry entry)
    {
        if (entry.Conditions == null || entry.Conditions.Length == 0)
            return true;

        foreach (var check in entry.Conditions)
        {
            if (check.Object == null) return false;

            string current = check.Object.CurrentState;
            if (string.IsNullOrEmpty(current))
                current = "default";

            PuzzleDebugOverlay.Log(
                $"  [Condition] {check.Object.ObjectId} = '{current}', нужно '{check.RequiredState}'");

            if (current != check.RequiredState)
                return false;
        }

        return true;
    }

    [Server]
    private void OnConditionsFailed(PuzzleEntry entry, int entryIndex)
    {
        PuzzleDebugOverlay.Log(
            $"[Director] '{entry.Name}': условия НЕ выполнены",
            PuzzleDebugOverlay.DebugLevel.Warning);

        if (entry.FailSound != null)
            RpcPlayFailSound(entryIndex);

        if (entry.ResetSourcesOnFail)
        {
            foreach (var source in entry.Sources)
            {
                if (source != null)
                    source.ApplyState(entry.ResetState);
            }
        }
    }

    private void ExecuteEntry(PuzzleEntry entry, int entryIndex, string sourceState)
    {
        if (entry.Delay <= 0f)
            DoExecute(entry, entryIndex, sourceState);
        else
            StartCoroutine(DelayedExecute(entry, entryIndex, sourceState));
    }

    private IEnumerator DelayedExecute(PuzzleEntry entry, int entryIndex, string sourceState)
    {
        yield return new WaitForSeconds(entry.Delay);
        DoExecute(entry, entryIndex, sourceState);
    }

    [Server]
    private void DoExecute(PuzzleEntry entry, int entryIndex, string sourceState)
    {
        if (entry.Targets == null || entry.Targets.Length == 0) return;

        string effectState = string.IsNullOrEmpty(entry.TargetState)
            ? sourceState
            : entry.TargetState;

        for (int t = 0; t < entry.Targets.Length; t++)
        {
            var target = entry.Targets[t];
            if (target == null) continue;

            entry.Template.Execute(target, effectState);

            if (target.GetComponent<InteractableObject>() == null)
                RpcSyncTargetActive(entryIndex, t, target.activeSelf);
        }

        if (entry.Sound != null)
            RpcPlaySound(entryIndex);

        PuzzleDebugOverlay.Log(
            $"[Director] '{entry.Name}' → {entry.Targets.Length} цель(ей)",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    [ClientRpc]
    private void RpcSyncTargetActive(int entryIndex, int targetIndex, bool active)
    {
        if (entryIndex < 0 || entryIndex >= _entries.Length) return;
        var targets = _entries[entryIndex].Targets;
        if (targets == null || targetIndex < 0 || targetIndex >= targets.Length) return;
        var target = targets[targetIndex];
        if (target != null && target.GetComponent<InteractableObject>() == null)
            target.SetActive(active);
    }

    [ClientRpc]
    private void RpcPlaySound(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= _entries.Length) return;
        var entry = _entries[entryIndex];
        if (entry.Sound == null) return;

        var firstTarget = entry.Targets != null && entry.Targets.Length > 0
            ? entry.Targets[0] : null;

        if (firstTarget != null)
            AudioSource.PlayClipAtPoint(entry.Sound, firstTarget.transform.position, entry.SoundVolume);
    }

    [ClientRpc]
    private void RpcPlayFailSound(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= _entries.Length) return;
        var entry = _entries[entryIndex];
        if (entry.FailSound == null) return;
        AudioSource.PlayClipAtPoint(entry.FailSound, transform.position);
    }

    // ──────────────────────── ДЕБАГ ────────────────────────

    public struct EntryDebugInfo
    {
        public string TemplateName;
        public string[] SourceIds;
        public string[] RequiredStates;
        public string[] CurrentStates;
        public bool[] ConditionsMet;
        public string TargetNames;
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

            string targetNames = "—";
            if (entry.Targets != null && entry.Targets.Length > 0)
            {
                var names = new List<string>();
                foreach (var t in entry.Targets)
                    names.Add(t != null ? t.name : "null");
                targetNames = string.Join(", ", names);
            }

            var info = new EntryDebugInfo
            {
                TemplateName = entry.Template != null ? entry.Template.name : "null",
                SourceIds = new string[count],
                RequiredStates = new string[count],
                CurrentStates = new string[count],
                ConditionsMet = new bool[count],
                TargetNames = targetNames,
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