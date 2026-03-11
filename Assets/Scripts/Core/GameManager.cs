using Mirror;
using UnityEngine;

public class GameManager : MonoBehaviour
{
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
