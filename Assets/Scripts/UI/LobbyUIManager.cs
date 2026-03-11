using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUIManager : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] private GameObject _mainMenuPanel;
    [SerializeField] private GameObject _hostPanel;
    [SerializeField] private GameObject _joinPanel;

    [Header("Код приглашения")]
    [SerializeField] private TMP_InputField _inviteCodeInput;

    [Header("Статус")]
    [SerializeField] private TextMeshProUGUI _connectionStatusText;

    public static bool InstanceIsNull => _instance == null;
    private static LobbyUIManager _instance;

    private void Awake()
    {
        _instance = this;
    }

    private void Start()
    {
        ShowMainMenu();
    }

    public void OnClickHost()
    {
        _mainMenuPanel.SetActive(false);
        _hostPanel.SetActive(true);
    }

    public void OnClickJoin()
    {
        _mainMenuPanel.SetActive(false);
        _joinPanel.SetActive(true);
    }

    public void OnClickBack()
    {
        ShowMainMenu();
    }


    public void OnClickStartHost()
    {
        SetStatus("Создание комнаты...");
        _hostPanel.SetActive(false);

        EdgegapRelayService.CreateRoom(
            onCodeReady: code =>
            {
                NetworkManager.singleton.StartHost();
                WaitingRoomUI.ShowWithCode(code);
            },
            onError: err =>
            {
                SetStatus($"Ошибка: {err}");
                _hostPanel.SetActive(true);
            }
        );
    }

    public void OnClickJoinWithCode()
    {
        string code = _inviteCodeInput.text.Trim();

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Введи код приглашения.");
            return;
        }

        SetStatus($"Подключение по коду {code}...");

        EdgegapRelayService.JoinRoom(
            code: code,
            onReady: () =>
            {
                WaitingRoomUI.ShowWithCode(code);
                NetworkManager.singleton.StartClient();
            },
            onError: err =>
            {
                SetStatus("Неверный код или комната закрыта.");
            }
        );
    }

    private void ShowMainMenu()
    {
        _mainMenuPanel.SetActive(true);
        _hostPanel.SetActive(false);
        _joinPanel.SetActive(false);
        SetStatus("");
    }

    private void SetStatus(string message)
    {
        if (_connectionStatusText != null)
            _connectionStatusText.text = message;
    }
}