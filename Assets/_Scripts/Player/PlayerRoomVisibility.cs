using Mirror;
using UnityEngine;

/// <summary>
/// Настраивает Culling Mask камеры: скрывает объекты на слое "чужой" комнаты.
/// Игрок 0 (Room A) видит RoomAOnly, не видит RoomBOnly.
/// Игрок 1 (Room B) видит RoomBOnly, не видит RoomAOnly.
///
/// НАСТРОЙКА:
/// 1. Добавь на Player-префаб (рядом с PlayerController)
/// 2. Создай Layer'ы: RoomAOnly (6), RoomBOnly (7) в Tags and Layers
/// 3. EscapeRoomNetworkManager после спавна: player.GetComponent<PlayerRoomVisibility>().SetPlayerIndex(index)
/// </summary>
public class PlayerRoomVisibility : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnPlayerIndexChanged))]
    private int _playerIndex = -1;

    public int PlayerIndex => _playerIndex;

    /// <summary>
    /// Вызывается сервером после спавна.
    /// </summary>
    [Server]
    public void SetPlayerIndex(int index)
    {
        _playerIndex = index;
    }

    public override void OnStartLocalPlayer()
    {
        if (_playerIndex >= 0)
            ApplyCullingMask(_playerIndex);
    }

    private void OnPlayerIndexChanged(int oldIndex, int newIndex)
    {
        if (isLocalPlayer)
            ApplyCullingMask(newIndex);
    }

    private void ApplyCullingMask(int index)
    {
        // Находим Main Camera (та, на которой Camera компонент)
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[Visibility] Camera.main не найдена!");
            return;
        }

        // Имена слоёв
        int roomALayer = LayerMask.NameToLayer("RoomAOnly");
        int roomBLayer = LayerMask.NameToLayer("RoomBOnly");

        if (roomALayer == -1 || roomBLayer == -1)
        {
            Debug.LogError("[Visibility] Слои RoomAOnly/RoomBOnly не найдены! Создай их в Tags and Layers.");
            return;
        }

        if (index == 1)
        {
            // Игрок A: видит RoomAOnly, не видит RoomBOnly
            mainCam.cullingMask |= (1 << roomALayer);
            mainCam.cullingMask &= ~(1 << roomBLayer);
            PuzzleDebugOverlay.Log("[Visibility] Игрок A: вижу RoomAOnly",
                PuzzleDebugOverlay.DebugLevel.Ok);
        }
        else
        {
            // Игрок B: видит RoomBOnly, не видит RoomAOnly
            mainCam.cullingMask |= (1 << roomBLayer);
            mainCam.cullingMask &= ~(1 << roomALayer);
            PuzzleDebugOverlay.Log("[Visibility] Игрок B: вижу RoomBOnly",
                PuzzleDebugOverlay.DebugLevel.Ok);
        }
    }
}
