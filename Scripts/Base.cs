using Godot;
using System;

public partial class Base : Node
{

	[Export] int MaxHp;
	[Export] public NetID MyID {get; private set;}
	[Export] int currentHP;
	[Export] public Node3D Spawnpoint {get; private set;}
	[Export] Area3D Inside;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if(GenericCore.Instance.IsServer)
		{
			currentHP = MaxHp;
			GameMaster.Instance.SuddenDeathTrigger += BaseDestroyed;
			Inside.BodyEntered += BaseEntered;
			Inside.BodyExited += BaseExited;
		}
	}
	
	public void Hit(int owner, int dmg)
	{
		GD.PushWarning($"Player Base {MyID.OwnerId} was hit by {owner}");

		if(owner == MyID.OwnerId) { return; }

		currentHP -= dmg;
		if(currentHP <= 0) BaseDestroyed();
	}

	void BaseDestroyed()
	{
		GenericCore.Instance.MainNetworkCore.NetDestroyObject(MyID);
		//Rpc(MethodName.SelfDestruct);	
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void SelfDestruct()
	{
		GetParent().QueueFree();
	}

	void BaseEntered(Node body)
	{
		if(body is Player player)
		{
			if(player.MyId.OwnerId == MyID.OwnerId)
			{
				player.EnteredBase();
			}
			else
			{
				var dir = Spawnpoint.GlobalPosition.DirectionTo(player.GlobalPosition);
				dir.Y = 0;
				player.Velocity = Vector3.Zero;
				player.ApplyKnockback(dir * 10);
			}
		}
	}

	void BaseExited(Node body)
	{
		if(body is Player player && player.MyId.OwnerId == MyID.OwnerId)
		{
			player.ExitBase();
		}
	}

}
