using Mirror;
using UnityEngine;
using UnityEngine.Events;


public class ItemReceiver : NetworkBehaviour
{
    [Header("Какой предмет принимает")]
    [Tooltip("ItemId из ItemDefinition. Должен совпадать с тем, что в руках у игрока.")]
    [SerializeField] private string _requiredItemId;

    [Header("Результат")]
    [Tooltip("Состояние InteractableObject после применения. " +
             "Добавь это состояние в _statesCycle объекта!")]
    [SerializeField] private string _resultState = "activated";

    [Tooltip("Забрать предмет из инвентаря? true = расходуемый (шар, ключ). " +
             "false = многоразовый (зажигалка, отвёртка).")]
    [SerializeField] private bool _consumeItem = true;

    [Header("Событийная шина")]
    [Tooltip("Та же GameEventInteraction что на InteractableObject и PuzzleNetworkBridge. " +
             "Нужна чтобы PuzzleManager узнал о смене состояния после применения предмета.")]
    [SerializeField] private GameEventInteraction _interactionEvent;

    [Header("Визуал (опционально)")]
    [Tooltip("Префаб, который появится в позиции объекта после применения. " +
             "null = ничего не спавнится (объект сам меняет вид через OnStateChanged).")]
    [SerializeField] private GameObject _visualPrefab;

    [Tooltip("Смещение визуала от центра объекта")]
    [SerializeField] private Vector3 _visualOffset = Vector3.zero;

    [Header("События")]
    [Tooltip("Вызывается после успешного применения (на обоих клиентах)")]
    public UnityEvent OnItemApplied;

    [SyncVar(hook = nameof(OnFilledChanged))]
    private bool _isFilled;

    private InteractableObject _interactable;

    public string RequiredItemId => _requiredItemId;
    public bool IsFilled => _isFilled;

    private void Awake()
    {
        _interactable = GetComponent<InteractableObject>();
    }

    [Server]
    public bool TryApply(string itemId)
    {
        if (_isFilled)
        {
            PuzzleDebugOverlay.Log(
                $"[ItemReceiver] '{_interactable?.ObjectId}' уже заполнен.",
                PuzzleDebugOverlay.DebugLevel.Warning);
            return false;
        }

        if (itemId != _requiredItemId)
        {
            PuzzleDebugOverlay.Log(
                $"[ItemReceiver] '{_interactable?.ObjectId}' требует '{_requiredItemId}', получил '{itemId}'.",
                PuzzleDebugOverlay.DebugLevel.Warning);
            return false;
        }

        _isFilled = true;

        if (_interactable != null)
            _interactable.ApplyState(_resultState);

        if (_interactable != null && _interactionEvent != null)
            _interactionEvent.Raise(new InteractionData(_interactable.ObjectId, _resultState));

        PuzzleDebugOverlay.Log(
            $"[ItemReceiver] '{_interactable?.ObjectId}' ← '{itemId}' → состояние '{_resultState}'",
            PuzzleDebugOverlay.DebugLevel.Ok);

        if (_visualPrefab != null)
            SpawnVisual();

        RpcNotifyApplied();

        return true;
    }

    public bool ShouldConsume => _consumeItem;

    [Server]
    private void SpawnVisual()
    {
        var go = Instantiate(
            _visualPrefab,
            transform.position + _visualOffset,
            transform.rotation
        );

        NetworkServer.Spawn(go);

        PuzzleDebugOverlay.Log(
            $"[ItemReceiver] Визуал '{go.name}' заспавнен через NetworkServer",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    [ClientRpc]
    private void RpcNotifyApplied()
    {
        OnItemApplied?.Invoke();
    }

    private void OnFilledChanged(bool _, bool filled)
    {
        if (filled)
            PuzzleDebugOverlay.Log($"[ItemReceiver] '{_interactable?.ObjectId}' заполнен (sync)");
    }
}