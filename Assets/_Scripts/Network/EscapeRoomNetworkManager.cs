using Edgegap;
using Mirror;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EscapeRoomNetworkManager : NetworkManager
{
    [Header("Префабы по сценам")]
    [SerializeField] private GameObject _waitingRoomPlayerPrefab;
    [SerializeField] private GameObject _gamePlayerPrefab;

    [Header("Игровые сцены")]
    [SerializeField] private string[] _gameSceneNames = { "Tutorial" };

    private int _playerCount = 0;
    private string _currentRoomCode = "";

    public string CurrentRoomCode
    {
        get => _currentRoomCode;
        set => _currentRoomCode = value;
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        _playerCount = 0;

        InteractableObjectRegistry.ClearAll();
        PuzzleDebugOverlay.ClearLog();

        if (PuzzleDebugOverlay.HasInstance)
            PuzzleDebugOverlay.Instance.InvalidateCache();

        PuzzleDebugOverlay.Log($"[Network] Сцена загружена: {sceneName}");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        GameObject prefab = IsGameScene()
            ? _gamePlayerPrefab
            : _waitingRoomPlayerPrefab;

        GameObject player;

        if (IsGameScene())
        {
            var spawnPoint = FindSpawnPoint(_playerCount);

            if (spawnPoint != null)
            {
                player = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
                PuzzleDebugOverlay.Log($"[Spawn] Игрок {_playerCount} на {spawnPoint.name}");
            }
            else
            {
                player = Instantiate(prefab);
                PuzzleDebugOverlay.Log($"[Spawn] Спавнер {_playerCount} не найден",
                    PuzzleDebugOverlay.DebugLevel.Warning);
            }
        }
        else
        {
            player = Instantiate(prefab);
        }

        _playerCount++;
        NetworkServer.AddPlayerForConnection(conn, player);

        var visibility = player.GetComponent<PlayerRoomVisibility>();
        if (visibility != null)
            visibility.SetPlayerIndex(_playerCount - 1);

        // Клиент переподключился → скрываем панель отключения
        if (_playerCount >= 2 && GameManager.Instance != null)
            GameManager.Instance.HideDisconnectPanel();
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        _playerCount = Mathf.Max(0, _playerCount - 1);

        if (IsGameScene() && GameManager.Instance != null)
            GameManager.Instance.ShowClientDisconnected(_currentRoomCode);

        PuzzleDebugOverlay.Log("[Network] Клиент отключился",
            PuzzleDebugOverlay.DebugLevel.Warning);
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();

        if (!NetworkServer.active && IsGameScene() && GameManager.Instance != null)
            GameManager.Instance.ShowHostDisconnected();

        PuzzleDebugOverlay.Log("[Network] Отключение от сервера",
            PuzzleDebugOverlay.DebugLevel.Error);
    }

    private Transform FindSpawnPoint(int playerIndex)
    {
        var spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        var point = spawnPoints.FirstOrDefault(s => s.PlayerIndex == playerIndex);
        return point?.transform;
    }

    private bool IsGameScene()
    {
        string current = SceneManager.GetActiveScene().name;
        foreach (var sceneName in _gameSceneNames)
        {
            if (current == sceneName)
                return true;
        }
        return false;
    }

    public override void Awake()
    {
        base.Awake();

        var transport = GetComponent<EdgegapKcpTransport>();
        if (transport != null)
            transport.Timeout = 60000;
    }

    public override void OnApplicationQuit()
    {
        StopAllCoroutines();
        base.OnApplicationQuit();
    }

    public override void OnDestroy()
    {
        var transport = GetComponent<EdgegapKcpTransport>();
        if (transport != null)
            transport.Shutdown();
    }
}