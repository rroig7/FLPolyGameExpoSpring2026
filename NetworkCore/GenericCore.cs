using Godot;
using Godot.Collections;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[GlobalClass]
[Tool]
public partial class GenericCore : Node
{


	/// <summary>
	/// Alerts the rest of the local instance that a new Client joined the network
	/// </summary>
	[Signal]
	public delegate void ClientConnectedEventHandler(long peerId, Dictionary<string, string> peerInfo);

	/// <summary>
	/// Alerts the rest of the local instance that an existing Client left the network
	/// </summary>
	[Signal]
	public delegate void ClientDisconnectedEventHandler(long peerId);

	/// <summary>
	/// Alerts the local user that the server was not found
	/// </summary>
	[Signal]
	public delegate void ClientFailedEventHandler(Error error);

	/// <summary>
	/// Signal the rest of the local instance that they are now the server
	/// </summary>
	[Signal]
	public delegate void ServerCreatedEventHandler(Dictionary<string, string> serverInfo);

	/// <summary>
	/// Alerts the rest of the local instance that the server disconnected
	/// </summary>
	[Signal]
	public delegate void ServerDisconnectedEventHandler();

	/// <summary>
	/// Alerts the local user that the server could not be made
	/// </summary>
	[Signal]
	public delegate void ServerFailedEventHandler(Error error);

	/// <summary>
	/// Signal the rest of the local instance that they have connected to the server
	/// </summary>
	[Signal]
	public delegate void LocalConnectedEventHandler();

	/// <summary>
	/// Which Port is the server/client connecting to
	/// </summary>
	private int _localPort = 7000;

	private int _portMinimum = 7010;
	private int _portMaximum = 7000;


	[Export]
	public string PublicIP;
	[Export]
	public string PrivateIP;
	/// <summary>
	/// Which IP is the server/client connecting to
	/// </summary>
	private string _serverAddress = "127.0.0.1";

	/// <summary>
	/// How many connections can one sever hold
	/// </summary>
	private int _maxConnections = 4;

	/// <summary>
	/// List of all the Peers in the network including server
	/// </summary>
	public Dictionary<long, Dictionary<string, string>> _peers = new();

	private Dictionary<string, string> _localInfo = new()
	{
		{ "NetID", "1" }
	};

	[Export]
	public Dictionary<int, NetID> _netObjects = new();


	[Export]
	public NetworkCore MainNetworkCore;
	
	public  uint _netObjectsCount;

	private Godot.Collections.Array<Node> nodesForErase = new Godot.Collections.Array<Node>();

	public static GenericCore Instance { get; private set; }

	[Export]
	public bool IsServer;
	[Export]
	public bool IsGenericCoreConnected;

	[Export]
	public bool IsListening = true;

	public long GetServerNetId()
	{

		return _peers.First().Key;
	}


	public override void _EnterTree()
	{
		base._EnterTree();
		GD.Print("Instance static variable set!");
		Instance ??= this;
	}
	public override void _Ready()
	{
		base._Ready();
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectSuccess;
		Multiplayer.ConnectionFailed += OnConnectionFail;
		Multiplayer.ServerDisconnected += OnServerDisconnected;    
		if (_portMinimum > _portMaximum)
			(_portMinimum, _portMaximum) = (_portMaximum, _portMinimum);
		string[] args = OS.GetCmdlineArgs();
		for(int i =0; i < args.Count(); i++)
		{
			if (args[i] == "GAMESERVER")
			{
				SetPort(args[i + 1]);
				CreateLocalGame();
				break;
			}
		}
	}
	public int GetPort()
	{
		return _localPort;
	}
	public void SetPort(string s)
	{
		try
		{
			_localPort = int.Parse(s);
		}
		catch (Exception ex) { }
	}
	public void SetIP(string s)
	{
		_serverAddress = s;
	}

	public void JOIN_WAN_CALLBACK()
	{
		_localPort = _portMinimum;
		JoinWan();
	}

