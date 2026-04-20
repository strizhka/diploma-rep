using Edgegap;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Кастомный NetworkManager.
/// Ответственности:
/// — спавн разных Player-префабов в зависимости от сцены;
/// — регистрация дополнительных spawnable-префабов (иначе не приходят клиенту);
/// — логирование сетевых событий;
/// — показ DisconnectedOverlay при разрыве соединения в игре;
/// — нейтрализация отладочной IMGUI-панели EdgegapKcpTransport.
/// </summary>
public class EscapeRoomNetworkManager : NetworkManager
{
    [Header("Префабы по сценам")]
    [SerializeField] private GameObject _waitingRoomPlayerPrefab;
    [SerializeField] private GameObject _gamePlayerPrefab;

    [Header("Игровые сцены")]
    [SerializeField] private string[] _gameSceneNames = { "Tutorial", "BaseMovement" };

    [Header("Дополнительные spawnable-префабы")]
    [Tooltip("Префабы, которые спавнятся в игре через NetworkServer.Spawn(): " +
             "кружка из ItemReceiver, фигурки из PedestalSlot и т.п. " +
             "Без регистрации клиент их не увидит.")]
    [SerializeField] private GameObject[] _extraSpawnPrefabs;

    private int _nextPlayerIndex;

    // ──────────────────────── СЕРВЕР ────────────────────────

    public override void OnStartServer()
    {
        base.OnStartServer();
        _nextPlayerIndex = 0;
        Log($"[Network] OnStartServer (transport={Transport.active?.GetType().Name})");
    }

    public override void OnStopServer()
    {
        Log("[Network] OnStopServer");
        base.OnStopServer();
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        _nextPlayerIndex = 0;

        InteractableObjectRegistry.ClearAll();
        PuzzleDebugOverlay.ClearLog();
        if (PuzzleDebugOverlay.HasInstance)
            PuzzleDebugOverlay.Instance.InvalidateCache();

        Log($"[Network] Server scene → {sceneName}");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        GameObject prefab = IsGameScene() ? _gamePlayerPrefab : _waitingRoomPlayerPrefab;
        if (prefab == null)
        {
            Debug.LogError("[Network] Player-префаб не задан в EscapeRoomNetworkManager!");
            return;
        }

        GameObject player = IsGameScene()
            ? InstantiateAtSpawnPoint(prefab, _nextPlayerIndex)
            : Instantiate(prefab);

        NetworkServer.AddPlayerForConnection(conn, player);

        if (player.TryGetComponent<PlayerRoomVisibility>(out var visibility))
            visibility.SetPlayerIndex(_nextPlayerIndex);

        Log($"[Network] OnServerAddPlayer connId={conn.connectionId} playerIndex={_nextPlayerIndex} prefab='{prefab.name}'");
        _nextPlayerIndex++;
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Log($"[Network] OnServerConnect connId={conn.connectionId} address={conn.address}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        int connId = conn.connectionId;
        base.OnServerDisconnect(conn);
        Log($"[Network] OnServerDisconnect connId={connId}");

        if (NetworkServer.active && IsGameScene() && CountNonHostConnections() == 0)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ShowClientDisconnected();
        }
    }

    public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
    {
        Log($"[Network] OnServerError conn={conn?.connectionId} error={error} reason={reason}");
        base.OnServerError(conn, error, reason);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Log("[Network] OnStartClient");
    }

    public override void OnStopClient()
    {
        Log("[Network] OnStopClient");
        base.OnStopClient();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Log("[Network] OnClientConnect");
    }

    public override void OnClientDisconnect()
    {
        Log("[Network] OnClientDisconnect");

        if (IsGameScene() && !NetworkServer.active && GameManager.Instance != null)
        {
            GameManager.Instance.ShowHostDisconnected();
            return;
        }

        base.OnClientDisconnect();
    }

    public override void OnClientError(TransportError error, string reason)
    {
        Log($"[Network] OnClientError error={error} reason={reason}");
        base.OnClientError(error, reason);
    }

    // ──────────────────────── LIFECYCLE ────────────────────────

    public override void Awake()
    {
        base.Awake();

        if (TryGetComponent<EdgegapKcpTransport>(out var transport))
        {
            transport.Timeout = 60000;
            TryDisableTransportDebugGUI(transport);
        }

        RegisterExtraSpawnPrefabs();
    }

    public override void OnApplicationQuit()
    {
        StopAllCoroutines();
        base.OnApplicationQuit();
    }

    public override void OnDestroy()
    {
        // base ПЕРВЫМ: Mirror чистит listeners. Без этого при переподключении
        // остаются «висячие» хендлеры и возникает «Multiple NetworkManagers detected».
        base.OnDestroy();

        if (TryGetComponent<EdgegapKcpTransport>(out var transport))
            transport.Shutdown();
    }

    // ──────────────────────── HELPERS ────────────────────────

    private GameObject InstantiateAtSpawnPoint(GameObject prefab, int playerIndex)
    {
        var points = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        var point = points.FirstOrDefault(s => s.PlayerIndex == playerIndex);

        if (point != null)
            return Instantiate(prefab, point.transform.position, point.transform.rotation);

        Debug.LogWarning($"[Network] SpawnPoint для игрока {playerIndex} не найден — спавним в (0,0,0)");
        return Instantiate(prefab);
    }

    private bool IsGameScene()
    {
        string current = SceneManager.GetActiveScene().name;
        foreach (var s in _gameSceneNames)
            if (current == s) return true;
        return false;
    }

    private int CountNonHostConnections()
    {
        int n = 0;
        foreach (var c in NetworkServer.connections.Values)
            if (c != null && c.connectionId != 0) n++;
        return n;
    }

    private void RegisterExtraSpawnPrefabs()
    {
        if (_extraSpawnPrefabs == null || _extraSpawnPrefabs.Length == 0) return;

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
            Log($"[Network] +{added} extra spawn prefabs (итого spawnPrefabs={spawnPrefabs.Count})");
    }

    private static void TryDisableTransportDebugGUI(EdgegapKcpTransport transport)
    {
        string[] candidateNames = { "showRelayGUI", "showGUI", "debugGUI", "OnGUIEnabled", "showDebugGUI", "relayGUIEnabled" };
        var t = transport.GetType();

        foreach (var name in candidateNames)
        {
            var field = t.GetField(name, System.Reflection.BindingFlags.Public
                                       | System.Reflection.BindingFlags.NonPublic
                                       | System.Reflection.BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(transport, false);
                Debug.Log($"[Network] EdgegapKcpTransport.{name} = false (debug IMGUI выключен)");
                return;
            }
        }

        Debug.LogWarning("[Network] Не нашёл bool-поле отладочной GUI EdgegapKcpTransport. " +
                         "Закомментируй OnGUIRelay() вручную в EdgegapKcpTransport.OnGUI().");
    }

    private static void Log(string msg)
    {
        Debug.Log(msg);
        if (PuzzleDebugOverlay.HasInstance) PuzzleDebugOverlay.Log(msg);
    }
}