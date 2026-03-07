using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUIManager : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] private GameObject _mainMenuPanel;
    [SerializeField] private GameObject _hostPanel;
    [SerializeField] private GameObject _joinPanel;

    [Header("Хост — настройки")]
    [SerializeField] private TMP_InputField _roomNameInput;

    [Header("Поиск — список серверов")]
    [SerializeField] private Transform _serverListContainer;
    [SerializeField] private ServerEntryUI _serverEntryPrefab;
    [SerializeField] private TextMeshProUGUI _searchStatusText;

    [Header("Ручной ввод IP")]
    [SerializeField] private TMP_InputField _manualIpInput;

    [Header("Статус подключения")]
    [SerializeField] private TextMeshProUGUI _connectionStatusText;

    [Header("Зависимости")]
    [SerializeField] private EscapeRoomNetworkDiscovery _discovery;

    // Статический список найденных серверов — заполняется из NetworkDiscovery
    private static readonly Dictionary<long, FoundServerEntry> _foundServers = new();
    private static LobbyUIManager _instance;
    public static bool InstanceIsNull => _instance == null;

    // Пул UI-строк списка
    private readonly List<ServerEntryUI> _entryPool = new();

    private const float ServerTimeout = 5f; // секунд до исчезновения из списка

    // ─── Unity ───

    private void Awake()
    {
        _instance = this;
    }

    private void Start()
    {
        ShowMainMenu();
    }

    private void Update()
    {
        // Убираем из списка серверы которые перестали отвечать
        bool needsRefresh = false;
        var toRemove = new List<long>();

        foreach (var kv in _foundServers)
        {
            if (Time.time - kv.Value.DiscoveredAt > ServerTimeout)
            {
                toRemove.Add(kv.Key);
                needsRefresh = true;
            }
        }

        foreach (var id in toRemove)
            _foundServers.Remove(id);

        if (needsRefresh)
            RefreshServerListUI();
    }

    // ─── Статический приёмник от NetworkDiscovery ───

    public static void OnServerFound(DiscoveryResponse response, System.Uri uri)
    {
        Debug.Log($"[Lobby] OnServerFound вызван. _instance null={_instance == null}");

        if (_instance == null) return;

        _foundServers[response.ServerId] = new FoundServerEntry
        {
            RoomName = response.RoomName,
            CurrentPlayers = response.CurrentPlayers,
            MaxPlayers = response.MaxPlayers,
            Uri = uri,
            ServerId = response.ServerId,
            DiscoveredAt = Time.time
        };

        Debug.Log($"[Lobby] Серверов в списке: {_foundServers.Count}");
        _instance.RefreshServerListUI();
        _instance._searchStatusText.text = $"Найдено комнат: {_foundServers.Count}";
    }

    // ─── Кнопки главного меню ───

    public void OnClickHost()
    {
        _mainMenuPanel.SetActive(false);
        _hostPanel.SetActive(true);
    }

    public void OnClickJoin()
    {
        _mainMenuPanel.SetActive(false);
        _joinPanel.SetActive(true);
        _foundServers.Clear();
        RefreshServerListUI();
        _searchStatusText.text = "Поиск комнат...";
        _discovery.StartSearching();
    }

    public void OnClickBack()
    {
        _discovery.StopAll();
        ShowMainMenu();
    }

    // ─── Хост ───

    public void OnClickStartHost()
    {
#if UNITY_STANDALONE_WIN
        // Автоматически открываем порт при старте хоста
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = "advfirewall firewall add rule name=\"diploma\" dir=in action=allow protocol=UDP localport=7777",
            Verb = "runas", // запрос прав администратора
            UseShellExecute = true
        });
#endif
        NetworkManager.singleton.StartHost();
        _discovery.StartAdvertising();
        SetStatus("Комната создана. Ожидание второго игрока...");
        _hostPanel.SetActive(false);
    }

    // ─── Подключение ───

    // Вызывается из ServerEntryUI при нажатии кнопки в строке списка
    public void ConnectToServer(System.Uri uri)
    {
        _discovery.StopAll();
        NetworkManager.singleton.StartClient(uri);
        SetStatus($"Подключение к {uri.Host}...");
    }

    // Ручной ввод IP
    public void OnClickConnectManual()
    {
        string ip = _manualIpInput.text.Trim();
        if (string.IsNullOrEmpty(ip))
        {
            SetStatus("Введи IP адрес хоста.");
            return;
        }

        var uri = new System.Uri($"kcp://{ip}");
        ConnectToServer(uri);
    }

    // ─── Внутреннее ───

    private void ShowMainMenu()
    {
        _mainMenuPanel.SetActive(true);
        _hostPanel.SetActive(false);
        _joinPanel.SetActive(false);
        SetStatus("");
    }

    private void RefreshServerListUI()
    {
        // Скрываем все строки
        foreach (var entry in _entryPool)
            entry.gameObject.SetActive(false);

        int i = 0;
        foreach (var kv in _foundServers)
        {
            // Берём из пула или создаём новый
            ServerEntryUI ui;
            if (i < _entryPool.Count)
            {
                ui = _entryPool[i];
                ui.gameObject.SetActive(true);
            }
            else
            {
                ui = Instantiate(_serverEntryPrefab, _serverListContainer);
                _entryPool.Add(ui);
            }

            ui.Setup(kv.Value, this);
            i++;
        }
    }

    private void SetStatus(string message)
    {
        if (_connectionStatusText != null)
            _connectionStatusText.text = message;
    }
}