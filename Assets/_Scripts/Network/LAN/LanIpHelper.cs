using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// Утилита для определения локального IP-адреса хоста в локальной сети.
/// Используется для отображения адреса в UI комнаты ожидания, чтобы хост
/// мог сообщить его второму игроку.
///
/// Возвращает IPv4-адрес активного сетевого интерфейса, отдавая предпочтение
/// проводным и Wi-Fi-адаптерам. Если ничего не нашлось — возвращает 127.0.0.1
/// (это нормальный случай для запуска двух инстансов на одной машине).
/// </summary>
public static class LanIpHelper
{
    /// <summary>
    /// Возвращает «хороший» локальный IP. Для одной машины — 127.0.0.1.
    /// Для двух машин в одной сети — реальный 192.168.x.x / 10.x.x.x.
    /// </summary>
    public static string GetLocalIp()
    {
        // Сначала пробуем найти IP активного сетевого интерфейса
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;

                // Пропускаем виртуальные, loopback и туннельные интерфейсы
                var nt = ni.NetworkInterfaceType;
                if (nt == NetworkInterfaceType.Loopback) continue;
                if (nt == NetworkInterfaceType.Tunnel) continue;

                // Принимаем только проводной Ethernet и Wi-Fi
                bool isUseful =
                    nt == NetworkInterfaceType.Ethernet ||
                    nt == NetworkInterfaceType.Wireless80211 ||
                    nt == NetworkInterfaceType.GigabitEthernet ||
                    nt == NetworkInterfaceType.FastEthernetT ||
                    nt == NetworkInterfaceType.FastEthernetFx;
                if (!isUseful) continue;

                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    string ip = addr.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue; // APIPA (нет реального DHCP)

                    return ip;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LanIpHelper] Ошибка определения IP: {e.Message}");
        }

        // Запасной вариант — через DNS-резолв
        try
        {
            string hostName = Dns.GetHostName();
            var entry = Dns.GetHostEntry(hostName);
            foreach (var ip in entry.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    string s = ip.ToString();
                    if (!s.StartsWith("127.") && !s.StartsWith("169.254."))
                        return s;
                }
            }
        }
        catch { /* не удалось — возвращаем loopback ниже */ }

        // Запасной запасной — loopback. Подходит для двух инстансов на одной машине.
        return "127.0.0.1";
    }

    /// <summary>
    /// Проверяет, выглядит ли строка как валидный IPv4-адрес или hostname.
    /// Используется для валидации поля ввода клиента.
    /// </summary>
    public static bool IsLikelyValidAddress(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        input = input.Trim();
        if (input == "localhost") return true;

        return IPAddress.TryParse(input, out _);
    }
}
