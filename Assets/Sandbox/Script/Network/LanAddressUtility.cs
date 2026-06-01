using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// LAN マルチプレイ用のローカル IPv4 アドレス取得。
/// </summary>
public static class LanAddressUtility
{
    /// <summary>ループバック以外の IPv4 を優先度順に返す（重複なし）。</summary>
    public static IReadOnlyList<string> GetLanIPv4Addresses()
    {
        var results = new List<string>();
        var seen = new HashSet<string>();

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var props = ni.GetIPProperties();
                if (props == null) continue;

                foreach (var uni in props.UnicastAddresses)
                {
                    if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(uni.Address)) continue;

                    string ip = uni.Address.ToString();
                    if (!seen.Add(ip)) continue;
                    if (IsLinkLocalOrInvalid(ip)) continue;

                    results.Add(ip);
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[LanAddress] 取得失敗: {ex.Message}");
        }

        results.Sort(CompareAddressPreference);
        return results;
    }

    public static string GetPrimaryLanIPv4()
    {
        var list = GetLanIPv4Addresses();
        return list.Count > 0 ? list[0] : "127.0.0.1";
    }

    /// <summary>Join 用の表示文字列（IP:port を改行区切り）。</summary>
    public static string FormatJoinAddresses(ushort port)
    {
        var ips = GetLanIPv4Addresses();
        if (ips.Count == 0)
            return $"127.0.0.1:{port}  (LAN IP を取得できませんでした)";

        var sb = new StringBuilder();
        for (int i = 0; i < ips.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(ips[i]).Append(':').Append(port);
        }

        return sb.ToString();
    }

    private static bool IsLinkLocalOrInvalid(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr)) return true;
        var bytes = addr.GetAddressBytes();
        if (bytes.Length != 4) return true;
        // 169.254.x.x APIPA
        if (bytes[0] == 169 && bytes[1] == 254) return true;
        return false;
    }

    private static int CompareAddressPreference(string a, string b)
    {
        int scoreA = ScoreAddress(a);
        int scoreB = ScoreAddress(b);
        return scoreB.CompareTo(scoreA);
    }

    private static int ScoreAddress(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr)) return 0;
        var bytes = addr.GetAddressBytes();
        if (bytes[0] == 192 && bytes[1] == 168) return 100;
        if (bytes[0] == 10) return 90;
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return 80;
        return 10;
    }
}
