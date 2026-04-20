using Mirror;
using UnityEngine;

public class WaitingRoomNetwork : NetworkBehaviour
{
    [Header("Настройки")]
    [SerializeField] private string _gameSceneName = "Tutorial";
    [SerializeField] private int _requiredReadyCount = 2;

    [SyncVar(hook = nameof(OnNameChanged))]
    private string _playerName = "Игрок";

    [SyncVar(hook = nameof(OnReadyChanged))]
    private bool _isReady;

    private static WaitingRoomNetwork _localPlayer;

    public override void OnStartLocalPlayer()
    {
        _localPlayer = this;
        PuzzleDebugOverlay.Log("[WaitingRoom] Локальный игрок инициализирован");
    }

    public override void OnStartClient()
    {
        if (!isLocalPlayer)
        {
            WaitingRoomUI.OnOtherPlayerJoined(_playerName);
            PuzzleDebugOverlay.Log($"[WaitingRoom] Удалённый игрок найден: {_playerName}");
        }
    }

    public override void OnStopClient()
    {
        if (!isLocalPlayer)
            WaitingRoomUI.OnOtherPlayerDisconnected();
    }

    public static void SetReady(bool ready) => _localPlayer?.CmdSetReady(ready);
    public static void SetName(string name) => _localPlayer?.CmdSetName(name);

    [Command] private void CmdSetName(string name) => _playerName = name;

    [Command]
    private void CmdSetReady(bool ready)
    {
        _isReady = ready;
        if (AllPlayersReady())
            LoadGameScene();
    }

    private void OnNameChanged(string _, string newName)
    {
        if (isLocalPlayer) return;
        WaitingRoomUI.OnOtherPlayerJoined(newName);
        PuzzleDebugOverlay.Log($"[WaitingRoom] Имя удалённого игрока: {newName}");
    }

    private void OnReadyChanged(bool _, bool ready)
    {
        if (isLocalPlayer) return;

        if (ready) WaitingRoomUI.OnOtherPlayerReady();
        else WaitingRoomUI.OnOtherPlayerNotReady();
        PuzzleDebugOverlay.Log($"[WaitingRoom] Готовность удалённого: {ready}");
    }

    [Server]
    private bool AllPlayersReady()
    {
        int readyCount = 0;
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null &&
                conn.identity.TryGetComponent<WaitingRoomNetwork>(out var p) &&
                p._isReady)
                readyCount++;
        }

        PuzzleDebugOverlay.Log($"[WaitingRoom] Готовы: {readyCount}/{_requiredReadyCount}");
        return readyCount >= _requiredReadyCount;
    }

    [Server]
    private void LoadGameScene()
    {
        PuzzleDebugOverlay.Log($"[WaitingRoom] Все готовы — грузим '{_gameSceneName}'");
        NetworkManager.singleton.ServerChangeScene(_gameSceneName);
    }
}