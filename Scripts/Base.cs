using Godot;
using System;

public partial class Base : StaticBody3D
{
	[Export] ProgressBar HealthBar;
	[Export] public int MaxHp = 750;
	[Export] public NetID MyID {get; private set;}
	public float _currentHp = 750f;
	[Export] public float CurrentHp
	{
		get => _currentHp;
		set
		{
			_currentHp = value;
			UpdateHealthBar();
		}
	}
	[Export] public Node3D Spawnpoint {get; private set;}
	[Export] Area3D Inside;

	
	// --- Turret Variables ---
	[Export] public Marker3D _turretSpawnLeft;
	[Export] public Marker3D _turretSpawnRight;

	public bool isLeftTurretSpawned;
	public bool isRightTurretSpawned;

	[Signal]
	public delegate void TurretSpawnRequestedEventHandler(Vector3 spawnPos, Quaternion spawnRot, int ownerId);

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if(GenericCore.Instance.IsServer)
		{
			_currentHp = MaxHp;
			GameMaster.Instance.SuddenDeathTrigger += BaseDestroyed;
			Inside.BodyEntered += BaseEntered;
			Inside.BodyExited += BaseExited;
		}
	}
	
	public void Hit(int owner, float dmg)
	{
		GD.PushWarning($"Player Base {MyID.OwnerId} was hit by {owner}");

		if(owner == MyID.OwnerId) { return; }

		_currentHp -= dmg;
		if(_currentHp <= 0) BaseDestroyed();
	}

	void BaseDestroyed()
	{
		Rpc(MethodName.ClientPlayDestroyedSfx, GlobalPosition);
		GenericCore.Instance.MainNetworkCore.NetDestroyObject(MyID);
		//Rpc(MethodName.SelfDestruct);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPlayDestroyedSfx(Vector3 pos)
	{
		SoundFx.PlayAt(GetTree().CurrentScene, pos, SoundFx.BaseDestroyed, -10f);
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
			GD.PushWarning($"Player {player.MyId.OwnerId} entered base {MyID.OwnerId}");
			if(player.MyId.OwnerId == MyID.OwnerId)
			{
				player.inBase = true;
				player.Rpc(Player.MethodName.EnteredBase);
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
			player.inBase = false;
			GD.PushWarning($"Player {player.MyId.OwnerId} exited base {MyID.OwnerId}");
			player.Rpc(Player.MethodName.ExitBase);
		}
	}

	private void UpdateHealthBar()
	{
		if (HealthBar == null) return;
		HealthBar.MaxValue = MaxHp;
		HealthBar.Value = _currentHp;
		HealthBar.Visible = _currentHp < MaxHp; // optional: hide at full HP
	}

	public void SpawnTurret()
	{
		if (!isLeftTurretSpawned)
		{
			RpcId(1, MethodName.ServerTurretSpawnRequest, _turretSpawnLeft.GlobalPosition, _turretSpawnLeft.GlobalBasis.GetRotationQuaternion());
			isLeftTurretSpawned = true;
		}
		if (!isRightTurretSpawned)
		{
			RpcId(1, MethodName.ServerTurretSpawnRequest, _turretSpawnRight.GlobalPosition, _turretSpawnRight.GlobalBasis.GetRotationQuaternion());
			isRightTurretSpawned = true;
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerTurretSpawnRequest(Vector3 spawnPos, Quaternion spawnRot)
	{
		if (!GenericCore.Instance.IsServer) return;
		EmitSignal(SignalName.TurretSpawnRequested, spawnPos, spawnRot, MyID.OwnerId);
	}
}
