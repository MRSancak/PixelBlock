using System.Collections.Generic;
using Godot;
using PixelBlock.Animals;
using PixelBlock.Core;
using PixelBlock.Farming;

namespace PixelBlock.Network;

public enum NetRole { None, Host, Client }

public sealed partial class NetworkManager : Node
{
    public const int Port = 24565;
    public static NetworkManager Instance { get; private set; }

    public NetRole Role { get; private set; } = NetRole.None;
    public bool Active => Role != NetRole.None;
    public GameSession Session { get; private set; }
    public LanDiscovery Lan { get; private set; }

    public System.Action OnConnectFailed;
    public System.Action OnServerLost;
    public System.Action<int, float, int, string, string> OnWorldInfo;
    public System.Action<string> OnMessage;

    private ENetMultiplayerPeer _peer;
    private bool _hooked;
    private readonly Dictionary<int, RemotePlayer> _remotes = new();
    private readonly Dictionary<int, string> _profiles = new();
    private float _sendT, _timeT, _animT, _cropT, _invT;
    private bool _invDirty;
    private readonly Dictionary<string, string> _peerInv = new();

    public Dictionary<string, string> ExportPeerInv() => new Dictionary<string, string>(_peerInv);

    public void ImportPeerInv(Dictionary<string, string> inv)
    {
        _peerInv.Clear();
        if (inv != null) foreach (var kv in inv) _peerInv[kv.Key] = kv.Value;
    }

    private string NameOfPeer(int id) =>
        _profiles.TryGetValue(id, out var p) ? CharacterProfile.Decode(p).PlayerName : "Oyuncu";

    public void MarkInventoryDirty() => _invDirty = true;

    public void SendAnimalHit(int netId, float dmg)
    {
        if (Role == NetRole.Client && IsLinked) RpcId(1, nameof(ServerAnimalHit), netId, dmg);
    }

    public void SendFarmUse(Vector3I soil, string heldName)
    {
        if (Role == NetRole.Client && IsLinked) RpcId(1, nameof(ServerFarmUse), soil.X, soil.Y, soil.Z, heldName);
    }

    public void GiveItem(int peer, string item, int n)
    {
        if (Role != NetRole.Host || peer <= 1 || n <= 0) return;
        RpcId(peer, nameof(ClientGive), item, n);
    }

    public override void _Ready()
    {
        Instance = this;
        Lan = new LanDiscovery { Name = "Lan" };
        AddChild(Lan);
        NetBridge.LocalBlockChanged = OnLocalBlock;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        NetBridge.LocalBlockChanged = null;
    }

    public Error StartHost(string hostName)
    {
        Disconnect();
        _peer = new ENetMultiplayerPeer();
        Error err = _peer.CreateServer(Port, 7);
        if (err != Error.Ok) { _peer = null; return err; }
        Multiplayer.MultiplayerPeer = _peer;
        Role = NetRole.Host;
        Hook();
        Lan.StartBroadcast(hostName, Port);
        return Error.Ok;
    }

    public Error Join(string address)
    {
        Disconnect();
        _peer = new ENetMultiplayerPeer();
        Error err = _peer.CreateClient(address, Port);
        if (err != Error.Ok) { _peer = null; return err; }
        Multiplayer.MultiplayerPeer = _peer;
        Role = NetRole.Client;
        Hook();
        return Error.Ok;
    }

    public void AttachSession(GameSession s) => Session = s;

    public void Disconnect()
    {
        Lan?.StopBroadcast();
        if (_hooked)
        {
            Multiplayer.PeerConnected -= OnPeerConnected;
            Multiplayer.PeerDisconnected -= OnPeerDisconnected;
            Multiplayer.ConnectedToServer -= OnConnectedToServer;
            Multiplayer.ConnectionFailed -= OnConnectionFailed;
            Multiplayer.ServerDisconnected -= OnServerDisconnected;
            _hooked = false;
        }
        if (_peer != null)
        {
            _peer.Close();
            Multiplayer.MultiplayerPeer = null;
            _peer = null;
        }
        foreach (var rp in _remotes.Values)
            if (IsInstanceValid(rp)) rp.QueueFree();
        _remotes.Clear();
        _profiles.Clear();
        Role = NetRole.None;
        Session = null;
    }

    private void Hook()
    {
        if (_hooked) return;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
        _hooked = true;
    }

    private bool IsLinked =>
        _peer != null && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

    private void OnPeerConnected(long id) { }

    private void OnPeerDisconnected(long id)
    {
        int pid = (int)id;
        string who = _remotes.TryGetValue(pid, out var rp) ? rp.PlayerName : "Bir oyuncu";
        RemoveRemote(pid);
        _profiles.Remove(pid);
        if (Role == NetRole.Host)
        {
            Rpc(nameof(ClientPlayerLeft), pid);
            OnMessage?.Invoke($"{who} oyundan ayrıldı");
        }
    }

