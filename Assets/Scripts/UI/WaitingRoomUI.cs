using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaitingRoomUI : MonoBehaviour
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

    private static WaitingRoomUI _instance;

    private void Awake()
    {
        _instance = this;
    }

    private static string _pendingCode;

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
        if (_instance == null) return;
        _instance._otherPlayerStatus.text = "не готов";
    }

    public static void OnOtherPlayerDisconnected()
    {
        if (_instance == null) return;
        _instance._otherPlayerNameText.text = "Ожидание игрока...";
        _instance._otherPlayerStatus.text = "";
        _instance._readyButton.interactable = false;
    }

    public static void ShowWithCode(string code)
    {
        Debug.Log($"[WaitingRoom] ShowWithCode. code={code}, _instance null={_instance == null}");

        if (_instance == null)
        {
            _pendingCode = code;
            return;
        }

        _instance.gameObject.SetActive(true);
        _instance._roomCodeText.text = code;
    }

    public static void OnOtherPlayerJoined(string name)
    {
        if (_instance == null) return;
        _instance._otherPlayerNameText.text = name;
        _instance._otherPlayerStatus.text = "подключён";
        _instance._readyButton.interactable = true;
    }

    public static void OnOtherPlayerReady()
    {
        if (_instance == null) return;
        _instance._otherPlayerStatus.text = "готов";
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