	public async Task JoinWan()
	{
		bool UsePublic = false;
		bool UsePrivate = false;
		GD.Print("Attempting to connect to public IP.");
		//Ping Public Ip address to see if we are external..........
		GD.Print("Trying Public IP Address: " + PublicIP.ToString());
		System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
		System.Net.NetworkInformation.PingOptions po = new System.Net.NetworkInformation.PingOptions();
		po.DontFragment = true;
		string data = "HELLLLOOOOO!";
		byte[] buffer = ASCIIEncoding.ASCII.GetBytes(data);
		int timeout = 500;
		System.Net.NetworkInformation.PingReply pr = ping.Send(PublicIP, timeout, buffer, po);
		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
		GD.Print("Ping Return: " + pr.Status.ToString());
		if (pr.Status == System.Net.NetworkInformation.IPStatus.Success)
		{
			GD.Print("The public IP responded with a roundtrip time of: " + pr.RoundtripTime);
			UsePublic = true;
			_serverAddress = PublicIP;
		}
		else
		{
			GD.Print("The public IP failed to respond");        
			//-------------------If not public, ping Florida Poly for internal access.
			if (!UsePublic)
			{
				GD.Print("Trying Private Address: " + PrivateIP.ToString());
				pr = ping.Send(PrivateIP, timeout, buffer, po);
				await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
				GD.Print("Ping Return: " + pr.Status.ToString());
				if (pr.Status.ToString() == "Success")
				{
					GD.Print("The Private IP responded with a roundtrip time of: " + pr.RoundtripTime);
					UsePrivate = true;
					_serverAddress = PrivateIP;
				}
				else
				{
					_serverAddress = "127.0.0.1";
					GD.Print("The Private IP failed to respond");
					UsePrivate = false;
				}
			}
		}
		if (JoinGame() != Error.Ok)
		{
			_serverAddress = "127.0.0.1";
			JoinGame();
		}
	}

	public Error JoinGame()
	{
		GD.Print($"Attempting to connect to {_serverAddress}:{_localPort}");
		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateClient(_serverAddress, _localPort);
		if (error != Error.Ok)

			return error;

		GD.Print("Connected to server");
		Multiplayer.MultiplayerPeer = peer;
		IsGenericCoreConnected = true;
		return Error.Ok;
	}
	public void DisconnectFromGame()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			GD.Print("Disconnecting from ENet session<Game Server>");

			// Close the connection
			Multiplayer.MultiplayerPeer.Close();

			// Remove the peer from the Multiplayer API
			Multiplayer.MultiplayerPeer = null;
			_peers.Clear();
			_netObjects.Clear();
			_netObjectsCount = 0;

