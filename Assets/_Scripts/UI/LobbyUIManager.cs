using DG.Tweening;
using Mirror;
using TMPro;
using Unity.Services.Multiplayer;
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

    // ──────────────────────── РЕЖИМ RELAY (как было) ────────────────────────

    public void OnClickStartHost()
    {
        SetStatus("Создание комнаты...");
        _isConnecting = true;
        _hostPanel.SwitchTo(_loadingPanel, _fade);

        NetworkMode.SetRelay();

        EdgegapRelayService.CreateRoom(
            onCodeReady: code =>
            {
                if (!_isConnecting) return;

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

        NetworkMode.SetRelay();

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

    // ──────────────────────── РЕЖИМ LAN (новое) ────────────────────────

    /// <summary>
    /// Создание хоста в локальной сети без relay.
    /// Привязать в инспекторе к новой кнопке «Создать LAN».
    /// </summary>
    public void OnClickStartLanHost()
    {
        SetStatus("Запуск LAN-сервера...");
        _isConnecting = true;

        NetworkMode.SetLan();

        // Определяем локальный IP, чтобы показать его в комнате ожидания.
        // Игрок передаёт его второму игроку любым способом.
        string ip = LanIpHelper.GetLocalIp();

        var nm = NetworkManager.singleton;
        nm.networkAddress = ip;

        // Дополнительно подавляем relay-параметры транспорта на случай,
        // если EdgegapKcpTransport их использует.
        TryClearRelayConfig();
        TrySetTransportPort(NetworkMode.DefaultLanPort);

        _lobbyCanvas.FadeOut(_fade, () =>
        {
            _lobbyCanvas.gameObject.SetActive(false);

            // Показываем IP в комнате ожидания вместо кода relay-сессии.
            WaitingRoomUI.ShowWithCode(ip);
            nm.StartHost();
        });

        _isConnecting = false;
    }

    /// <summary>
    /// Подключение клиента к LAN-хосту по IP.
    /// IP вводится игроком в то же поле _inviteCodeInput, что и код для relay.
    /// Привязать в инспекторе к новой кнопке «Подключиться по LAN».
    /// </summary>
    public void OnClickJoinLanByIp()
    {
        string ip = _inviteCodeInput.text.Trim();

        if (!LanIpHelper.IsLikelyValidAddress(ip))
        {
            SetStatus("Введи IP-адрес хоста (например, 192.168.1.42 или 127.0.0.1).");
            return;
        }

        SetStatus($"Подключение к {ip}...");
        _isConnecting = true;
        _joinPanel.interactable = false;

        NetworkMode.SetLan();
        NetworkMode.LanAddress = ip;

        var nm = NetworkManager.singleton;
        nm.networkAddress = ip;

        TryClearRelayConfig();
        TrySetTransportPort(NetworkMode.DefaultLanPort);

        _lobbyCanvas.FadeOut(_fade, () =>
        {
            _lobbyCanvas.gameObject.SetActive(false);
            WaitingRoomUI.ShowWithCode(ip);
            nm.StartClient();
        });

        _isConnecting = false;
    }

    // ──────────────────────── ОТМЕНА ────────────────────────

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

    // ──────────────────────── ВСПОМОГАТЕЛЬНОЕ ────────────────────────

    /// <summary>
    /// Через рефлексию обнуляет relay-параметры на активном транспорте.
    /// Страховка для EdgegapKcpTransport — если его relay-поля содержат что-то
    /// с предыдущего relay-запуска, он мог бы попытаться использовать их в LAN.
    /// </summary>
    private static void TryClearRelayConfig()
    {
        var transport = Transport.active;
        if (transport == null) return;

        var t = transport.GetType();
        var flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

        foreach (var name in new[] { "RelayAddress", "relayAddress" })
        {
            var f = t.GetField(name, flags);
            if (f != null && f.FieldType == typeof(string))
            {
                f.SetValue(transport, "");
                Debug.Log($"[Lan] Сброшено {name} на транспорте");
            }
        }

        foreach (var name in new[] { "RelayGameServerPort", "relayPort", "RelayPort" })
        {
            var f = t.GetField(name, flags);
            if (f == null) continue;

            if (f.FieldType == typeof(ushort)) f.SetValue(transport, (ushort)0);
            else if (f.FieldType == typeof(int)) f.SetValue(transport, 0);
        }
    }

    /// <summary>
    /// Устанавливает порт у активного транспорта через рефлексию.
    /// Большинство Mirror-транспортов имеют поле port (ushort).
    /// </summary>
    private static void TrySetTransportPort(ushort port)
    {
        var transport = Transport.active;
        if (transport == null) return;

        var t = transport.GetType();
        var flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

        foreach (var name in new[] { "Port", "port" })
        {
            var f = t.GetField(name, flags);
            if (f == null) continue;

            try
            {
                if (f.FieldType == typeof(ushort)) f.SetValue(transport, port);
                else if (f.FieldType == typeof(int)) f.SetValue(transport, (int)port);
                Debug.Log($"[Lan] Установлен порт {port} на транспорте");
                return;
            }
            catch { /* поле не сеттабельно в этой версии — ок */ }
        }
    }
}

//using DG.Tweening;
 //using Mirror;
 //using TMPro;
 //using UnityEngine;

//public class LobbyUIManager : Singleton<LobbyUIManager>
//{
//    [Header("Канвас лобби")]
//    [SerializeField] private CanvasGroup _lobbyCanvas;

//    [Header("Панели")]
//    [SerializeField] private CanvasGroup _mainPanel;
//    [SerializeField] private CanvasGroup _hostPanel;
//    [SerializeField] private CanvasGroup _joinPanel;
//    [SerializeField] private CanvasGroup _loadingPanel;

//    [Header("Код приглашения")]
//    [SerializeField] private TMP_InputField _inviteCodeInput;

//    [Header("Статус")]
//    [SerializeField] private TextMeshProUGUI _connectionStatusText;

//    [Header("Анимация")]
//    [SerializeField] private float _fade = 0.4f;

//    private bool _isConnecting;

//    protected override void Awake()
//    {
//        base.Awake();
//    }

//    private void Start()
//    {
//        _lobbyCanvas.HideInstant();
//    }

//    public void Show()
//    {
//        _isConnecting = false;
//        _mainPanel.ShowInstant();
//        _hostPanel.HideInstant();
//        _joinPanel.HideInstant();
//        _loadingPanel.HideInstant();
//        _lobbyCanvas.SetAlpha(0f);
//        _lobbyCanvas.FadeIn(_fade);
//        SetStatus("");
//    }

//    public void OnClickHost() => _mainPanel.SwitchTo(_hostPanel, _fade);
//    public void OnClickJoin() => _mainPanel.SwitchTo(_joinPanel, _fade);

//    public void OnClickBack()
//    {
//        if (_isConnecting)
//        {
//            CancelConnection();
//            return;
//        }

//        _hostPanel.FadeOut(_fade);
//        _joinPanel.FadeOut(_fade);
//        _loadingPanel.FadeOut(_fade);
//        _mainPanel.FadeIn(_fade);
//        SetStatus("");
//    }

//    public void OnClickBackToMenu()
//    {
//        if (_isConnecting)
//            CancelConnection();

//        _mainPanel.FadeOut(_fade, () =>
//            _lobbyCanvas.FadeOut(_fade, () =>
//            {
//                _lobbyCanvas.gameObject.SetActive(false);
//                MainMenuUI.Instance.Show();
//            }));
//    }

//    public void OnClickStartHost()
//    {
//        SetStatus("Создание комнаты...");
//        _isConnecting = true;
//        _hostPanel.SwitchTo(_loadingPanel, _fade);

//        EdgegapRelayService.CreateRoom(
//            onCodeReady: code =>
//            {
//                if (!_isConnecting) return;

//                //var nm = NetworkManager.singleton as EscapeRoomNetworkManager;
//                //if (nm != null) nm.CurrentRoomCode = code;

//                _lobbyCanvas.FadeOut(_fade, () =>
//                {
//                    _lobbyCanvas.gameObject.SetActive(false);
//                    WaitingRoomUI.ShowWithCode(code);
//                    NetworkManager.singleton.StartHost();
//                });
//                _isConnecting = false;
//            },
//            onError: err =>
//            {
//                SetStatus($"Ошибка: {err}");
//                _isConnecting = false;
//                _loadingPanel.SwitchTo(_hostPanel, _fade);
//            }
//        );
//    }

//    public void OnClickJoinWithCode()
//    {
//        string code = _inviteCodeInput.text.Trim();

//        if (string.IsNullOrEmpty(code))
//        {
//            SetStatus("Введи код приглашения.");
//            return;
//        }

//        SetStatus($"Подключение...");
//        _isConnecting = true;
//        _joinPanel.interactable = false;

//        EdgegapRelayService.JoinRoom(
//            code: code,
//            onReady: () =>
//            {
//                if (!_isConnecting) return;

//                _lobbyCanvas.FadeOut(_fade, () =>
//                {
//                    _lobbyCanvas.gameObject.SetActive(false);
//                    WaitingRoomUI.ShowWithCode(code);
//                    NetworkManager.singleton.StartClient();
//                });
//                _isConnecting = false;
//            },
//            onError: err =>
//            {
//                SetStatus("Неверный код или комната закрыта.");
//                _isConnecting = false;
//                _joinPanel.interactable = true;
//            }
//        );
//    }

//    public void OnClickCancel()
//    {
//        CancelConnection();
//    }

//    private void CancelConnection()
//    {
//        _isConnecting = false;

//        if (NetworkServer.active && NetworkClient.isConnected)
//            NetworkManager.singleton.StopHost();
//        else if (NetworkClient.isConnected)
//            NetworkManager.singleton.StopClient();
//        else if (NetworkServer.active)
//            NetworkManager.singleton.StopServer();

//        SetStatus("Отменено.");
//        _loadingPanel.FadeOut(_fade);
//        _joinPanel.interactable = true;
//        _hostPanel.interactable = true;
//        _mainPanel.FadeIn(_fade);
//    }

//    private void SetStatus(string message)
//    {
//        if (_connectionStatusText != null)
//            _connectionStatusText.text = message;
//    }
//}