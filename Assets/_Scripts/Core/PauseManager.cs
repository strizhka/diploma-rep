using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseManager : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Событийная шина")]
    [Tooltip("Void-событие: пауза включена. Добавь в NetworkGameEventRegistry.")]
    [SerializeField] private GameEvent _onPausedEvent;

    [Tooltip("Void-событие: пауза снята.")]
    [SerializeField] private GameEvent _onResumedEvent;

    [Header("Тексты")]
    [SerializeField] private string _youPausedText = "Вы поставили паузу\n\n[Esc] Продолжить";
    [SerializeField] private string _otherPausedText = "Другой игрок поставил паузу\n\nОжидайте...";

    [SyncVar(hook = nameof(OnPauseChanged))]
    private bool _isPaused;

    [SyncVar]
    private int _pauseOwnerConnId = -1;

    private bool _iAmOwner;

    private static PauseManager _instance;
    public static PauseManager Instance => _instance;
    public static bool IsPaused => _instance != null && _instance._isPaused;

    private void Awake()
    {
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

    public void OnPauseInput(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (!_isPaused)
        {
            CmdPause();
        }
        else if (_iAmOwner)
        {
            CmdResume();
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdPause(NetworkConnectionToClient sender = null)
    {
        if (_isPaused) return;
        if (sender == null) return;

        _isPaused = true;
        _pauseOwnerConnId = sender.connectionId;

        if (_onPausedEvent != null)
            NetworkEventBridge.Broadcast(_onPausedEvent);

        PuzzleDebugOverlay.Log(
            $"[Pause] Пауза от игрока {sender.connectionId}",
            PuzzleDebugOverlay.DebugLevel.Warning);
    }

    [Command(requiresAuthority = false)]
    private void CmdResume(NetworkConnectionToClient sender = null)
    {
        if (!_isPaused) return;
        if (sender == null) return;

        if (sender.connectionId != _pauseOwnerConnId)
        {
            PuzzleDebugOverlay.Log(
                $"[Pause] Игрок {sender.connectionId} не может снять чужую паузу",
                PuzzleDebugOverlay.DebugLevel.Warning);
            return;
        }

        _isPaused = false;
        _pauseOwnerConnId = -1;

        if (_onResumedEvent != null)
            NetworkEventBridge.Broadcast(_onResumedEvent);

        PuzzleDebugOverlay.Log("[Pause] Пауза снята",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    private void OnPauseChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _iAmOwner = IsLocalOwner();
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _iAmOwner = false;
        }

        UpdateUI(newValue);
    }


    private void UpdateUI(bool paused)
    {
        if (_pausePanel != null)
            _pausePanel.SetActive(paused);

        if (!paused || _statusText == null) return;

        _statusText.text = _iAmOwner ? _youPausedText : _otherPausedText;
    }


    private bool IsLocalOwner()
    {
        if (NetworkClient.connection == null) return false;
        return 0 == _pauseOwnerConnId;
    }
}
