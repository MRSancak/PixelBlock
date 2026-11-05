using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Godot;

namespace PixelBlock.Network;

public sealed partial class LanDiscovery : Node
{
    public const int DiscoveryPort = 24566;

    public sealed class LanServer
    {
        public string Name;
        public string Ip;
        public int Port;
        public double Seen;
    }

    public readonly List<LanServer> Servers = new();

    private UdpClient _tx, _rx;
    private string _payload;
    private float _t;
    private double _clock;

    public void StartBroadcast(string hostName, int port)
    {
        StopBroadcast();
        try
        {
            _tx = new UdpClient { EnableBroadcast = true };
            _payload = $"RAENGINE1|{hostName.Replace("|", "")}|{port}";
        }
        catch (System.Exception e) { GD.PushWarning("[LAN] yayın başlatılamadı: " + e.Message); _tx = null; }
    }

    public void StopBroadcast()
    {
        try { _tx?.Close(); } catch { }
        _tx = null;
    }

    public void StartListen()
    {
        StopListen();
        try
        {
            _rx = new UdpClient(AddressFamily.InterNetwork);
            _rx.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _rx.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
        }
        catch (System.Exception e) { GD.PushWarning("[LAN] dinleme başlatılamadı: " + e.Message); _rx = null; }
    }

    public void StopListen()
    {
        try { _rx?.Close(); } catch { }
        _rx = null;
        Servers.Clear();
    }

    public override void _ExitTree()
    {
        StopBroadcast();
        StopListen();
    }

    public override void _Process(double delta)
    {
        _clock += delta;

        if (_tx != null)
        {
            _t -= (float)delta;
            if (_t <= 0f)
            {
                _t = 1f;
                try
                {
                    byte[] b = Encoding.UTF8.GetBytes(_payload);
                    _tx.Send(b, b.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
                }
                catch { /* ağ yok: sessizce geç */ }
            }
        }

        if (_rx != null)
        {
            try
            {
                while (_rx.Available > 0)
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _rx.Receive(ref ep);
                    string[] t = Encoding.UTF8.GetString(data).Split('|');
                    if (t.Length < 3 || t[0] != "RAENGINE1" || !int.TryParse(t[2], out int port)) continue;
                    string ip = ep.Address.ToString();
                    LanServer found = null;
                    foreach (var s in Servers) if (s.Ip == ip && s.Port == port) found = s;
                    if (found == null) { found = new LanServer { Ip = ip, Port = port }; Servers.Add(found); }
                    found.Name = t[1];
                    found.Seen = _clock;
                }
            }
            catch { /* tek paket hatası dinlemeyi durdurmasın */ }
            Servers.RemoveAll(s => _clock - s.Seen > 4.0);
        }
    }

    public static List<string> LocalAddresses()
    {
        var list = new List<string>();
        try
        {
            foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    list.Add(a.ToString());
        }
        catch { }
        return list;
    }
}
