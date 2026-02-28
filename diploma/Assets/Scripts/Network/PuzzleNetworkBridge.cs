using Mirror;
using UnityEngine;

public class PuzzleNetworkBridge : NetworkBehaviour
{
    [Tooltip("Ћокальное событие взаимодействи€ Ч то же самое что слушает InteractableObject")]
    [SerializeField] private GameEventInteraction _interactionEvent;

    private static PuzzleNetworkBridge _instance;

    private void Awake()
    {
        _instance = this;
    }

    private void OnEnable()
    {
        _interactionEvent?.AddListener(OnLocalInteraction);
    }

    private void OnDisable()
    {
        _interactionEvent?.RemoveListener(OnLocalInteraction);
    }

    // —рабатывает локально когда игрок нажал E на объекте
    private void OnLocalInteraction(InteractionData data)
    {
        PuzzleDebugOverlay.Log($"[Bridge] локальное событие: {data.ObjectId} = {data.NewState}");
        CmdReportInteraction(data.ObjectId, data.NewState);
    }

    // ќтправл€ет на сервер
    [Command(requiresAuthority = false)]
    private void CmdReportInteraction(string objectId, string newState)
    {
        PuzzleDebugOverlay.Log($"[Cmd] дошло до сервера: {objectId} = {newState}");
        var manager = FindAnyObjectByType<PuzzleManager>();
        if (manager == null)
        {
            Debug.LogError("[Bridge] PuzzleManager не найден на сцене.");
            return;
        }

        manager.ReportInteraction(objectId, newState);
    }
}