			IsGenericCoreConnected = false;
			IsServer = false;
		}
	}

	public void StopListening()
	{
		if (!IsServer) return;

		IsListening = false;
		if( LobbyStreamlined.Instance != null)//Node.IsInstanceValid(LobbyStreamlined.Instance))
		{
			LobbyStreamlined.Instance.DisconnectFromLobbySystem();
		}

	}

	private Error CreateLocalGame()
	{
		GD.Print($"Attempting to create game server at {_localPort}");
		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateServer(_localPort, _maxConnections);
		if (error != Error.Ok)
		{
			EmitSignalServerFailed(error);
			return error;
		}

		GD.Print("Created Local Game");
		Multiplayer.MultiplayerPeer = peer;
		_peers[1] = _localInfo;

		//CheckForObjectsOnScene(GetTree().Root);
		EmitSignalServerCreated(_localInfo);
		IsServer = true;
		IsGenericCoreConnected = true;
		//_peers.Clear();
		IsListening = true;
		return Error.Ok;
	}



	/// <summary>
	/// Sends a message to the rest of the clients to register this player.
	/// </summary>
	/// <param name="id"></param>
	private void OnPeerConnected(long id)
	{
		if(GenericCore.Instance.IsServer && !IsListening)return;
		RpcId(id, MethodName.RegisterPeer, _localInfo);
		EmitSignalLocalConnected();
		GD.Print("Client Connected!");
	}

	/// <summary>
	/// Sends a message to the local instance that a client disconnected<br/>
	/// Also removes player from connected peers table
	/// </summary>
	/// <param name="id"></param>
	private void OnPeerDisconnected(long id)
	{
		_peers.Remove(id);
		//Need to destroy objects.
		EmitSignalClientDisconnected(id);
		if (!Multiplayer.IsServer()) return;

	}

	/// <summary>
	/// Called when successfully connected to the network on the local side
	/// </summary>
	private void OnConnectSuccess()
	{
		
		GD.Print("Client callback incoming!");
		int peerId = Multiplayer.GetUniqueId();
		_peers[peerId] = _localInfo;
		EmitSignalClientConnected(peerId, _localInfo);
	}

	/// <summary>
	/// Called when failing to connect to the network on local
	/// </summary>
	// Basically reassuring that the MultiplayerPeer is null
	private void OnConnectionFail()
	{
		Multiplayer.MultiplayerPeer = null;
	}

	/// <summary>
	/// Called when Server is closed <br/>
	/// Makes sure that there is no more <see cref="MultiplayerApi.MultiplayerPeer"/>
	/// </summary>
	private void OnServerDisconnected()
	{
		Multiplayer.MultiplayerPeer = null;
		_peers.Clear();
		EmitSignalServerDisconnected();
	}

	/// <summary>
	/// This function is called from local but not run on local<br/>
	/// This function tells the other peers (including server) that they joined the network.
	/// </summary>
	/// <param name="peerInfo"></param>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RegisterPeer(Dictionary<string, string> peerInfo)
	{
		int newPeerId = Multiplayer.GetRemoteSenderId(); //Who is sending to call this function
		if (newPeerId == 1 && !Multiplayer.IsServer())
			peerInfo["NetID"] = GetServerNetId().ToString();
		else
			peerInfo["NetID"] = newPeerId.ToString(); //Updating the dictionary for the new player
		_peers[newPeerId] = peerInfo;
		EmitSignalClientConnected(newPeerId, peerInfo); //Update local instance to new player
	}


	public void RegisterObject(NetID netId)
	{   
		netId.netObjectID = GenericCore.Instance._netObjectsCount;
		GenericCore.Instance._netObjects.Add((int)(GenericCore.Instance._netObjectsCount++), netId);
	}

	public override Array<Dictionary> _GetPropertyList()
	{
		var propList = new Array<Dictionary>();

		propList.AddRange([
			new()
			{
				{ "name", "_portMaximum" },
				{ "type", (int)Variant.Type.Int },
				{ "usage", (int)(PropertyUsageFlags.Default) },
				{ "hint", (int)PropertyHint.Range },
				{ "hint_string", "0,65535,1,hide_slider" }
			},
			new()
			{
				{ "name", "_portMinimum" },
				{ "type", (int)Variant.Type.Int },
				{ "usage", (int)(PropertyUsageFlags.Default) },
				{ "hint", (int)PropertyHint.Range },
				{ "hint_string", "0,65535,1,hide_slider" }
			},
			new()
			{
				{ "name", "_localPort" },
				{ "type", (int)Variant.Type.Int },
				{ "usage", (int)(PropertyUsageFlags.Default) },
				{ "hint", (int)PropertyHint.Range },
				{ "hint_string", "0,65535,1,hide_slider" }
			},
			new Dictionary()
			{
				{ "name", "_serverAddress" },
				{ "type", (int)Variant.Type.String },
				{ "usage", (int)(PropertyUsageFlags.Default) },
			},
			new Dictionary()
			{
				{ "name", "_maxConnections" },
				{ "type", (int)Variant.Type.Int },
				{ "usage", (int)(PropertyUsageFlags.Default) },
				{ "hint", (int)PropertyHint.Range },
				{ "hint_string", "0,100,1,or_greater,hide_slider" }
			},
			new Dictionary()
			{
				{ "name", "_peers" },
				{ "type", (int)Variant.Type.Dictionary },
				{ "usage", (int)(PropertyUsageFlags.ReadOnly | PropertyUsageFlags.Editor) },
				{ "hint", (int)(PropertyHint.TypeString) },
				{
					"hint_string",
					$"{Variant.Type.Int:D}:; {Variant.Type.Dictionary:D}/{PropertyHint.DictionaryType:D}:"
				}
			},
		]);

		return propList;
	}

}
