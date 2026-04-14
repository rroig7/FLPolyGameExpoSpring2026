using Godot;
using System;

public partial class Base : Node
{

	[Export] int MaxHp;
	[Export] public NetID MyID {get; private set;}
	[Export] int currentHP;
	[Export] public Node3D Spawnpoint {get; private set;}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if(GenericCore.Instance.IsServer)
		{
			currentHP = MaxHp;
		}
	}
	
	public void Hit(int owner, int dmg)
	{
		GD.PushWarning($"Player Base {MyID.OwnerId} was hit by {owner}");

		if(owner == MyID.OwnerId) { return; }

		currentHP -= dmg;
		if(currentHP <= 0) { GenericCore.Instance.MainNetworkCore.NetDestroyObject(MyID); }
	}

}
