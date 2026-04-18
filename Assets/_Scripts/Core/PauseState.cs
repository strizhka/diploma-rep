using Mirror;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PauseState : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Событийная шина")]
    [SerializeField] private GameEvent _onPausedEvent;
    [SerializeField] private GameEvent _onResumedEvent;

    [Header("Текст")]
    [SerializeField] private string _pausedText = "Пауза";

    [Header("Дедупликация")]
    [Tooltip("Серверный debounce — защита от двойных Cmd за счёт двойного Input-события.")]
    [SerializeField] private float _serverDebounceSeconds = 0.25f;

    [SyncVar(hook = nameof(OnPauseChanged))]
    private bool _isPaused;

    // Только серверный timestamp. На клиенте никогда не трогается.
    private float _lastServerToggleTime = -999f;

    private static PauseState _instance;
    public static PauseState Instance => _instance;
    public static bool IsPaused => _instance != null && _instance._isPaused;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogError(
                $"[PauseState] В сцене несколько PauseState! Другой на '{_instance.gameObject.name}'. " +
                $"Оставь ровно один.");
            return;
        }

        _instance = this;

        if (_pausePanel != null)
            _pausePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
        Time.timeScale = 1f;
    }

    // ──────────────────────── ПУБЛИЧНОЕ API ────────────────────────

    /// <summary>Вызывается из PauseManager на Player-префабе при нажатии Esc.</summary>
    public void TogglePause()
    {
        CmdTogglePause();
    }

    /// <summary>Кнопка «Продолжить».</summary>
    public void OnClickResume()
    {
        if (!_isPaused) return;
        CmdTogglePause();
    }

    /// <summary>Кнопка «Отключиться». Корректно завершает сессию.</summary>
    public void OnClickDisconnect()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[Pause] Выход по кнопке");

        if (NetworkManager.singleton == null)
        {
            Debug.LogError("[Pause] NetworkManager.singleton == null.");
            return;
        }

        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkServer.active)
            NetworkManager.singleton.StopServer();
        else if (NetworkClient.isConnected || NetworkClient.isConnecting)
            NetworkManager.singleton.StopClient();
    }

    // ──────────────────────── CMD ────────────────────────

    [Command(requiresAuthority = false)]
    private void CmdTogglePause()
    {
        float now = Time.realtimeSinceStartup;
        if (now - _lastServerToggleTime < _serverDebounceSeconds)
        {
            Debug.Log($"[Pause] Дубликат CmdTogglePause за {now - _lastServerToggleTime:F3}s — игнорирую.");
            return;
        }
        _lastServerToggleTime = now;

        _isPaused = !_isPaused;

        var e = _isPaused ? _onPausedEvent : _onResumedEvent;
        if (e != null) NetworkEventBridge.Broadcast(e);

        Debug.Log(_isPaused ? "[Pause] Пауза включена" : "[Pause] Пауза снята");
    }

    // ──────────────────────── SYNC HOOK ────────────────────────

    private void OnPauseChanged(bool oldValue, bool newValue)
    {
        if (oldValue == newValue) return;

        Debug.Log($"[Pause:Hook] {oldValue} → {newValue}");

        if (newValue)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        UpdateUI(newValue);
    }

    private void UpdateUI(bool paused)
    {
        if (_pausePanel != null)
            _pausePanel.SetActive(paused);

        if (paused && _statusText != null)
            _statusText.text = _pausedText;
    }
}