    private void OnConnectedToServer()
    {
        RpcId(1, nameof(ServerHello), CharacterProfile.Current.Encode());
    }

    private void OnConnectionFailed()
    {
        Disconnect();
        OnConnectFailed?.Invoke();
    }

    private void OnServerDisconnected()
    {
        Disconnect();
        OnServerLost?.Invoke();
    }

    public void SendReady() => RpcId(1, nameof(ServerReady));

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ServerHello(string appearance)
    {
        if (!Multiplayer.IsServer() || Session == null) return;
        int id = Multiplayer.GetRemoteSenderId();
        _profiles[id] = appearance;
        int day = SeasonClock.Instance != null ? SeasonClock.Instance.DayCount : 0;
        string savedInv = _peerInv.TryGetValue(CharacterProfile.Decode(appearance).PlayerName, out var e) ? e : "";
        RpcId(id, nameof(ClientWorldInfo), Session.World.Seed, Session.Env.TimeOfDay, day, CharacterProfile.Current.PlayerName, savedInv);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ServerReady()
    {
        if (!Multiplayer.IsServer() || Session == null) return;
        int id = Multiplayer.GetRemoteSenderId();

        var buf = new List<int>();
        foreach (var (pos, bid) in Session.World.AllEdits())
        {
            buf.Add(pos.X); buf.Add(pos.Y); buf.Add(pos.Z); buf.Add(bid);
            if (buf.Count >= 3000)
            {
                RpcId(id, nameof(ClientEdits), Variant.From(buf.ToArray()));
                buf.Clear();
            }
        }
        if (buf.Count > 0) RpcId(id, nameof(ClientEdits), Variant.From(buf.ToArray()));

        RpcId(id, nameof(ClientPlayerJoined), 1, CharacterProfile.Current.Encode());
        foreach (var kv in _profiles)
            if (kv.Key != id) RpcId(id, nameof(ClientPlayerJoined), kv.Key, kv.Value);
        string app = _profiles.TryGetValue(id, out var a) ? a : "";
        Rpc(nameof(ClientPlayerJoined), id, app);

        AddRemote(id, app);
        OnMessage?.Invoke($"{CharacterProfile.Decode(app).PlayerName} oyuna katıldı");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ServerBlock(int x, int y, int z, int id)
    {
        if (!Multiplayer.IsServer()) return;
        int sender = Multiplayer.GetRemoteSenderId();
        ApplyRemoteBlock(x, y, z, (ushort)id);
        Rpc(nameof(ClientBlock), x, y, z, id, sender);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ServerAnimalHit(int netId, float dmg)
    {
        if (!Multiplayer.IsServer()) return;
        int sender = Multiplayer.GetRemoteSenderId();
        Vector3 from = _remotes.TryGetValue(sender, out var rp) ? rp.GlobalPosition : Vector3.Zero;
        AnimalDirector.Instance?.HitByNetId(netId, dmg, from, sender);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ServerFarmUse(int x, int y, int z, string heldName)
    {
        if (!Multiplayer.IsServer()) return;
        FarmManager.Instance?.UseRemote(new Vector3I(x, y, z), heldName, Multiplayer.GetRemoteSenderId());
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ServerInventory(string enc)
    {
        if (!Multiplayer.IsServer()) return;
        _peerInv[NameOfPeer(Multiplayer.GetRemoteSenderId())] = enc;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void ServerState(Vector3 pos, float yaw, float pitch, int flags)
    {
        if (!Multiplayer.IsServer()) return;
        int id = Multiplayer.GetRemoteSenderId();
        UpdateRemote(id, pos, yaw, pitch, flags);
        Rpc(nameof(ClientState), id, pos, yaw, pitch, flags);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClientWorldInfo(int seed, float time, int day, string hostName, string savedInv)
    {
        OnWorldInfo?.Invoke(seed, time, day, hostName, savedInv);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClientGive(string item, int n)
    {
        if (Session?.Inventory == null) return;
        ushort id = BlockRegistry.IdOf(item);
        if (id != 0) Session.Inventory.Add(id, n);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void ClientAnimals(float[] data)
    {
        AnimalDirector.Instance?.ApplyNet(data);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClientCrops(string snapshot)
    {
        FarmManager.Instance?.ApplySnapshot(snapshot);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClientEdits(int[] flat)
    {
        if (Session?.World == null || flat == null) return;
        for (int i = 0; i + 3 < flat.Length; i += 4)
            ApplyRemoteBlock(flat[i], flat[i + 1], flat[i + 2], (ushort)flat[i + 3]);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClientBlock(int x, int y, int z, int id, int sender)
    {
        if (sender == Multiplayer.GetUniqueId()) return;
        ApplyRemoteBlock(x, y, z, (ushort)id);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClientPlayerJoined(int id, string appearance)
    {
        if (id == Multiplayer.GetUniqueId()) return;
        AddRemote(id, appearance);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ClientPlayerLeft(int id)
    {
        RemoveRemote(id);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void ClientState(int id, Vector3 pos, float yaw, float pitch, int flags)
    {
        if (id == Multiplayer.GetUniqueId()) return;
        UpdateRemote(id, pos, yaw, pitch, flags);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void ClientTime(float timeOfDay, int day, int weather)
    {
        if (Session?.Env == null) return;
        Session.Env.TimeOfDay = timeOfDay;
        if (SeasonClock.Instance != null) SeasonClock.Instance.DayCount = day;
        Session.Env.SetWeather((Weather)weather);
    }

    private void AddRemote(int id, string appearance)
    {
        if (Session == null) return;
        RemoveRemote(id);
        var rp = new RemotePlayer();
        Session.AddChild(rp);
        rp.Setup(id, CharacterProfile.Decode(appearance));
        _remotes[id] = rp;
    }

    private void RemoveRemote(int id)
    {
        if (_remotes.TryGetValue(id, out var rp))
        {
            if (IsInstanceValid(rp)) rp.QueueFree();
            _remotes.Remove(id);
        }
    }

    private void UpdateRemote(int id, Vector3 pos, float yaw, float pitch, int flags)
    {
        if (_remotes.TryGetValue(id, out var rp)) rp.SetState(pos, yaw, pitch, flags);
    }

    private void ApplyRemoteBlock(int x, int y, int z, ushort id)
    {
        var w = Session?.World;
        if (w == null) return;
        var c = VoxelWorld.ChunkCoord(x, y, z);
        if (w.HasChunk(c)) w.SetBlock(x, y, z, id);
        else w.PreloadEdits(new[] { (x, y, z, id) });
    }

    private void OnLocalBlock(Vector3I cell, ushort id)
    {
        if (!Active || !IsLinked) return;
        if (Role == NetRole.Host) Rpc(nameof(ClientBlock), cell.X, cell.Y, cell.Z, (int)id, 1);
        else RpcId(1, nameof(ServerBlock), cell.X, cell.Y, cell.Z, (int)id);
    }

    public override void _Process(double delta)
    {
        if (!Active || Session?.Player == null || !IsLinked) return;
        float dt = (float)delta;

        _sendT -= dt;
        if (_sendT <= 0f)
        {
            _sendT = 0.05f;
            var p = Session.Player;
            bool mouse = Input.MouseMode == Input.MouseModeEnum.Captured;
            int flags = 0;
            if (p.Crouching) flags |= 1;
            if (Input.IsActionPressed(GameInput.Actions.Sprint)) flags |= 2;
            if (mouse && (Input.IsActionPressed(GameInput.Actions.Primary) || Input.IsActionPressed(GameInput.Actions.Secondary))) flags |= 4;
            if (p.IsOnFloor() || p.Creative) flags |= 8;
            float yaw = p.Rotation.Y;
            float pitch = p.Head.Rotation.X;
            if (Role == NetRole.Host) Rpc(nameof(ClientState), 1, p.GlobalPosition, yaw, pitch, flags);
            else RpcId(1, nameof(ServerState), p.GlobalPosition, yaw, pitch, flags);
        }

        if (Role == NetRole.Client && _invDirty)
        {
            _invT -= dt;
            if (_invT <= 0f && Session.Inventory != null)
            {
                _invT = 1.5f;
                _invDirty = false;
                RpcId(1, nameof(ServerInventory), Session.Inventory.Encode());
            }
        }

        if (Role == NetRole.Host && Multiplayer.GetPeers().Length > 0)
        {
            _animT -= dt;
            if (_animT <= 0f && AnimalDirector.Instance != null)
            {
                _animT = 0.1f;
                Rpc(nameof(ClientAnimals), Variant.From(AnimalDirector.Instance.ExportNet()));
            }
            _cropT -= dt;
            if (_cropT <= 0f && FarmManager.Instance != null)
            {
                _cropT = 2f;
                Rpc(nameof(ClientCrops), FarmManager.Instance.ExportSnapshot());
            }
        }

        if (Role == NetRole.Host)
        {
            _timeT -= dt;
            if (_timeT <= 0f)
            {
                _timeT = 4f;
                int day = SeasonClock.Instance != null ? SeasonClock.Instance.DayCount : 0;
                Rpc(nameof(ClientTime), Session.Env.TimeOfDay, day, (int)Session.Env.Weather);
            }
        }
    }
}
