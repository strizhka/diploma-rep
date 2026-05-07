/// <summary>
/// Хранит текущий режим сетевого соединения. Устанавливается перед вызовом
/// StartHost/StartClient, читается в EscapeRoomNetworkManager и других местах,
/// чтобы пропустить или активировать relay-логику.
/// </summary>
public static class NetworkMode
{
    public enum Mode
    {
        Relay,  // Edgegap relay (по умолчанию, как раньше)
        Lan     // Локальная сеть (loopback или LAN)
    }

    public static Mode Current { get; private set; } = Mode.Relay;

    /// <summary>Адрес для подключения клиента в LAN-режиме (IP или hostname).</summary>
    public static string LanAddress { get; set; } = "127.0.0.1";

    /// <summary>Стандартный порт для LAN-сессии.</summary>
    public const ushort DefaultLanPort = 7777;

    public static void SetRelay() => Current = Mode.Relay;
    public static void SetLan()   => Current = Mode.Lan;

    public static bool IsLan => Current == Mode.Lan;
}
