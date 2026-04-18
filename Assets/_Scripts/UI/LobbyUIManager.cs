using DG.Tweening;
using Mirror;
using TMPro;
using UnityEngine;

public class LobbyUIManager : Singleton<LobbyUIManager>
{
    [Header("Канвас лобби")]
    [SerializeField] private CanvasGroup _lobbyCanvas;

    [Header("Панели")]
    [SerializeField] private CanvasGroup _mainPanel;
    [SerializeField] private CanvasGroup _hostPanel;
    [SerializeField] private CanvasGroup _joinPanel;
    [SerializeField] private CanvasGroup _loadingPanel;

    [Header("Код приглашения")]
    [SerializeField] private TMP_InputField _inviteCodeInput;

    [Header("Статус")]
    [SerializeField] private TextMeshProUGUI _connectionStatusText;

    [Header("Анимация")]
    [SerializeField] private float _fade = 0.4f;

    private bool _isConnecting;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        _lobbyCanvas.HideInstant();
    }

    public void Show()
    {
        _isConnecting = false;
        _mainPanel.ShowInstant();
        _hostPanel.HideInstant();
        _joinPanel.HideInstant();
        _loadingPanel.HideInstant();
        _lobbyCanvas.SetAlpha(0f);
        _lobbyCanvas.FadeIn(_fade);
        SetStatus("");
    }

    public void OnClickHost() => _mainPanel.SwitchTo(_hostPanel, _fade);
    public void OnClickJoin() => _mainPanel.SwitchTo(_joinPanel, _fade);

    public void OnClickBack()
    {
        if (_isConnecting)
        {
            CancelConnection();
            return;
        }

        _hostPanel.FadeOut(_fade);
        _joinPanel.FadeOut(_fade);
        _loadingPanel.FadeOut(_fade);
        _mainPanel.FadeIn(_fade);
        SetStatus("");
    }

    public void OnClickBackToMenu()
    {
        if (_isConnecting)
            CancelConnection();

        _mainPanel.FadeOut(_fade, () =>
            _lobbyCanvas.FadeOut(_fade, () =>
            {
                _lobbyCanvas.gameObject.SetActive(false);
                MainMenuUI.Instance.Show();
            }));
    }

    public void OnClickStartHost()
    {
        SetStatus("Создание комнаты...");
        _isConnecting = true;
        _hostPanel.SwitchTo(_loadingPanel, _fade);

        EdgegapRelayService.CreateRoom(
            onCodeReady: code =>
            {
                if (!_isConnecting) return;

                //var nm = NetworkManager.singleton as EscapeRoomNetworkManager;
                //if (nm != null) nm.CurrentRoomCode = code;

                _lobbyCanvas.FadeOut(_fade, () =>
                {
                    _lobbyCanvas.gameObject.SetActive(false);
                    WaitingRoomUI.ShowWithCode(code);
                    NetworkManager.singleton.StartHost();
                });
                _isConnecting = false;
            },
            onError: err =>
            {
                SetStatus($"Ошибка: {err}");
                _isConnecting = false;
                _loadingPanel.SwitchTo(_hostPanel, _fade);
            }
        );
    }

    public void OnClickJoinWithCode()
    {
        string code = _inviteCodeInput.text.Trim();

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Введи код приглашения.");
            return;
        }

        SetStatus($"Подключение...");
        _isConnecting = true;
        _joinPanel.interactable = false;

        EdgegapRelayService.JoinRoom(
            code: code,
            onReady: () =>
            {
                if (!_isConnecting) return;

                _lobbyCanvas.FadeOut(_fade, () =>
                {
                    _lobbyCanvas.gameObject.SetActive(false);
                    WaitingRoomUI.ShowWithCode(code);
                    NetworkManager.singleton.StartClient();
                });
                _isConnecting = false;
            },
            onError: err =>
            {
                SetStatus("Неверный код или комната закрыта.");
                _isConnecting = false;
                _joinPanel.interactable = true;
            }
        );
    }

    public void OnClickCancel()
    {
        CancelConnection();
    }

    private void CancelConnection()
    {
        _isConnecting = false;

        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();
        else if (NetworkServer.active)
            NetworkManager.singleton.StopServer();

        SetStatus("Отменено.");
        _loadingPanel.FadeOut(_fade);
        _joinPanel.interactable = true;
        _hostPanel.interactable = true;
        _mainPanel.FadeIn(_fade);
    }

    private void SetStatus(string message)
    {
        if (_connectionStatusText != null)
            _connectionStatusText.text = message;
    }
}