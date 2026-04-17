using Godot;
using System.Collections.Generic;

public partial class Turret : Node3D
{
	[Export] public float RotationSpeed = 5.0f;
	[Export] public float FireRate = 1.0f;
	[Export] public float BulletSpeed = 20.0f;
	[Export] public PackedScene BulletScene;

	[Export] public NetID MyID;

	[Export] public Node3D TurretHead;
	[Export] public Node3D Muzzle;
	[Export] public Area3D TurretFOV;

	private readonly List<Node3D> _targetsInFOV = new();
	private Node3D _currentTarget;
	private float _fireCooldown = 0.25f;
	private bool _isAcquiring = false;

	[Export] public uint CollisionMask = 1;

	public override void _Ready()
	{
		TurretHead ??= GetNode<Node3D>("TurretHead");
		Muzzle     ??= GetNode<Node3D>("TurretHead/Muzzle");
		TurretFOV  ??= GetNode<Area3D>("TurretFOV");

		TurretFOV.BodyEntered += OnBodyEntered;
		TurretFOV.BodyExited  += OnBodyExited;
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

		AimAt(_currentTarget, (float)delta);

		if (_isAcquiring && IsAimedAt(_currentTarget))
			_isAcquiring = false;

		if (!_isAcquiring && _fireCooldown <= 0f && IsAimedAt(_currentTarget))
		{
			// Rpc(MethodName.SpawnBulletOnAllPeers, Muzzle.GlobalPosition, ))
			_fireCooldown = 1f / FireRate;
		}
	}

	// ── Targeting ─────────────────────────────────────────────────────────────

	private Node3D GetClosestTarget()
	{
		Node3D closest = null;
		float  minDist = float.MaxValue;

		foreach (Node3D body in _targetsInFOV)
		{
			if (!IsInstanceValid(body)) continue;
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

	// ── Firing ────────────────────────────────────────────────────────────────

	// private void Fire()
	// {
	// 	if (BulletScene is null)
	// 	{
	// 		GD.PushWarning("Turret: BulletScene is not assigned.");
	// 		return;
	// 	}
	//
	// 	var bullet = BulletScene.Instantiate<Node3D>();
	//
	// 	var t = Transform3D.Identity;
	// 	t.Origin = Muzzle.GlobalPosition;
	// 	t.Basis  = new Basis(Muzzle.GlobalTransform.Basis.GetRotationQuaternion());
	// 	bullet.GlobalTransform = t;
	//
	// 	// DO NOT TOUCH THIS CODE HOLY FUCK
	// 	Vector3 muzzleFwd = -Muzzle.GlobalTransform.Basis.Z;
	// 	bullet.GlobalTransform = new Transform3D(
	// 		Basis.LookingAt(-muzzleFwd, Vector3.Up),
	// 		Muzzle.GlobalPosition
	// 	);
	// 	// DO NOT TOUCH THIS CODE HOLY FUCK
	//
	// 	GetTree().Root.AddChild(bullet);
	// }
	
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	// void SpawnBulletOnAllPeers(Vector3 spawnPos, Quaternion spawnRot, int bulletId)
	// {
	// 	if (BulletScene == null)
	// 	{
	// 		GD.PushWarning("Player: SnowBulletScene is not assigned!");
	// 		return;
	// 	}
	//
	// 	var bullet = BulletScene.Instantiate<SnowBullet>();
	// 	bullet.IsAuthoritative = GenericCore.Instance.IsServer;
	// 	bullet.ShooterId       = (int)MyId.OwnerId;
	// 	bullet.BulletId        = bulletId;
	//
	// 	// Authority must be the server (1) so the bullet's Rpc(DestroyOnClient)
	// 	// call is permitted — RpcMode.Authority means "only the authority may call this"
	// 	bullet.SetMultiplayerAuthority(1);
	//
	// 	var t = Transform3D.Identity;
	// 	t.Origin = spawnPos;
	// 	t.Basis  = new Basis(spawnRot);
	// 	bullet.GlobalTransform = t;
	//
	// 	GetTree().CurrentScene.AddChild(bullet);
	// }

	// ── FOV callbacks ─────────────────────────────────────────────────────────

	private void OnBodyEntered(Node3D body)
	{
		if (body.IsInGroup("players") && !_targetsInFOV.Contains(body))
			_targetsInFOV.Add(body);
	}

	private void OnBodyExited(Node3D body) => _targetsInFOV.Remove(body);
}
