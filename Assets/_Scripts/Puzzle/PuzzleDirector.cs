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
        [Tooltip("Название загадки для удобства (не влияет на логику)")]
        public string Name;

        [Header("Шаблон эффекта")]
        public PuzzleTemplate Template;

        [Header("Источники (кто триггерит)")]
        public InteractableObject[] Sources;
        public string[] TriggerStates;

        [Header("Цели (на кого действует)")]
        [Tooltip("Один или несколько объектов. Эффект применяется ко всем.")]
        public GameObject[] Targets;

        [Tooltip("Дополнительный параметр для SetState")]
        public string TargetState;

        [Header("Настройки")]
        public float Delay;
        public bool OneShot = true;

        public ConditionMode Mode = ConditionMode.All;

        [Min(0.1f)]
        public float TimeWindow = 3f;

        [Header("Звук (опционально)")]
        public AudioClip Sound;

        [Range(0f, 1f)]
        public float SoundVolume = 1f;
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

    // ──────────────────────── LIFECYCLE ────────────────────────

    public override void OnStartServer()
    {
        _runtimes = new EntryRuntime[_entries.Length];
        for (int i = 0; i < _entries.Length; i++)
            _runtimes[i] = new EntryRuntime();
    }

    // ──────────────────────── ОСНОВНОЙ МЕТОД ────────────────────────

    [Server]
    public void ReportInteraction(string objectId, string newState)
    {

        if (_runtimes == null) return;

        float now = Time.time;

        for (int i = 0; i < _entries.Length; i++)
        {
            var entry = _entries[i];
            var runtime = _runtimes[i];

            if (runtime.HasFired && entry.OneShot) continue;
            if (entry.Template == null) continue;
            if (entry.Targets == null || entry.Targets.Length == 0) continue;
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
                ExecuteEntry(entry, i, newState);
            }
        }
    }

    // ──────────────────────── ПРОВЕРКА УСЛОВИЙ ────────────────────────

    private bool EvaluateEntry(PuzzleEntry entry, EntryRuntime runtime, float now)
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

            // "*" = любое состояние (для MirrorState)
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

    // ──────────────────────── ВЫПОЛНЕНИЕ ЭФФЕКТА ────────────────────────

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
        // Если TargetState пуст — используем состояние источника (для MirrorState)
        string effectState = string.IsNullOrEmpty(entry.TargetState)
            ? sourceState
            : entry.TargetState;
        // Применяем эффект к КАЖДОЙ цели
        for (int t = 0; t < entry.Targets.Length; t++)
        {
            var target = entry.Targets[t];
            if (target == null) continue;

            entry.Template.Execute(target, effectState);

            // Синхронизация для объектов БЕЗ InteractableObject
            if (target.GetComponent<InteractableObject>() == null)
            {
                bool isActive = target.activeSelf;
                RpcSyncTargetActive(entryIndex, t, isActive);
            }
        }

        // Звук — проигрываем один раз в позиции первой цели
        if (entry.Sound != null)
            RpcPlaySound(entryIndex);
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

        // Позиция звука — первая цель
        var firstTarget = entry.Targets != null && entry.Targets.Length > 0
            ? entry.Targets[0] : null;

        if (firstTarget != null)
            AudioSource.PlayClipAtPoint(entry.Sound, firstTarget.transform.position, entry.SoundVolume);
    }

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

            // Собираем имена целей
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