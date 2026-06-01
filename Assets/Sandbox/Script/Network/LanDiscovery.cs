using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// 同一 LAN 内のホストを UDP ブロードキャストで検出する軽量ディスカバリ。
/// クライアントが探索要求をブロードキャスト → ホストが応答（ゲームポート/名前）を返す。
/// IP 手入力なしでホスト一覧を提示するために使う。
///
/// スレッド安全: 受信はバックグラウンドスレッドで行い、結果はロック付き辞書に格納する。
/// メインスレッドからは <see cref="GetHosts"/> でスナップショットを取得する。
/// </summary>
public sealed class LanDiscovery : IDisposable
{
    public const ushort DefaultDiscoveryPort = 47777;
    private const string Magic = "PJREV_LAN_V1";
    private const string RequestBody = "REQ";

    public readonly struct HostEntry
    {
        public readonly string Address;
        public readonly ushort GamePort;
        public readonly string HostName;
        public readonly int LastSeenTick;

        public HostEntry(string address, ushort gamePort, string hostName, int lastSeenTick)
        {
            Address = address;
            GamePort = gamePort;
            HostName = hostName;
            LastSeenTick = lastSeenTick;
        }
    }

    private UdpClient _socket;
    private Thread _thread;
    private volatile bool _running;
    private bool _isServer;
    private ushort _discoveryPort = DefaultDiscoveryPort;
    private ushort _advertisedGamePort;
    private string _hostName = "Host";

    private readonly object _lock = new();
    private readonly Dictionary<string, HostEntry> _hosts = new();

    public bool IsRunning => _running;
    public bool IsServer => _isServer;

    /// <summary>ホスト側: 探索要求に応答するレスポンダを起動する。</summary>
    public void StartResponder(ushort gamePort, string hostName, ushort discoveryPort = DefaultDiscoveryPort)
    {
        Stop();
        _isServer = true;
        _advertisedGamePort = gamePort;
        _hostName = string.IsNullOrEmpty(hostName) ? "Host" : hostName;
        _discoveryPort = discoveryPort;
        Begin(RunServer);
    }

    /// <summary>クライアント側: 周期的に探索要求をブロードキャストしホストを収集する。</summary>
    public void StartClientScan(ushort discoveryPort = DefaultDiscoveryPort)
    {
        Stop();
        _isServer = false;
        _discoveryPort = discoveryPort;
        lock (_lock) _hosts.Clear();
        Begin(RunClient);
    }

    private void Begin(ThreadStart body)
    {
        _running = true;
        _thread = new Thread(body) { IsBackground = true, Name = "LanDiscovery" };
        _thread.Start();
    }

    /// <summary>メインスレッドから呼ぶ。一定時間内に応答のあったホスト一覧を返す。</summary>
    public List<HostEntry> GetHosts(int staleMs = 4000)
    {
        var list = new List<HostEntry>();
        int now = Environment.TickCount;
        lock (_lock)
        {
            foreach (var kv in _hosts)
            {
                if (Mathf.Abs(now - kv.Value.LastSeenTick) <= staleMs)
                    list.Add(kv.Value);
            }
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Address, b.Address));
        return list;
    }

    private void RunServer()
    {
        try
        {
            _socket = new UdpClient();
            _socket.EnableBroadcast = true;
            _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Client.Bind(new IPEndPoint(IPAddress.Any, _discoveryPort));
            _socket.Client.ReceiveTimeout = 500;

            byte[] response = Encoding.UTF8.GetBytes($"{Magic}|{_advertisedGamePort}|{_hostName}");
            string requestPrefix = $"{Magic}|{RequestBody}";

            while (_running)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _socket.Receive(ref remote);
                    string msg = Encoding.UTF8.GetString(data);
                    if (msg.StartsWith(requestPrefix, StringComparison.Ordinal))
                        _socket.Send(response, response.Length, remote);
                }
                catch (SocketException)
                {
                    // 受信タイムアウト（_running ループ継続のため）
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LanDiscovery] レスポンダ停止: {e.Message}");
        }
        finally
        {
            CloseSocket();
        }
    }

    private void RunClient()
    {
        try
        {
            _socket = new UdpClient();
            _socket.EnableBroadcast = true;
            _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            _socket.Client.ReceiveTimeout = 800;

            byte[] request = Encoding.UTF8.GetBytes($"{Magic}|{RequestBody}");
            var broadcast = new IPEndPoint(IPAddress.Broadcast, _discoveryPort);
            var loopback = new IPEndPoint(IPAddress.Loopback, _discoveryPort);

            int sinceBroadcast = int.MaxValue;
            while (_running)
            {
                // 約1秒ごとに探索要求を送出（同一PC検証のため loopback にも送る）。
                if (sinceBroadcast >= 1000)
                {
                    try
                    {
                        _socket.Send(request, request.Length, broadcast);
                        _socket.Send(request, request.Length, loopback);
                    }
                    catch (SocketException)
                    {
                    }
                    sinceBroadcast = 0;
                }

                int waitStart = Environment.TickCount;
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _socket.Receive(ref remote);
                    ParseResponse(data, remote);
                }
                catch (SocketException)
                {
                    // 受信タイムアウト
                }
                sinceBroadcast += Mathf.Max(1, Environment.TickCount - waitStart);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LanDiscovery] スキャン停止: {e.Message}");
        }
        finally
        {
            CloseSocket();
        }
    }

    private void ParseResponse(byte[] data, IPEndPoint remote)
    {
        string msg = Encoding.UTF8.GetString(data);
        string[] parts = msg.Split('|');
        if (parts.Length < 3 || parts[0] != Magic) return;
        if (!ushort.TryParse(parts[1], out ushort gamePort)) return;

        string hostName = parts[2];
        string address = remote.Address.ToString();
        var entry = new HostEntry(address, gamePort, hostName, Environment.TickCount);
        lock (_lock) _hosts[address] = entry;
    }

    public void Stop()
    {
        _running = false;
        CloseSocket();
        if (_thread != null && _thread.IsAlive)
        {
            _thread.Join(300);
        }
        _thread = null;
    }

    private void CloseSocket()
    {
        try { _socket?.Close(); }
        catch { /* 破棄時の例外は無視 */ }
        _socket = null;
    }

    public void Dispose() => Stop();
}
