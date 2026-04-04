using Godot;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public partial class WanLobbySys : Node
{
	[Export]
	private int _portMinimum = 7010;
	[Export]
	private int _portMaximum = 7000;

	public int currentPortOffset = 1;

	[Export]
	public string PublicIP;
	[Export]
	public string PrivateIP;

	[Export]
	public string LobbyServerIP;


	[Export]
	public bool IsWanLobbyServer;
	[Export]
	public bool IsWanLobbyConnected;


	bool UsePublic = false;
	bool UsePrivate = false;

	ENetMultiplayerPeer LobbySystemPeer;

	[Export]
	public TextEdit GameNameBox;
	[Export]
	public VBoxContainer ActiveGames;

	[Export]
	public Button CreateNewGame;

  
	public static WanLobbySys Instance;

	[Export]
	public MultiplayerSpawner WanSpawner;

	public override void _Ready()
	{
		base._Ready();
		
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectSuccess;
		Multiplayer.ConnectionFailed += OnConnectionFail;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
		string[] args = OS.GetCmdlineArgs();

		foreach (string arg in args)
		{
			if (arg == "MASTER")
			{
				CreateMasterServer();
			}
		}
		if(!IsWanLobbyConnected)
		{
			//This will find the correct IP address
			//Then connect to the lobby master.
			CheckIPAddresses();
		}
		Instance = this;

	}
	public async Task CheckIPAddresses()
	{

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
			LobbyServerIP = PublicIP;

		}
		else
		{
			GD.Print("The public IP failed to respond");
		}
		//-------------------If not public, ping Florida Poly for internal access.
		if (!UsePublic)
		{
			GD.Print("Trying Florida Poly Address: " + PrivateIP.ToString());
			pr = ping.Send(PrivateIP, timeout, buffer, po);
			await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
			GD.Print("Ping Return: " + pr.Status.ToString());
			if (pr.Status.ToString() == "Success")
			{
				GD.Print("The Florida Poly IP responded with a roundtrip time of: " + pr.RoundtripTime);
				UsePrivate = true;
				LobbyServerIP = PrivateIP;
			}
			else
			{
				LobbyServerIP = "127.0.0.1";
				GD.Print("The Florida Poly IP failed to respond");
				UsePrivate = false;
			}

		}
		if (JoinLobbyServer() != Error.Ok)
		{
			LobbyServerIP = "127.0.0.1";
			JoinLobbyServer();
		}
	}

	public void CreatNewGameServer()
	{
		if(GameNameBox.Text.Length < 2)
		{
			return;
		}
		//Create a new Game with a new process.

		if (IsWanLobbyServer)
		{
			try
			{
				System.Diagnostics.Process proc = new System.Diagnostics.Process();
				proc.StartInfo.UseShellExecute = true;
				string[] args = System.Environment.GetCommandLineArgs();

				if(OS.HasFeature("editor"))
				{
					GD.Print("Inside editor hint.");
					proc.StartInfo.FileName = args[0];
					GD.Print(args[0]);
					for (int i =1; i < args.Length; i++)
						{
						GD.Print(args[i]);
							proc.StartInfo.ArgumentList.Add(args[i]);    

						}
				}
				else
				{ 
					proc.StartInfo.FileName = args[0];

			  }
				proc.StartInfo.Arguments += "GAMESERVER " + currentPortOffset;
				GD.Print(proc.StartInfo.Arguments);
				//I need to think about this.
				//gameServers.Add(gameCounter, proc);
				currentPortOffset++;
				proc.Start();
			}
			catch (System.Exception e)
			{
				GD.Print("EXCEPTION - in creating a game!!! - " + e.ToString());
			}
		}
	}


	private Error JoinLobbyServer()
	{
		GD.Print($"LOBBY Attempting to connect to {LobbyServerIP}:{_portMinimum}");
		LobbySystemPeer = new ENetMultiplayerPeer();
		
		Error error = LobbySystemPeer.CreateClient(LobbyServerIP, _portMinimum);
		Multiplayer.MultiplayerPeer = LobbySystemPeer;
		if (error != Error.Ok)

			return error;

		GD.Print("Connected to server");
		
		IsWanLobbyConnected = true;
		return Error.Ok;
	}
	public Error CreateMasterServer()
	{ 
		 GD.Print("Attempting to create lobby system at port: "+ _portMinimum );
		LobbySystemPeer = new ENetMultiplayerPeer();
		
		Error err = LobbySystemPeer.CreateServer(_portMinimum, 1000);
		Multiplayer.MultiplayerPeer = LobbySystemPeer;
		if (err != Error.Ok)
		{
			GD.Print(err.ToString());
			return err;
		}
		GD.Print("Master Server Created!");
		IsWanLobbyConnected = true;
		IsWanLobbyServer = true;
		//GameNameBox.Hide();
		//CreateNewGame.Hide();
		return Error.Ok;
	}
	private void OnServerDisconnected()
	{
	   
	}

	private void OnConnectionFail()
	{
	   
	}

	private void OnConnectSuccess()
	{
		/*GD.Print("A");
		if (IsWanLobbyServer)
		{
			//WanSpawner
			//GD.Print("B");
			//WanSpawner.Spawn(0);
			/*var packedScene = GD.Load<PackedScene>(WanSpawner._SpawnableScenes[0]);
			var node = packedScene.Instantiate();
			GetNode(WanSpawner.SpawnPath).AddChild(node, true);
		}*/
	}

	private void OnPeerDisconnected(long id)
	{
	   
	}

	private void OnPeerConnected(long id)
	{
		GD.Print("A");
		if (IsWanLobbyServer)
		{
			//WanSpawner
			GD.Print("B");
			//WanSpawner.Spawn(0);
			var packedScene = GD.Load<PackedScene>(WanSpawner._SpawnableScenes[0]);
			
			var node = packedScene.Instantiate();
			
			GD.Print(WanSpawner.GetPath()+"/"+WanSpawner.SpawnPath);
			GetNode(WanSpawner.GetPath() + "/" + WanSpawner.SpawnPath).AddChild(node, true);
		}

	}
}
