using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServerEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _roomNameText;
    [SerializeField] private TextMeshProUGUI _playersText;
    [SerializeField] private TextMeshProUGUI _ipText;
    [SerializeField] private Button _connectButton;

    private System.Uri _uri;
    private LobbyUIManager _manager;

    public void Setup(FoundServerEntry entry, LobbyUIManager manager)
    {
        _uri = entry.Uri;
        _manager = manager;

        _roomNameText.text = entry.RoomName;
        _playersText.text = $"{entry.CurrentPlayers}/{entry.MaxPlayers}";
        _ipText.text = entry.Uri.Host;

        _connectButton.interactable = entry.CurrentPlayers < entry.MaxPlayers;

        _connectButton.onClick.RemoveAllListeners();
        _connectButton.onClick.AddListener(() => _manager.ConnectToServer(_uri));
    }
}
