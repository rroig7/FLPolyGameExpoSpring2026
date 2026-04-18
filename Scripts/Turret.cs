using Godot;
using System.Collections.Generic;

public partial class Turret : Node3D
{
	[Export] public float RotationSpeed = 5.0f;
	[Export] public float FireRate = 1.0f;
	[Export] public float BulletSpeed = 20.0f;
	[Export] public float BulletDamage = 10.0f;

	[Export] public NetID MyID;

	[Export] public Node3D TurretHead;
	[Export] public Node3D Muzzle;
	[Export] public Area3D TurretFOV;
	
	/// <summary>Peer ID of the player who placed this turret. Set before the turret enters the tree.</summary>
	[Export] public int OwnerPeerId = -2;

	[Signal] public delegate void BulletSpawnRequestedEventHandler(Vector3 origin, Quaternion rotation, int bulletId, int shooterId, float dmg);

	private readonly List<Node3D> _targetsInFOV = new();
	private Node3D _currentTarget;
	private float _fireCooldown = 0.25f;
	private bool _isAcquiring = false;
	private int _bulletCounter = 0;

	[Export] public uint CollisionMask = 1;

	public override void _Ready()
	{
		TurretHead ??= GetNode<Node3D>("TurretHead");
		Muzzle     ??= GetNode<Node3D>("TurretHead/Muzzle");
		TurretFOV  ??= GetNode<Area3D>("TurretFOV");

		TurretFOV.BodyEntered += OnBodyEntered;
		TurretFOV.BodyExited  += OnBodyExited;

		GameMaster.Instance?.RegisterTurret(this);
		
		GD.Print($"[Turret] OwnerPeerId set to {OwnerPeerId}");
		
		GameMaster.Instance.SuddenDeathTrigger += RemoveTurret;
	}

	public override void _Process(double delta)
	{
		_fireCooldown -= (float)delta;

		Node3D newTarget = GetClosestTarget();



		if (newTarget != _currentTarget)
		{
			_currentTarget = newTarget;
			_isAcquiring = true;
		}

		if (_currentTarget is null)
			return;
		;
		
		

		if (_currentTarget is not Player p || p.MyId.OwnerId != OwnerPeerId)
			AimAt(_currentTarget, (float)delta);
		
		if (_isAcquiring && IsAimedAt(_currentTarget))
			_isAcquiring = false;

		if (!_isAcquiring && _fireCooldown <= 0f && IsAimedAt(_currentTarget))
		{
			if (GenericCore.Instance.IsServer)
				Fire();
			_fireCooldown = 1f / FireRate;
		}
	}

	// ── Firing ────────────────────────────────────────────────────────────────

	private void Fire()
	{
		if (_currentTarget is null) return;
		if (_currentTarget is Player p && p.MyId.OwnerId == OwnerPeerId) return;

		// Use muzzle orientation (consistent with IsAimedAt); avoids LookingAt failure
		// when target is near-vertical relative to turret (aimDir ≈ Vector3.Up/Down).
		Vector3 aimDir = -Muzzle.GlobalTransform.Basis.Z;

		int bulletId = _bulletCounter++;
		Quaternion spawnRot = Transform3D.Identity
			.LookingAt(-aimDir, Vector3.Up)
			.Basis.GetRotationQuaternion();

		EmitSignal(SignalName.BulletSpawnRequested, Muzzle.GlobalPosition, spawnRot, bulletId, OwnerPeerId, BulletDamage);
	}

	// ── Targeting ─────────────────────────────────────────────────────────────

	private Node3D GetClosestTarget()
	{
		Node3D closest = null;
		float  minDist = float.MaxValue;

		for (int i = _targetsInFOV.Count - 1; i >= 0; i--)
		{
			Node3D body = _targetsInFOV[i];
			if (!IsInstanceValid(body)) { _targetsInFOV.RemoveAt(i); continue; }
			if (body is Player p && p.IsDead) { _targetsInFOV.RemoveAt(i); continue; }
			if (body is MeleeEnemy me && me.CurrentHp <= 0) { _targetsInFOV.RemoveAt(i); continue; }
			if (body is BossEnemy be && be.CurrentHp <= 0) { _targetsInFOV.RemoveAt(i); continue; }

			float d = GlobalPosition.DistanceSquaredTo(body.GlobalPosition);
			if (d < minDist) { minDist = d; closest = body; }
		}
		return closest;
	}

	// ── Aiming ────────────────────────────────────────────────────────────────

	private void AimAt(Node3D target, float delta)
	{
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
			TurretHead.GlobalPosition,
			target.GlobalPosition,
			CollisionMask
		);

		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);

		Vector3 aimPoint = result.Count > 0
			? (Vector3)result["position"]
			: target.GlobalPosition;

		// Full 3D direction — no Y zeroing, so the head pitches up/down too
		Vector3 toTarget = (aimPoint - TurretHead.GlobalPosition).Normalized();

		if (toTarget.IsZeroApprox())
			return;

		Basis lookBasis   = Basis.LookingAt(toTarget, Vector3.Up);
		Basis parentBasis = TurretHead.GetParentNode3D().GlobalBasis.Orthonormalized();
		Basis localLook   = parentBasis.Inverse() * lookBasis;

		Quaternion desired = new Quaternion(localLook.Orthonormalized());
		TurretHead.Quaternion = TurretHead.Quaternion.Slerp(desired, RotationSpeed * delta);
	}

	/// Returns true when the muzzle is roughly pointing at the target (within 5°).
	private bool IsAimedAt(Node3D target)
	{
		Vector3 toTarget  = (target.GlobalPosition - Muzzle.GlobalPosition).Normalized();
		Vector3 muzzleFwd = -Muzzle.GlobalTransform.Basis.Z;
		return muzzleFwd.Dot(toTarget) > 0.996f;
	}

	// ── FOV callbacks ─────────────────────────────────────────────────────────

	private void OnBodyEntered(Node3D body)
	{
		if (_targetsInFOV.Contains(body)) return;
		bool isTargetable = body.IsInGroup("players") || body.IsInGroup("enemies") || body.IsInGroup("boss");
		if (!isTargetable) return;
		if (body is Player p && p.MyId.OwnerId == OwnerPeerId) return;
		_targetsInFOV.Add(body);
	}

	private void OnBodyExited(Node3D body) => _targetsInFOV.Remove(body);

	public void RemoveTurret()
	{
		if (MyID == null)
			MyID = GetNodeOrNull<NetID>("MultiplayerSynchronizer");

		if (MyID != null && IsInstanceValid(MyID))
		{
			MyID.ProcessMode = ProcessModeEnum.Disabled;
			try { MyID.ReplicationConfig = null; } catch { }
			MyID.Rpc(NetID.MethodName.ManualDelete);
		}
		else
		{
			GD.PushWarning("BossEnemy.Die: no valid NetID found, freeing locally only.");
			QueueFree();
		}
	}
}
