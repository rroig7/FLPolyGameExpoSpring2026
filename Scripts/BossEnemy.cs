using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BossEnemy : CharacterBody3D
{
	// ── Exports ───────────────────────────────────────────────────────────────
	[Export] public NetID myId;
	[Export] ProgressBar HealthBar;

	[Export] public float FireRate       = 1.0f;
	[Export] public float BulletSpeed    = 20.0f;
	[Export] public float BulletDamage = 20.0f;
	[Export] public float RotationSpeed  = 5.0f;

	[Export] public float MeleeRange     = 2.5f;
	[Export] public float MeleeCooldown  = 1.2f;
	[Export] public float MeleeDamage    = 25.0f;

	[Export] public float MoveSpeed      = 4.0f;
	[Export] public float PatrolRadius   = 20.0f;

	[Export] public uint CollisionMask   = 1;

	[Export] private int _bulletCounter = 0;
	
	[Export] public Node3D Muzzle;

	private float _currentHp = 1000f;
	[Export] public float CurrentHp
	{
		get => _currentHp;
		set
		{
			_currentHp = value;
			UpdateHealthBar();
		}
	}
	[Export] public float MaxHp = 1000f;

	[Export] public float TargetAimHeight = 1.0f;

	[Export] public int XP_Value = 100;
	
	[Signal] public delegate void BulletSpawnRequestedEventHandler(Vector3 origin, Quaternion rotation, int bulletId, int shooterId, float dmg);

	// ── Synced properties (matched to MeleeEnemy pattern) ────────────────────
	[Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public bool SyncedIsMoving   = false;
	[Export] public bool SyncedIsChasing  = false;

	// ── State ─────────────────────────────────────────────────────────────────
	private enum BossState { Idle, Chase, Melee, Ranged }
	private BossState _state = BossState.Idle;

	private Node3D       _currentTarget;
	private List<Node3D> _targetsInFOV  = new();
	private float        _fireCooldown  = 1f;
	private float        _meleeCooldown = 2.5f;
	private bool         _isAcquiring   = false;
	private Vector3      _spawnPosition;

	private Area3D _bossFOV;
	
	private bool _isDying = false;

	private AudioStreamPlayer3D _walkSfx;

	public override void _Ready()
	{
		_spawnPosition = GlobalPosition;
		CurrentHp = MaxHp;

		_walkSfx = SoundFx.MakeLooped(this, SoundFx.BossWalk, -10f);

		if (myId == null)
			myId = GetNodeOrNull<NetID>("MultiplayerSynchronizer");

		_bossFOV = GetNode<Area3D>("BossFOV");
		_bossFOV.BodyEntered += OnBodyEntered;
		_bossFOV.BodyExited  += OnBodyExited;
		
		GameMaster.Instance.SuddenDeathTrigger += Die;
	}

	public override void _Process(double delta)
	{
		if (myId == null || !myId.IsNetworkReady) return;

		float dt = (float)delta;

		// ── Server drives all logic ───────────────────────────────────────────
		if (GenericCore.Instance.IsServer)
		{
			_fireCooldown  -= dt;
			_meleeCooldown -= dt;

			Node3D newTarget = GetClosestTarget();
			if (newTarget != _currentTarget)
			{
				_currentTarget = newTarget;
				_isAcquiring   = true;
			}

			if (_currentTarget is Player tp && tp.IsDead) _currentTarget = null;
			if (_currentTarget != null && !IsInstanceValid(_currentTarget)) _currentTarget = null;

			_state = ChooseState();

			switch (_state)
			{
				case BossState.Idle:
					SyncedIsMoving  = false;
					SyncedIsChasing = false;
					break;

				case BossState.Chase:
					MoveToward(_currentTarget, dt);
					FaceTarget(_currentTarget, dt);
					SyncedIsMoving  = true;
					SyncedIsChasing = true;
					break;

				case BossState.Melee:
					Velocity = new Vector3(0, Velocity.Y, 0);
					FaceTarget(_currentTarget, dt);
					SyncedIsMoving  = false;
					SyncedIsChasing = true;
					if (_meleeCooldown <= 0f)
						DoMeleeAttack();
					break;

				case BossState.Ranged:
					Velocity = new Vector3(0, Velocity.Y, 0);
					FaceTarget(_currentTarget, dt);
					SyncedIsMoving  = false;
					SyncedIsChasing = true;

					if (_isAcquiring && IsAimedAt(_currentTarget))
						_isAcquiring = false;

					if (!_isAcquiring && _fireCooldown <= 0f && IsAimedAt(_currentTarget))
					{
						GD.Print("BOSS ATTEMPTED TO FIRE");
						Fire();
						_fireCooldown = 1f / FireRate;
					}
					break;
			}

			SyncedVelocity = Velocity;
		}

		// ── Clients only update visuals ───────────────────────────────────────
		if (!GenericCore.Instance.IsServer)
			UpdateAnimation();

		SoundFx.SetLoopActive(_walkSfx, SyncedIsMoving && !_isDying);
	}

	// ── State selection ───────────────────────────────────────────────────────

	private BossState ChooseState()
	{
		if (_currentTarget == null)
			return BossState.Idle;

		float dist = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

		if (dist <= MeleeRange)
			return BossState.Melee;

		if (GlobalPosition.DistanceTo(_spawnPosition) > PatrolRadius)
			return BossState.Ranged;

		if (!HasLineOfSight(_currentTarget))
			return BossState.Chase;

		return BossState.Ranged;
	}

	// ── Targeting ─────────────────────────────────────────────────────────────

	private Node3D GetClosestTarget()
	{
		Node3D closest = null;
		float  minDist = float.MaxValue;

		// Snapshot before iteration: OnBodyExited can fire synchronously during
		// a physics step and mutate _targetsInFOV, shifting indices mid-loop.
		var snapshot = _targetsInFOV.ToArray();
		foreach (var body in snapshot)
		{
			if (!IsInstanceValid(body)) { _targetsInFOV.Remove(body); continue; }
			if (body is Player p && p.IsDead) { _targetsInFOV.Remove(body); continue; }

			float d = GlobalPosition.DistanceSquaredTo(body.GlobalPosition);
			if (d < minDist) { minDist = d; closest = body; }
		}
		return closest;
	}

	// ── Movement ──────────────────────────────────────────────────────────────

	private void MoveToward(Node3D target, float delta)
	{
		Vector3 dir = target.GlobalPosition - GlobalPosition;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f) return;

		Velocity = new Vector3(dir.Normalized().X * MoveSpeed, Velocity.Y, dir.Normalized().Z * MoveSpeed);
		MoveAndSlide();
	}

	// ── Rotation ──────────────────────────────────────────────────────────────

	private void FaceTarget(Node3D target, float delta)
	{
		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0f;
		if (toTarget.IsZeroApprox()) return;

		Basis desired   = Basis.LookingAt(toTarget.Normalized(), Vector3.Up)
			.Rotated(Vector3.Up, Mathf.DegToRad(180));
		Quaternion from = new Quaternion(GlobalBasis.Orthonormalized());
		Quaternion to   = new Quaternion(desired.Orthonormalized());

		GlobalBasis = new Basis(from.Slerp(to, RotationSpeed * delta)).Orthonormalized();
	}

	private bool IsAimedAt(Node3D target)
	{
		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0f;
		toTarget = toTarget.Normalized();

		Vector3 forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		forward = forward.Normalized();
		
		// return forward.Dot(toTarget) > 0.98f; // slightly relaxed threshold
		return true;
	}

	// ── Line of sight ─────────────────────────────────────────────────────────

	private bool HasLineOfSight(Node3D target)
	{
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
			GlobalPosition,
			target.GlobalPosition,
			CollisionMask
		);
		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return result.Count == 0 ||
			   result["collider"].Obj is Node hit && hit == target;
	}

	// ── Melee ─────────────────────────────────────────────────────────────────

	private void DoMeleeAttack()
	{
		_meleeCooldown = MeleeCooldown;

		Rpc(MethodName.ClientPlayMeleeSfx);

		if (_currentTarget is Player playerTarget)
			playerTarget.TakeDamage(MeleeDamage);
		else if (_currentTarget != null && _currentTarget.HasMethod("TakeDamage"))
			_currentTarget.Call("TakeDamage", MeleeDamage);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPlayMeleeSfx()
	{
		SoundFx.PlayOn(this, SoundFx.BossMeleeAttack, -10f);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPlayDeathSfx(Vector3 pos)
	{
		SoundFx.PlayAt(GetTree().CurrentScene, pos, SoundFx.BossDeath, -10f);
	}

	// ── Firing ────────────────────────────────────────────────────────────────

	private void Fire()
	{
		if (_currentTarget is null) return;

		Vector3 aimPoint = _currentTarget.GlobalPosition + Vector3.Up * TargetAimHeight;
		Vector3 aimDir = Muzzle.GlobalPosition.DirectionTo(aimPoint).Normalized();
		if (aimDir == Vector3.Zero)
			aimDir = Muzzle.GlobalTransform.Basis.Z;
		
		GD.Print("SENDING BOSS BULLET RPC CALL");
		RpcId(1, MethodName.ServerReceiveFire, aimDir);
	}


	// ── Animation (clients only) ──────────────────────────────────────────────

	private void UpdateAnimation()
	{
		// Wire up your AnimationPlayer here the same way MeleeEnemy does it.
		// Example (add [Export] public AnimationPlayer myAnimation; at the top):
		//
		// if (myAnimation == null) return;
		// if (!SyncedIsMoving && SyncedIsChasing)
		//     myAnimation.Play("Attacking");
		// else if (SyncedIsMoving && SyncedIsChasing)
		//     myAnimation.Play("Running");
		// else
		//     myAnimation.Play("Idle");
	}

	private void UpdateHealthBar()
	{
		if (HealthBar == null) return;
		HealthBar.MaxValue = MaxHp;
		HealthBar.Value = _currentHp;
		HealthBar.Visible = _currentHp < MaxHp; // optional: hide at full HP
	}

	// ── FOV callbacks ─────────────────────────────────────────────────────────

	private void OnBodyEntered(Node3D body)
	{
		if (body.IsInGroup("players") && !_targetsInFOV.Contains(body))
		{
			_targetsInFOV.Add(body);
			GD.Print($"{body.Name} entered Boss Radius.");
		}
		
	}

	private void OnBodyExited(Node3D body) => _targetsInFOV.Remove(body);
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveFire(Vector3 aimDir)
	{
		if (!GenericCore.Instance.IsServer) return;
		if (Multiplayer.GetRemoteSenderId() != myId.OwnerId) return;
	
		int      bulletId = _bulletCounter++;
		Vector3  spawnPos = Muzzle.GlobalPosition;
		Quaternion spawnRot = Transform3D.Identity
			.LookingAt(-aimDir, Vector3.Up)
			.Basis.GetRotationQuaternion();
	
		// Signal Level to spawn the bullet (server-side instantiation via NetworkCore).
		// Use -1 as shooter sentinel so the host player (auth=1) isn't treated
		// as the shooter of the boss's own bullets.
		EmitSignal(BossEnemy.SignalName.BulletSpawnRequested, spawnPos, spawnRot, bulletId, -1, BulletDamage);
	}
	
	public void OnHitByBullet(int id, float dmg)
	{
		if (!GenericCore.Instance.IsServer) return;
		if (_isDying) return;

		var shooter = GetTree().GetNodesInGroup("players")
			.OfType<Player>()
			.FirstOrDefault(p => p.MyId.OwnerId == id);

		if (shooter != null && !_targetsInFOV.Contains(shooter))
			_targetsInFOV.Add(shooter);

		CurrentHp -= dmg; // or wire this up to bullet damage later
		GD.Print($"{Name} took damage, HP={CurrentHp}/{MaxHp}");

		if (CurrentHp > 0f) return;

		// Commit to death before XP award / Die() so a same-frame second bullet
		// can't re-award XP or re-enter Die().
		_isDying = true;

		if (shooter != null)
		{
			shooter.XP += XP_Value;
			GD.Print($"{shooter.Name} gained {XP_Value} XP, total XP={shooter.XP}");
		}

		Die();
	}
	
	public void Die()
	{
		if (_isDying) return;
		_isDying = true;
		SoundFx.SetLoopActive(_walkSfx, false);
		Rpc(MethodName.ClientPlayDeathSfx, GlobalPosition);

		if (myId == null)
			myId = GetNodeOrNull<NetID>("MultiplayerSynchronizer");

		if (myId != null && IsInstanceValid(myId))
		{
			myId.ProcessMode = ProcessModeEnum.Disabled;
			try { myId.ReplicationConfig = null; } catch { }
			myId.Rpc(NetID.MethodName.ManualDelete);
		}
		else
		{
			GD.PushWarning("BossEnemy.Die: no valid NetID found, freeing locally only.");
			QueueFree();
		}
	}
	

}
