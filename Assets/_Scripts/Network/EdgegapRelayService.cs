using System;
using System.Collections;
using System.Text;
using Edgegap;
using UnityEngine;
using UnityEngine.Networking;

public class EdgegapRelayService : Singleton<EdgegapRelayService>
{
    private string _apiToken = "";

    private const string ApiBase = "https://api.edgegap.com/v1/relays/sessions";
    private const int RequestTimeout = 10;

    protected override bool Persist => true;

    protected override void Awake()
    {
        base.Awake();

        if (string.IsNullOrEmpty(_apiToken))
        {
            var config = Resources.Load<TextAsset>("RelayConfig");
            if (config != null)
                _apiToken = config.text.Trim();
        }
    }

    public static void CreateRoom(Action<string> onCodeReady, Action<string> onError)
    {
        if (!HasInstance)
        {
            onError?.Invoke("EdgegapRelayService не инициализирован.");
            return;
        }
        Instance.StartCoroutine(Instance.CreateRoomCoroutine(onCodeReady, onError));
    }

    public static void JoinRoom(string code, Action onReady, Action<string> onError)
    {
        if (!HasInstance)
        {
            onError?.Invoke("EdgegapRelayService не инициализирован.");
            return;
        }
        Instance.StartCoroutine(Instance.JoinRoomCoroutine(code, onReady, onError));
    }


    private IEnumerator CreateRoomCoroutine(Action<string> onCodeReady, Action<string> onError)
    {
        using var ipRequest = UnityWebRequest.Get("https://api.ipify.org");
        yield return ipRequest.SendWebRequest();

        if (ipRequest.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke("не удалось определить IP");
            yield break;
        }

        string myIp = ipRequest.downloadHandler.text.Trim();
        PuzzleDebugOverlay.Log($"[Relay] IP: {myIp}");

        string body = $"{{\"users\": [{{\"ip\": \"{myIp}\"}}, {{\"ip\": \"{myIp}\"}}]}}";

        using var request = new UnityWebRequest(ApiBase, "POST");
        request.timeout = RequestTimeout;
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"token {_apiToken}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            PuzzleDebugOverlay.Log(
                $"[Relay] Ошибка создания: {request.responseCode} {request.downloadHandler.text}",
                PuzzleDebugOverlay.DebugLevel.Error);
            onError?.Invoke(request.error);
            yield break;
        }

        var session = JsonUtility.FromJson<RelaySession>(request.downloadHandler.text);
        string sessionId = session.session_id;
        PuzzleDebugOverlay.Log($"[Relay] Сессия создана: {sessionId}, статус: {session.status}");

        RelaySession readySession = null;
        string errorMsg = null;

        yield return StartCoroutine(WaitForReady(sessionId,
            onReady: s => readySession = s,
            onError: err => errorMsg = err));

        if (errorMsg != null)
        {
            onError?.Invoke(errorMsg);
            yield break;
        }

        ApplyTransport(readySession, isServer: true);
        PuzzleDebugOverlay.Log($"[Relay] Готово! Код: {sessionId}", PuzzleDebugOverlay.DebugLevel.Ok);
        onCodeReady?.Invoke(sessionId);
    }

    private IEnumerator JoinRoomCoroutine(string code, Action onReady, Action<string> onError)
    {
        using var request = UnityWebRequest.Get($"{ApiBase}/{code}");
        request.SetRequestHeader("Authorization", $"token {_apiToken}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            PuzzleDebugOverlay.Log(
                $"[Relay] Ошибка поиска: {request.downloadHandler.text}",
                PuzzleDebugOverlay.DebugLevel.Error);
            onError?.Invoke("неверный код");
            yield break;
        }

        var session = JsonUtility.FromJson<RelaySession>(request.downloadHandler.text);
        PuzzleDebugOverlay.Log($"[Relay] Сессия найдена: {session.session_id}");

        RelaySession readySession = null;
        string errorMsg = null;

        yield return StartCoroutine(WaitForReady(code,
            onReady: s => readySession = s,
            onError: err => errorMsg = err));

        if (errorMsg != null)
        {
            onError?.Invoke(errorMsg);
            yield break;
        }

        ApplyTransport(readySession, isServer: false);
        PuzzleDebugOverlay.Log($"[Relay] Подключение готово", PuzzleDebugOverlay.DebugLevel.Ok);
        onReady?.Invoke();
    }

    private IEnumerator WaitForReady(string sessionId, Action<RelaySession> onReady, Action<string> onError)
    {
        int attempts = 0;
        const int maxAttempts = 15;

        while (attempts < maxAttempts)
        {
            using var request = UnityWebRequest.Get($"{ApiBase}/{sessionId}");
            request.SetRequestHeader("Authorization", $"token {_apiToken}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"ошибка готовности: {request.error}");
                yield break;
            }

            var session = JsonUtility.FromJson<RelaySession>(request.downloadHandler.text);
            PuzzleDebugOverlay.Log(
                $"[Relay] Статус: {session.status}, ready: {session.ready} (попытка {attempts + 1})");

            if (session.ready && session.relay != null)
            {
                PuzzleDebugOverlay.Log($"[Relay] Полный ответ: {request.downloadHandler.text}");
                onReady?.Invoke(session);
                yield break;
            }

            attempts++;
            yield return new WaitForSeconds(2f);
        }

        onError?.Invoke("relay не ответил. попробуй еще раз");
    }


    private void ApplyTransport(RelaySession session, bool isServer)
    {
        var transport = FindAnyObjectByType<EdgegapKcpTransport>();
        if (transport == null)
        {
            PuzzleDebugOverlay.Log("[Relay] EdgegapKcpTransport не найден!",
                PuzzleDebugOverlay.DebugLevel.Error);
            return;
        }

        transport.relayAddress = session.relay.host;
        transport.relayGameServerPort = (ushort)session.relay.ports.server.port;
        transport.relayGameClientPort = (ushort)session.relay.ports.client.port;
        transport.sessionId = (uint)session.authorization_token;

        uint myUserId = 0;

        if (session.session_users != null)
        {
            foreach (var user in session.session_users)
            {
                PuzzleDebugOverlay.Log($"[Relay] session_user: ip={user.ip_address} token={user.authorization_token}");
            }

            if (session.session_users.Length >= 2)
            {
                myUserId = isServer
                    ? (uint)session.session_users[0].authorization_token
                    : (uint)session.session_users[1].authorization_token;
            }
            else if (session.session_users.Length == 1)
            {
                myUserId = (uint)session.session_users[0].authorization_token;
            }
        }

        transport.userId = myUserId;

        PuzzleDebugOverlay.Log(
            $"[Relay] Транспорт: {session.relay.host} " +
            $"serverPort={session.relay.ports.server.port} " +
            $"clientPort={session.relay.ports.client.port} " +
            $"session={transport.sessionId} user={transport.userId} isServer={isServer}",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    protected override void OnDestroy()
    {
        StopAllCoroutines();
        base.OnDestroy();
    }

    private void OnApplicationQuit()
    {
        StopAllCoroutines();
    }
}

[Serializable]
public class RelaySession
{
    public string session_id;
    public int authorization_token;
    public string status;
    public bool ready;
    public bool linked;
    public RelayData relay;
    public SessionUser[] session_users;
}

[Serializable]
public class SessionUser
{
    public string ip_address;
    public int authorization_token;
}

[Serializable]
public class RelayData
{
    public string ip;
    public string host;
    public RelayPorts ports;
}

[Serializable]
public class RelayPorts
{
    public RelayPort server;
    public RelayPort client;
}

[Serializable]
public class RelayPort
{
    public int port;
    public string protocol;
    public string link;
}