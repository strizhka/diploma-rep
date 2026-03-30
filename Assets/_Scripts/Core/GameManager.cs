using Mirror;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Отключить при старте")]
    [SerializeField] private GameObject[] _disableOnStart;

    [Header("Включить при старте")]
    [SerializeField] private GameObject[] _enableOnStart;

    private void Awake()
    {
        foreach (var go in _disableOnStart)
            if (go != null) go.SetActive(false);

        foreach (var go in _enableOnStart)
            if (go != null) go.SetActive(true);
    }

    public void StopGame()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopHost();
        }
        else if (NetworkServer.active)
        {
            NetworkManager.singleton.StopServer();
        }
        else if (NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopClient();
        }
    }

    private void OnApplicationQuit()
    {
        if (NetworkServer.active || NetworkClient.isConnected)
            StopGame();

        System.Threading.Thread.Sleep(200);
    }
}
