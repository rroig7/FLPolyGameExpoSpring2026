using Godot;
using System;
using System.Threading.Tasks;

[GlobalClass]
[Tool]
public partial class NetworkCore : MultiplayerSpawner
{
	[Export]
	public bool SpawnInexZeroOnConnect;

	[Signal]
	public delegate void ExposedClientConnectedEventHandler(long peerId, Godot.Collections.Dictionary<string, string> peerInfo);

	[Signal]
	public delegate void ExposedClientDisconnectedEventHandler(long peerId);



	public override void _Ready()
	{

		base._Ready();

		slowStart();
  
	}

	public async void slowStart()
	{

		while (GenericCore.Instance == null)
		{
			await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
		}
		GenericCore.Instance.ClientDisconnected += OnClientDisconnected;
		GenericCore.Instance.ClientConnected += OnClientConnected;
		GD.Print(GenericCore.Instance.Name);
		
	}

	/// <summary>
	/// When Called it will spawn the indexed scene in the spawn area.
	/// </summary>
	/// <param name="index">The Scene we want to spawn in the spawn array</param>
	/// <param name="owner">The Owner of the spawned Scene, default is the server</param>
	/// <typeparam name="T">The type of node that the scene root is</typeparam>
	/// <returns>The Scene Node</returns>
	//[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public Node NetCreateObject(int index, Vector3 initialPosition, Quaternion rotation, long owner = -1L)
	{
		GD.Print("Spawning NPMs");
		if (!Multiplayer.IsServer())
			return null;
		var packedScene = GD.Load<PackedScene>(_SpawnableScenes[index]);
		var node = packedScene.Instantiate();
		GetNode(SpawnPath).AddChild(node, true);

		if (node is Node2D)
		{
			((Node2D)node).GlobalPosition = new Vector2(initialPosition.X, initialPosition.Y);
		}
	   
		if (node is Node3D)
		{
			((Node3D)node).GlobalPosition = initialPosition;
			((Node3D)node).Rotation = rotation.GetEuler();
		}
		
	   

		foreach (var child in node.GetChildren())
			if (child is NetID netId)
			{
				netId.IsSynced = true;
				GD.Print("NET ID INTEGER IS: " + GenericCore.Instance._netObjectsCount);
				netId.netObjectID = GenericCore.Instance._netObjectsCount;
				netId._myNetworkCore = this;
				netId.IsNetworkReady = true;
				GenericCore.Instance._netObjects.Add((int)(GenericCore.Instance._netObjectsCount++), netId);
				netId.Rpc("Initialize", owner);


			}

		return node;
	}

	/// <summary>
	/// Destroys all netObjects that were owned by the peer
	/// </summary>
	/// <param name="peerId">The peerId that needs deletion</param>
	private void NetDestroyObject(int peerId)
	{
		Godot.Collections.Array<int> staleObjs = new();
		Godot.Collections.Array<int> liveObjs  = new();
		foreach (var i in GenericCore.Instance._netObjects.Keys)
		{
			if (!IsInstanceValid(GenericCore.Instance._netObjects[i]))
			{
				staleObjs.Add(i);
				continue;
			}
			if (GenericCore.Instance._netObjects[i].OwnerId != peerId) continue;
			liveObjs.Add(i);
		}
 
		foreach (var stale in staleObjs)
			GenericCore.Instance._netObjects.Remove(stale);
 
		foreach (var live in liveObjs)
		{
			try
			{
				GenericCore.Instance._netObjects[live].GetParent().QueueFree();
				GenericCore.Instance._netObjects.Remove(live);
			}
			catch
			{
				GD.PushWarning("Notice: Wrong Spawner trying to destroy object.  Not an error.");
			}
		}
	}

	/// <summary>
	/// Destroys a single NetID from the list
	/// </summary>
	/// <param name="netId">The netId that would be deleted</param>
	public void NetDestroyObject(NetID netId)
	{

			if (!GenericCore.Instance.IsServer)
			{
				return;
			}
			if (netId._myNetworkCore == null)
			{
				try
				{
				GenericCore.Instance._netObjects.Remove((int)netId.netObjectID);
					GD.Print("Spawner: " + Name + ", is tring to RPC delete - " + netId.GetParent().Name);
					netId.ReplicationConfig = null;
					netId.Rpc("ManualDelete");
				}
				catch
				{
					GD.PushWarning("Game Object already Destroyed.");
				}
			}
			if (netId._myNetworkCore != this)
			{
				//Avoid a problem whenever possible.
				return;
			}
			foreach (var i in GenericCore.Instance._netObjects.Keys)
			{
				try
				{
					if (GenericCore.Instance._netObjects[i] != netId) continue;
						GenericCore.Instance._netObjects[i].GetParent().QueueFree();
						GenericCore.Instance._netObjects.Remove(i);
				}
				catch
				{
					//Wrong Spawner...
					GD.PushWarning("Notice: Wrong Spawner trying to destroy object.  Not an error.");
				}
			}
	}

	public void OnClientDisconnected(long id)
	{
		NetDestroyObject((int)id);
		EmitSignalExposedClientDisconnected(id);
	}

	public void OnClientConnected(long peerId, Godot.Collections.Dictionary<string, string> peerInfo) 
	{
		if(SpawnInexZeroOnConnect)
		{
			if (GenericCore.Instance.IsServer)
			{
				NetCreateObject(0, new Vector3(0, 0, 0), Quaternion.Identity, peerId);
			}
		}
		EmitSignalExposedClientConnected(peerId, peerInfo);
	}
}
