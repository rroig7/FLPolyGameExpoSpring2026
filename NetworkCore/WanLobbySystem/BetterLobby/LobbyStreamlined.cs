using Godot;
using System;
using System.Text;
using System.Threading.Tasks;



[GlobalClass]
public partial class LobbyStreamlined : Node
	{



	[Export]
	public string PublicIP;
	[Export]
	public string PrivateIP;

	[Export]
	public int PortMinimum;

	[Export]
	private int portOffset = 1;

	public string LobbyServerIP;
	private bool UsePublic;
	private bool UsePrivate;
	private bool UseLocal;

	public bool IsWanLobbyConnected;
	[Export] public bool IsWanLobbyServer;

	public static LobbyStreamlined Instance;


	[Export]
	private MultiplayerSpawner AgentSpawner;


	private ENetMultiplayerPeer AgentPeer;

	private Godot.MultiplayerApi AgentAPI;

	private NodePath LobbyRootPath;

	[Export]
	public TextEdit GameNameBox;

	public string tempGameName;

	[Export]
	public float MaxGameTime = 30;
	
	public override void _Ready()
		{
		//D.Print(OS.GetExecutablePath());
		Instance = this;
		AgentAPI = MultiplayerApi.CreateDefaultInterface();
		GetTree().SetMultiplayer(AgentAPI, GetPath());
		LobbyRootPath = GetPath();
		AgentAPI.PeerConnected += OnPeerConnected;
		AgentAPI.PeerDisconnected += OnPeerDisconnected;

		string[] args = OS.GetCmdlineArgs();
		AgentSpawner.SpawnFunction = new Callable(this, nameof(SpawnAgent));
		bool isGameServer = false;

		GD.Print($"Command Line Arguments: {args}");

		foreach (string arg in args)
		{
			if (arg == "MASTER")
			{
				CreateMasterServer();
				
			}
			if(arg.Contains("GAMENAME"))
			{
				tempGameName = arg.Split('#')[1];
				isGameServer = true;
			}
		}

		if (!IsWanLobbyConnected)
		{
			GD.Print("Connecting the agent to the master!");
			//This will find the correct IP address
			//Then connect to the lobby master.
			if (!isGameServer)
			{
				GD.Print("Connecting agent to master server using IP Ping");
				CheckIPAddresses();
			}
			else
			{
				GD.Print("Connecting game server to local master.");
				LobbyServerIP = "127.0.0.1";
				JoinLobbyServer();
			}
		}



	}

	private void OnPeerConnected(long id)
	{
		if (IsWanLobbyServer)
		{
			AgentSpawner.Spawn(id);
			GD.Print("Spawning Agent");
			Rpc("UpdatePortOffset", portOffset);
		}
}

	private Node SpawnAgent(Variant d)
	{
		long peerId = (long)d;
		var packedScene = GD.Load<PackedScene>(AgentSpawner._SpawnableScenes[0]);
		var node = packedScene.Instantiate();
		node.SetMultiplayerAuthority((int)peerId, true);
		return node;
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print($"Agent disconnected: {id}");

		// Only the server should clean up
		if (!IsWanLobbyServer)
			return;

		// Get the container where agents are spawned
		Node spawnRoot = GetNode(AgentSpawner.GetPath() + "/" + AgentSpawner.SpawnPath);

		foreach (Node child in spawnRoot.GetChildren())
		{
			if (child.GetMultiplayerAuthority() == id)
			{
				GD.Print($"Freeing agent owned by {id}");
				child.QueueFree();
			}
		}
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
					LobbyServerIP = PrivateIP;
				}
				else
				{
					LobbyServerIP = "127.0.0.1";
					GD.Print("The Private IP failed to respond");
					UsePrivate = false;
				}
			}
		}
		if (JoinLobbyServer() != Error.Ok)
		{
			LobbyServerIP = "127.0.0.1";
			JoinLobbyServer();
		}
	}


	private Error JoinLobbyServer()
	{
		GD.Print($"LOBBY Attempting to connect to {LobbyServerIP}:{PortMinimum}");
		AgentPeer = new ENetMultiplayerPeer();

		Error error = AgentPeer.CreateClient(LobbyServerIP, PortMinimum);
		AgentAPI.MultiplayerPeer = AgentPeer;
		if (error != Error.Ok)
			return error;

		GD.Print("Connected to MASTER");

		IsWanLobbyConnected = true;
		return Error.Ok;
	}
	public Error CreateMasterServer()
	{
		GD.Print("Attempting to create lobby system at port: " + PortMinimum);
		AgentPeer = new ENetMultiplayerPeer();

		Error err = AgentPeer.CreateServer(PortMinimum, 1000);
		AgentAPI.MultiplayerPeer = AgentPeer;
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

	public override void _Process(double delta)
		{   
			base._Process(delta);
			AgentAPI.Poll();
		if (!IsWanLobbyServer)
			{ UpdateVBoxChildren((VBoxContainer)GetNode(AgentSpawner.GetPath() + "/" + AgentSpawner.SpawnPath)); }

		
		if (GenericCore.Instance.IsGenericCoreConnected || IsWanLobbyServer)
			{
				((Control)GetChild(0)).Visible = false;
				foreach(Node n in GenericCore.Instance.GetChildren())
				{
					if(IsWanLobbyServer)
					{
						if (n is CanvasItem canvasItem)
						{
							canvasItem.Visible = false;
						}
						else if (n is Node3D node3D)
						{
							node3D.Visible = false;
						}
						else if (n is CanvasLayer canvasLayer)
						{
							canvasLayer.Visible = false;
						}						
					}

				}
			}
		else
			{
			((Control)GetChild(0)).Visible = true;
			foreach (Node n in GenericCore.Instance.GetChildren())
			{
				if (n is CanvasItem canvasItem)
				{
					canvasItem.Visible = true;
				}
				else if (n is Node3D node3D)
				{
					node3D.Visible = true;
				}
				else if (n is CanvasLayer canvasLayer)
				{
					canvasLayer.Visible = true;
				}
			}
			
		}
		
	}

	private void UpdateVBoxChildren(VBoxContainer vbox)
	{
		foreach (Node c in vbox.GetChildren())
		{
			if ((c is Control))
			{
				Control child = (Control)c;

				// Find the button inside the child (assume it's direct child)
				Button btn = child.GetNode<Button>("Button");

				if (btn != null && btn.Visible)
				{
					// Make the child 32 pixels high
					//child.CustomMinimumSize = new Vector2(0, 32);
					//child.Visible = true;
				}
				else
				{
					// Collapse the child
					//child.CustomMinimumSize = Vector2.Zero;
					//child.Visible = false; // optional if you want full collapse
				}
			}
		}

		// Force the container to recalc layout
		vbox.QueueSort();
	}


	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ProcessSpawnServerSide(String n)
	{
		if (IsWanLobbyServer)
		{
			try
			{
				System.Diagnostics.Process proc = new System.Diagnostics.Process();
				proc.StartInfo.UseShellExecute = true;     
				string[] args = OS.GetCmdlineArgs(); ;
				proc.StartInfo.FileName = OS.GetExecutablePath();
				proc.StartInfo.Arguments += "--headless GAMESERVER " +(PortMinimum+ portOffset) + " GAMENAME#"+n+" > "+n+".log";
				GD.Print("Starting Game Server With: "+proc.StartInfo.Arguments);
				portOffset++;
				Rpc("UpdatePortOffset", portOffset);
				proc.Start();
				if (MaxGameTime > 0)
				{
					GameMonitor(proc);
				}
			}
			catch (System.Exception e)
			{
				GD.Print("EXCEPTION - in creating a game!!! - " + e.ToString());
			}
		}
	}
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void UpdatePortOffset(int p)
	{
		if (!IsWanLobbyServer)
		{
			portOffset = p;
		}
	}
	public async void GameMonitor(System.Diagnostics.Process proc)
	{
		await ToSignal(GetTree().CreateTimer(MaxGameTime), SceneTreeTimer.SignalName.Timeout);
		if(!proc.HasExited)
		{
			proc.Kill();
		}
	}

	public void CreatNewGameServer()
	{
		if (GameNameBox.Text.Length < 2)
		{
			return;
		}
		int currentPort = portOffset;
		
		RpcId(1, "ProcessSpawnServerSide", GameNameBox.Text.Replace(' ','-').Replace('\n','-').Replace('#','-'));
		WaitForGameToStart(portOffset);
	}
	public async void WaitForGameToStart(int p)
	{
		GenericCore.Instance.SetPort((p + PortMinimum).ToString());
		GenericCore.Instance.SetIP(LobbyServerIP);
		while (p == portOffset)
		{
			await ToSignal(GetTree().CreateTimer(.1f), SceneTreeTimer.SignalName.Timeout);
		}
		await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
		GenericCore.Instance.JoinGame();
	}
	public void DisconnectFromLobbySystem()
	{
		if (AgentAPI.MultiplayerPeer != null)
		{
			GD.Print("Disconnecting from ENet session<Lobby>");

			// Close the connection
			AgentAPI.MultiplayerPeer.Close();

			// Remove the peer from the Multiplayer API
			AgentAPI.MultiplayerPeer = null;

		}
	}
}
