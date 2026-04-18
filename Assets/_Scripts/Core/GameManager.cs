using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Инициализация сцены")]
    [SerializeField] private GameObject[] _disableOnStart;
    [SerializeField] private GameObject[] _enableOnStart;

    [Header("Панель отключения")]
    [SerializeField] private CanvasGroup _disconnectPanel;
    [SerializeField] private TextMeshProUGUI _disconnectTitle;
    [SerializeField] private TextMeshProUGUI _disconnectMessage;
    [SerializeField] private GameObject _roomCodeGroup;
    [SerializeField] private TextMeshProUGUI _roomCodeText;

    [Header("Сцена лобби")]
    [SerializeField] private string _lobbyScene = "Lobby";

    [Header("Анимация")]
    [SerializeField] private float _fade = 0.4f;

    private static GameManager _instance;
    public static GameManager Instance => _instance;

    private bool _disconnectShown;

    private void Awake()
    {
        _instance = this;

        foreach (var go in _disableOnStart)
            if (go != null) go.SetActive(false);
        foreach (var go in _enableOnStart)
            if (go != null) go.SetActive(true);

        if (_disconnectPanel != null)
            _disconnectPanel.HideInstant();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public void ShowHostDisconnected()
    {
        if (_disconnectShown) return;
        _disconnectShown = true;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_disconnectTitle != null)
            _disconnectTitle.text = "Соединение потеряно";
        if (_disconnectMessage != null)
            _disconnectMessage.text = "Хост отключился.";
        if (_roomCodeGroup != null)
            _roomCodeGroup.SetActive(false);

        _disconnectPanel?.FadeIn(_fade);
    }

    public void ShowClientDisconnected(string roomCode = "")
    {
        if (_disconnectShown) return;
        _disconnectShown = true;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_disconnectTitle != null)
            _disconnectTitle.text = "Игрок отключился";
        if (_disconnectMessage != null)
            _disconnectMessage.text = "Другой игрок покинул игру.\nОн может переподключиться.";
        if (_roomCodeGroup != null)
        {
            _roomCodeGroup.SetActive(!string.IsNullOrEmpty(roomCode));
            if (_roomCodeText != null)
                _roomCodeText.text = $"Код: {roomCode}";
        }

        _disconnectPanel?.FadeIn(_fade);
    }

    public void HideDisconnectPanel()
    {
        _disconnectShown = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _disconnectPanel?.FadeOut(_fade);
    }

    public void OnClickExitToMenu()
    {
        CleanupAndReturnToLobby();
    }

    public void StopGame()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkServer.active)
            NetworkManager.singleton.StopServer();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();
    }

    public static void CleanupAndReturnToLobby()
    {
        Time.timeScale = 1f;

        if (_instance != null)
            _instance.StopGame();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string lobby = _instance != null ? _instance._lobbyScene : "Lobby";
        SceneManager.LoadScene(lobby);
    }

    private void OnApplicationQuit()
    {
        if (NetworkServer.active || NetworkClient.isConnected)
            StopGame();
        System.Threading.Thread.Sleep(200);
    }
}