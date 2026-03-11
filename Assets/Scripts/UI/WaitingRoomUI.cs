using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaitingRoomUI : Singleton<WaitingRoomUI>
{
    [Header("Имена")]
    [SerializeField] private TMP_InputField _myNameInput;
    [SerializeField] private TextMeshProUGUI _otherPlayerNameText;
    [SerializeField] private TextMeshProUGUI _otherPlayerStatus;

    [Header("Код комнаты")]
    [SerializeField] private TextMeshProUGUI _roomCodeText;
    [SerializeField] private Button _copyCodeButton;

    [Header("Готовность")]
    [SerializeField] private Button _readyButton;
    [SerializeField] private TextMeshProUGUI _readyButtonText;
    [SerializeField] private GameObject _waitingForOtherText;

    private bool _iAmReady = false;

    private static string _pendingCode;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        _otherPlayerNameText.text = "Ожидание игрока...";
        _otherPlayerStatus.text = "";
        _waitingForOtherText.SetActive(false);
        _readyButton.interactable = false;

        _copyCodeButton.onClick.AddListener(CopyCode);
        _readyButton.onClick.AddListener(OnReadyClicked);
        _myNameInput.onValueChanged.AddListener(OnNameChanged);

        if (!string.IsNullOrEmpty(_pendingCode))
        {
            _roomCodeText.text = _pendingCode;
            _pendingCode = null;
        }
    }

    public static void OnOtherPlayerNotReady()
    {
        if (!HasInstance) return;
        Instance._otherPlayerStatus.text = "не готов";
    }

    public static void OnOtherPlayerDisconnected()
    {
        if (!HasInstance) return;
        Instance._otherPlayerNameText.text = "Ожидание игрока...";
        Instance._otherPlayerStatus.text = "";
        Instance._readyButton.interactable = false;
    }

    public static void ShowWithCode(string code)
    {
        Debug.Log($"[WaitingRoom] ShowWithCode. code={code}, HasInstance={HasInstance}");

        if (!HasInstance)
        {
            _pendingCode = code;
            return;
        }

        Instance.gameObject.SetActive(true);
        Instance._roomCodeText.text = code;
    }

    public static void OnOtherPlayerJoined(string name)
    {
        if (!HasInstance) return;
        Instance._otherPlayerNameText.text = name;
        Instance._otherPlayerStatus.text = "подключён";
        Instance._readyButton.interactable = true;
    }

    public static void OnOtherPlayerReady()
    {
        if (!HasInstance) return;
        Instance._otherPlayerStatus.text = "готов";
    }

    private void OnReadyClicked()
    {
        _iAmReady = !_iAmReady;
        _readyButtonText.text = _iAmReady ? "Отменить готовность" : "Готов";
        _waitingForOtherText.SetActive(_iAmReady);

        WaitingRoomNetwork.SetReady(_iAmReady);
    }

    private void OnNameChanged(string newName)
    {
        WaitingRoomNetwork.SetName(newName);
    }

    private void CopyCode()
    {
        GUIUtility.systemCopyBuffer = _roomCodeText.text;
        _copyCodeButton.GetComponentInChildren<TextMeshProUGUI>().text = "Скопировано!";
        Invoke(nameof(ResetCopyButton), 2f);
    }

    private void ResetCopyButton()
    {
        _copyCodeButton.GetComponentInChildren<TextMeshProUGUI>().text = "Скопировать";
    }
}