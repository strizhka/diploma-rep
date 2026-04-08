using Edgegap;
using Mirror;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ИЗМЕНЕНИЕ: ссылки на PuzzleManager заменены на PuzzleDirector.
/// Всё остальное без изменений.
/// </summary>
public class EscapeRoomNetworkManager : NetworkManager
{
    [Header("Префабы по сценам")]
    [SerializeField] private GameObject _waitingRoomPlayerPrefab;
    [SerializeField] private GameObject _gamePlayerPrefab;

    [Header("Игровые сцены")]
    [SerializeField] private string[] _gameSceneNames = { "BaseMovement" };

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
                PuzzleDebugOverlay.Log($"[Spawn] Игрок {_playerCount} заспавнен на {spawnPoint.name}");
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

        // Устанавливаем индекс игрока для per-player visibility (RoomAOnly/RoomBOnly)
        var visibility = player.GetComponent<PlayerRoomVisibility>();
        if (visibility != null)
            visibility.SetPlayerIndex(_playerCount - 1);
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

            // ВАЖНО: В Inspector на EdgegapKcpTransport также проверь:
            // - Send Window Size = 512 (по умолчанию может быть 128)
            // - Receive Window Size = 512
            // Маленькие буферы → переполнение → клиент теряет синхронизацию
        }
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