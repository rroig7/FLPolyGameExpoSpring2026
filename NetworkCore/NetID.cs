using Godot;

[GlobalClass]
[Tool]
public partial class NetID : MultiplayerSynchronizer
{
	[Export] public bool IsLocal;
	[Export] public bool IsServer;
	[Export] public long OwnerId;
	[Export] public uint netObjectID;
	[Export] public NetworkCore _myNetworkCore;
	[Export] public bool IsNetworkReady = false;
	[Export] public bool IsSynced = false;

	/// <summary>
	/// Will emit once net ID is valid to use.
	/// </summary>
	[Signal]
	public delegate void NetIDReadyEventHandler();
	public override void _EnterTree()
	{
		base._EnterTree();
		Name = "MultiplayerSynchronizer";

		if (ReplicationConfig == null)
		{
			GD.Print("No replication config found, creating one.");
			ReplicationConfig = new SceneReplicationConfig();
		}

		var config = ReplicationConfig as SceneReplicationConfig;
		if (config == null)
		{
			GD.PushError("ReplicationConfig is not a SceneReplicationConfig!");
			return;
		}
		if (!config.HasProperty("MultiplayerSynchronizer:IsNetworkReady"))
		{
			config.AddProperty("MultiplayerSynchronizer:IsNetworkReady");
		}
		if (!config.HasProperty("MultiplayerSynchronizer:IsSynced"))
		{
			config.AddProperty("MultiplayerSynchronizer:IsSynced");
		}
		if (!config.HasProperty("MultiplayerSynchronizer:OwnerId"))
		{
			config.AddProperty("MultiplayerSynchronizer:OwnerId");
		}
	}

	public override void _Ready()
	{
		base._Ready();
		Synchronized += NetID_Synchronized;
		slowStart();
		
	}

	private void NetID_Synchronized()
	{
		IsSynced = true;
	}

	public async void slowStart()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		while (GenericCore.Instance == null || !GenericCore.Instance.IsGenericCoreConnected)
		{
			await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);

		}
		if(GenericCore.Instance.IsServer && OwnerId ==0)
		{
			OwnerId = 1;
			if (GenericCore.Instance.GetServerNetId() == OwnerId)
			IsLocal = true;  
			SetMultiplayerAuthority(1); // 1 = server
			IsNetworkReady = true;
			//IsSynced = true;
		}
	   //There is a problem with this ---- There is no way to know if it was created by spawner or 
	   //Drag and Drop.
		await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
		if (!GenericCore.Instance.IsServer)
		{
			for(int i =0; i < 10; i ++)
			{
				await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
				if(IsSynced)
				{
					break;
				}
			}
			if (!IsSynced)
			{
				GD.Print("Deleting the inscene object: " + GetParent().Name);
		
				GetParent().QueueFree();
			}
			else
			{
				IsNetworkReady = true;
			}
		}
	   
		EmitSignalNetIDReady();
		//Emit a signal.
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void Initialize(long peerIdOwner)
	{
	   
		OwnerId = peerIdOwner;
		if (peerIdOwner == 1)
			IsServer = true;
		if (GenericCore.Instance.GetServerNetId() == OwnerId)
			IsLocal = true;
	}

	~NetID()
	{

		if (Multiplayer.IsServer())
		{
			GD.Print("Destroying a network object from the destructor. "+Name);
			_myNetworkCore.NetDestroyObject(this);
		}
	}


	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public async void ManualDelete()
	{
		GD.Print("Trying to remote destroy an object: " + GetParent().Name);
		if(ReplicationConfig != null)
		{
			try
			{
				ReplicationConfig = null;
			}
			catch {//Stop stupid chatty error
				   }
		}
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GetParent().QueueFree();
	}
}
