using Mirror;
using UnityEngine;

/// <summary>
/// Проверяет совпадение фигурок на постаментах.
/// Работает с PedestalSlot (универсальный постамент).
///
/// Каждая пара: постамент + ожидаемый ItemId.
/// Когда все совпали → загадка решена.
///
/// НАСТРОЙКА:
/// 1. GO "PedestalMatcher" + NetworkIdentity
/// 2. _pairs: постамент + правильный ItemId
/// 3. На каждом PedestalSlot → OnItemPlaced → PedestalMatcher.CheckMatch
/// </summary>
public class PedestalMatcher : NetworkBehaviour
{
    [System.Serializable]
    public class PedestalPair
    {
        [Tooltip("Название (для удобства)")]
        public string Name;

        [Tooltip("Какой предмет должен стоять")]
        public string CorrectItemId;

        [Tooltip("Постамент")]
        public PedestalSlot Pedestal;
    }

    [Header("Пары")]
    [SerializeField] private PedestalPair[] _pairs;

    [Header("Результат")]
    [SerializeField] private InteractableObject _resultObject;
    [SerializeField] private string _resultState = "solved";

    [Header("Событийная шина")]
    [SerializeField] private GameEventInteraction _interactionEvent;

    [Header("Звуки")]
    [SerializeField] private AudioClip _solvedSound;

    private bool _isSolved;

    /// <summary>
    /// Привязывается к PedestalSlot.OnItemPlaced (UnityEvent) на каждом постаменте.
    /// </summary>
    public void CheckMatch()
    {
        if (!NetworkServer.active || _isSolved) return;

        int matched = 0;

        for (int i = 0; i < _pairs.Length; i++)
        {
            var pair = _pairs[i];
            if (pair.Pedestal == null) continue;

            if (pair.Pedestal.PlacedItemId == pair.CorrectItemId)
                matched++;
        }

        PuzzleDebugOverlay.Log($"[PedestalMatcher] Совпадений: {matched}/{_pairs.Length}");

        if (matched >= _pairs.Length)
        {
            _isSolved = true;

            if (_resultObject != null)
                _resultObject.ApplyState(_resultState);

            if (_interactionEvent != null && _resultObject != null)
                _interactionEvent.Raise(
                    new InteractionData(_resultObject.ObjectId, _resultState));

            RpcOnSolved();

            PuzzleDebugOverlay.Log("[PedestalMatcher] ВСЕ СОВПАЛИ!",
                PuzzleDebugOverlay.DebugLevel.Ok);
        }
    }

    [ClientRpc]
    private void RpcOnSolved()
    {
        if (_solvedSound != null)
            AudioSource.PlayClipAtPoint(_solvedSound, transform.position);
    }
}