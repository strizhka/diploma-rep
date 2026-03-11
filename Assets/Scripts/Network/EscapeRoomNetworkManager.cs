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

    private int _playerCount = 0;

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        _playerCount = 0;
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
    }

    private Transform FindSpawnPoint(int playerIndex)
    {
        var spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        var point = spawnPoints.FirstOrDefault(s => s.PlayerIndex == playerIndex);
        return point?.transform;
    }

    private bool IsGameScene()
    {
        return SceneManager.GetActiveScene().name == "BaseMovement";
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