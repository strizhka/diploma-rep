using Mirror;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Показывает состояние всей системы прямо на экране.
/// Работает в редакторе и в билде.
/// Включить/выключить: нажать F3 в рантайме.
/// </summary>
public class PuzzleDebugOverlay : MonoBehaviour
{
    [Header("Настройки отображения")]
    [SerializeField] private bool _showOnStart = true;

    [Header("Визуал")]
    [SerializeField] private int _fontSize = 14;
    [SerializeField] private Color _headerColor = Color.yellow;
    [SerializeField] private Color _okColor = Color.green;
    [SerializeField] private Color _waitColor = Color.white;
    [SerializeField] private Color _errorColor = Color.red;

    private bool _isVisible;

    // Лог последних N событий
    private static readonly Queue<LogEntry> _log = new();
    private const int MaxLogEntries = 20;

    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private bool _stylesInitialized;

    private struct LogEntry
    {
        public string Message;
        public Color Color;
        public float Time;
    }

    private static PuzzleDebugOverlay _instance;

    private void Awake()
    {
        _instance = this;
        _isVisible = _showOnStart;
    }

    // Статический метод — PlayerInput вызывает через SendMessage или UnityEvent
    public static void ToggleStatic()
    {
        if (_instance != null)
            _instance._isVisible = !_instance._isVisible;
    }

    // ───── Статический API — вызывай из любого класса ─────

    public static void Log(string message, DebugLevel level = DebugLevel.Info)
    {
        Color color = level switch
        {
            DebugLevel.Ok => Color.green,
            DebugLevel.Warning => Color.yellow,
            DebugLevel.Error => Color.red,
            _ => Color.white,
        };

        _log.Enqueue(new LogEntry
        {
            Message = $"[{Time.time:F1}s] {message}",
            Color = color,
            Time = Time.time
        });

        // Дублируем в консоль Unity тоже
        switch (level)
        {
            case DebugLevel.Error: Debug.LogError(message); break;
            case DebugLevel.Warning: Debug.LogWarning(message); break;
            default: Debug.Log(message); break;
        }

        while (_log.Count > MaxLogEntries)
            _log.Dequeue();
    }

    public enum DebugLevel { Info, Ok, Warning, Error }

    // ───── Unity ─────

    private void Start()
    {
        _isVisible = _showOnStart;
    }

    public void OnDebugToggle(InputAction.CallbackContext context)
    {
        if (context.performed)
            _isVisible = !_isVisible;
    }

    private void OnGUI()
    {
        if (!_isVisible) return;

        InitStyles();

        float screenW = Screen.width;
        float screenH = Screen.height;

        // ── Левая панель: сетевой статус ──
        DrawNetworkPanel(10, 10, 280, screenH - 20);

        // ── Правая панель: лог событий ──
        DrawLogPanel(screenW - 410, 10, 400, screenH - 20);
    }

    private void DrawNetworkPanel(float x, float y, float w, float h)
    {
        GUILayout.BeginArea(new Rect(x, y, w, h));
        GUILayout.BeginVertical(_boxStyle);

        DrawColoredLabel("═══ NETWORK ═══", _headerColor);

        string role = "Нет подключения";
        if (NetworkServer.active && NetworkClient.isConnected)
            role = "HOST (сервер + клиент)";
        else if (NetworkServer.active)
            role = "SERVER (дедикейтед)";
        else if (NetworkClient.isConnected)
            role = "CLIENT";

        DrawColoredLabel($"Роль: {role}", NetworkClient.isConnected ? _okColor : _errorColor);
        DrawColoredLabel($"Клиентов: {NetworkServer.connections.Count}", _waitColor);
        DrawColoredLabel($"Tick: {Time.frameCount}", _waitColor);

        GUILayout.Space(8);
        DrawColoredLabel("═══ ОБЪЕКТЫ ═══", _headerColor);

        // Показываем все зарегистрированные InteractableObject
        foreach (var kv in InteractableObjectRegistry.GetAll())
        {
            string stateText = $"  [{kv.Key}] → \"{kv.Value.CurrentState}\"";
            DrawColoredLabel(stateText, _waitColor);
        }

        GUILayout.Space(8);
        DrawColoredLabel("═══ ПАЗЛЫ ═══", _headerColor);

        var manager = FindAnyObjectByType<PuzzleManager>();
        if (manager != null)
        {
            foreach (var info in manager.GetDebugInfo())
            {
                Color c = info.IsCompleted ? _okColor : _waitColor;
                DrawColoredLabel($"  {info.PuzzleId}: {(info.IsCompleted ? "✓ ВЫПОЛНЕН" : "ожидание")}", c);

                foreach (var cond in info.Conditions)
                {
                    string mark = cond.IsMet ? "  ✓" : "  ✗";
                    Color cc = cond.IsMet ? _okColor : _errorColor;
                    DrawColoredLabel($"    {mark} {cond.ObjectId} = \"{cond.RequiredState}\" (сейчас: \"{cond.CurrentState}\")", cc);
                }
            }
        }
        else
        {
            DrawColoredLabel("  PuzzleManager не найден!", _errorColor);
        }

        DrawColoredLabel($"\n[F3] — скрыть", _waitColor);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawLogPanel(float x, float y, float w, float h)
    {
        GUILayout.BeginArea(new Rect(x, y, w, h));
        GUILayout.BeginVertical(_boxStyle);

        DrawColoredLabel("═══ СОБЫТИЯ ═══", _headerColor);

        foreach (var entry in _log)
            DrawColoredLabel(entry.Message, entry.Color);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawColoredLabel(string text, Color color)
    {
        _labelStyle.normal.textColor = color;
        GUILayout.Label(text, _labelStyle);
    }

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 8, 8)
        };

        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0, 0, 0, 0.82f));
        bg.Apply();
        _boxStyle.normal.background = bg;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = _fontSize,
            wordWrap = true,
            richText = true
        };

        _stylesInitialized = true;
    }
}