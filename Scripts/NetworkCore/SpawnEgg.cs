using Godot;
using System;
using System.Threading.Tasks;

[GlobalClass]
public partial class SpawnEgg : Node2D
{
	[Export] NetworkCore MyNetworkCore;
	[Export] int SpawnIndex;
	[Export] bool Respawn;
	[Export] float TimeToRespawn;
	Node Spawn;

	// Called when the node enters the scene tree for the first time.
	public override async void _Ready()
	{
		while (!GenericCore.Instance.IsGenericCoreConnected)
			await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);

		if(!GenericCore.Instance.IsServer)
			{
				QueueFree();
				return;
			}
			
		if(MyNetworkCore == null)
			MyNetworkCore = GenericCore.Instance.MainNetworkCore;
		
		SpawnObject();
	}

	void SpawnObject()
	{
		GD.PushWarning("Spawning Enemy");
		Spawn = MyNetworkCore.NetCreateObject(SpawnIndex, new(GlobalPosition.X, GlobalPosition.Y, 0), Quaternion.Identity);
		if(Respawn) Spawn.TreeExited += RespawnObject;
	}

	async void RespawnObject()
	{
		Spawn.TreeExited -= RespawnObject;
		await ToSignal(GetTree().CreateTimer(TimeToRespawn), Timer.SignalName.Timeout);
		SpawnObject();
	}

}
