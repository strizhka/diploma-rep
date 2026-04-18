using Edgegap;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EscapeRoomNetworkManager : NetworkManager
{
    [Header("Префабы по сценам")]
    [SerializeField] private GameObject _waitingRoomPlayerPrefab;
    [SerializeField] private GameObject _gamePlayerPrefab;

    [Header("Игровые сцены")]
    [SerializeField] private string[] _gameSceneNames = { "BaseMovement", "Tutorial" };

    [Header("Дополнительные spawnable-префабы")]
    [Tooltip("Префабы, которые могут быть заспавнены в игре через NetworkServer.Spawn() " +
             "(например, кружка кофе из ItemReceiver, фигурки из PedestalSlot). " +
             "Кладёшь их сюда — на старте сервера они регистрируются у Mirror, " +
             "и спавн будет работать у клиента.")]
    [SerializeField] private GameObject[] _extraSpawnPrefabs;

    private int _playerCount = 0;

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        _playerCount = 0;

        InteractableObjectRegistry.ClearAll();
        PuzzleDebugOverlay.ClearLog();

        if (PuzzleDebugOverlay.HasInstance)
            PuzzleDebugOverlay.Instance.InvalidateCache();

        PuzzleDebugOverlay.Log($"[Network] Сцена загружена: {sceneName}");
        FileLogger.Write($"[Network] OnServerSceneChanged → '{sceneName}'");
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
                PuzzleDebugOverlay.Log($"[Spawn] Спавнер {_playerCount} не найден — спавним в (0,0,0)",
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

        FileLogger.Write($"[Network] OnServerAddPlayer connId={conn.connectionId} " +
                         $"playerIndex={_playerCount - 1} prefab='{prefab.name}'");
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
        {
            transport.Timeout = 60000;
        }


        if (_extraSpawnPrefabs != null && _extraSpawnPrefabs.Length > 0)
        {
            int added = 0;
            var set = new HashSet<GameObject>(spawnPrefabs);
            foreach (var p in _extraSpawnPrefabs)
            {
                if (p != null && set.Add(p))
                {
                    spawnPrefabs.Add(p);
                    added++;
                }
            }
            if (added > 0)
                FileLogger.Write($"[Network] +{added} extra spawn prefabs зарегистрированы " +
                                 $"(итого spawnPrefabs={spawnPrefabs.Count})");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        FileLogger.Write($"[Network] OnStartServer (transport={Transport.active?.GetType().Name})");
        PuzzleDebugOverlay.Log("[Network] Сервер запущен", PuzzleDebugOverlay.DebugLevel.Ok);
    }

    public override void OnStopServer()
    {
        FileLogger.Write("[Network] OnStopServer");
        PuzzleDebugOverlay.Log("[Network] Сервер остановлен");
        base.OnStopServer();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        FileLogger.Write($"[Network] OnServerConnect connId={conn.connectionId} address={conn.address}");
        PuzzleDebugOverlay.Log($"[Network] Клиент {conn.connectionId} подключился",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        FileLogger.Write($"[Network] OnServerDisconnect connId={conn.connectionId}");
        PuzzleDebugOverlay.Log($"[Network] Клиент {conn.connectionId} отключился",
            PuzzleDebugOverlay.DebugLevel.Warning);
        base.OnServerDisconnect(conn);
    }

    public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
    {
        FileLogger.Write($"[Network] OnServerError conn={conn?.connectionId} error={error} reason={reason}");
        PuzzleDebugOverlay.Log($"[Network] Server error: {error} ({reason})",
            PuzzleDebugOverlay.DebugLevel.Error);
        base.OnServerError(conn, error, reason);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        FileLogger.Write("[Network] OnStartClient");
        PuzzleDebugOverlay.Log("[Network] Клиент запущен");
    }

    public override void OnStopClient()
    {
        FileLogger.Write("[Network] OnStopClient");
        PuzzleDebugOverlay.Log("[Network] Клиент остановлен");
        base.OnStopClient();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        FileLogger.Write("[Network] OnClientConnect — соединение установлено");
        PuzzleDebugOverlay.Log("[Network] Подключились к серверу", PuzzleDebugOverlay.DebugLevel.Ok);
    }

    public override void OnClientDisconnect()
    {
        FileLogger.Write("[Network] OnClientDisconnect — соединение разорвано");
        PuzzleDebugOverlay.Log("[Network] Отключение от сервера",
            PuzzleDebugOverlay.DebugLevel.Warning);
        base.OnClientDisconnect();
    }

    public override void OnClientError(TransportError error, string reason)
    {
        FileLogger.Write($"[Network] OnClientError error={error} reason={reason}");
        PuzzleDebugOverlay.Log($"[Network] Client error: {error} ({reason})",
            PuzzleDebugOverlay.DebugLevel.Error);
        base.OnClientError(error, reason);
    }

    public override void OnApplicationQuit()
    {
        FileLogger.Write("[Network] OnApplicationQuit");
        StopAllCoroutines();
        base.OnApplicationQuit();
    }

    public override void OnDestroy()
    {
        FileLogger.Write("[Network] EscapeRoomNetworkManager.OnDestroy");
        var transport = GetComponent<EdgegapKcpTransport>();
        if (transport != null)
            transport.Shutdown();
    }
}