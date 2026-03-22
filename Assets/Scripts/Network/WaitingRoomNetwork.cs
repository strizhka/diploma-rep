using Mirror;
using UnityEngine;

public class WaitingRoomNetwork : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))]
    private string _playerName = "Игрок";

    [SyncVar(hook = nameof(OnReadyChanged))]
    private bool _isReady = false;

    private static WaitingRoomNetwork _localPlayer;
    private static WaitingRoomNetwork _remotePlayer;

    public override void OnStartLocalPlayer()
    {
        _localPlayer = this;
        PuzzleDebugOverlay.Log($"[WaitingRoom] Локальный игрок инициализирован");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isLocalPlayer)
        {
            _remotePlayer = this;
            WaitingRoomUI.OnOtherPlayerJoined(_playerName);
            PuzzleDebugOverlay.Log($"[WaitingRoom] Удалённый игрок найден: {_playerName}");
        }
    }

    public override void OnStopClient()
    {
        if (!isLocalPlayer)
        {
            _remotePlayer = null;
            WaitingRoomUI.OnOtherPlayerDisconnected();
        }
        base.OnStopClient();
    }

    public static void SetReady(bool ready)
    {
        _localPlayer?.CmdSetReady(ready);
    }

    public static void SetName(string name)
    {
        _localPlayer?.CmdSetName(name);
    }

    [Command]
    private void CmdSetName(string name)
    {
        _playerName = name;
    }

    [Command]
    private void CmdSetReady(bool ready)
    {
        _isReady = ready;

        if (AllPlayersReady())
            RpcStartGame();
    }

    private void OnNameChanged(string _, string newName)
    {
        if (!isLocalPlayer)
        {
            WaitingRoomUI.OnOtherPlayerJoined(newName);
            PuzzleDebugOverlay.Log($"[WaitingRoom] Имя удалённого игрока: {newName}");
        }
    }

    private void OnReadyChanged(bool _, bool newReady)
    {
        if (!isLocalPlayer)
        {
            if (newReady)
                WaitingRoomUI.OnOtherPlayerReady();
            else
                WaitingRoomUI.OnOtherPlayerNotReady();

            PuzzleDebugOverlay.Log($"[WaitingRoom] Готовность удалённого: {newReady}");
        }
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

        PuzzleDebugOverlay.Log($"[WaitingRoom] Готовы: {readyCount}/2");
        return readyCount >= 2;
    }

    [ClientRpc]
    private void RpcStartGame()
    {
        PuzzleDebugOverlay.Log("[WaitingRoom] Оба готовы — загружаем игру!");
        if (NetworkServer.active)
            NetworkManager.singleton.ServerChangeScene("Tutorial");
    }
}