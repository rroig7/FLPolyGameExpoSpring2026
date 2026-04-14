using Godot;

/// <summary>
/// Deterministic projectile — no MultiplayerSynchronizer needed or wanted.
///
/// Both peers spawn the bullet at the same GlobalTransform via the
/// SpawnBulletOnAllPeers RPC and apply the same Velocity, so physics keeps
/// them in lock-step automatically.
///
/// When the server detects a collision it calls NotifyHitOnClients() which
/// RPCs every peer to destroy their local copy at the impact point, giving
/// clean visual confirmation without any sync overhead.
/// </summary>
public partial class SnowBullet : CharacterBody3D
{
	[Export] public float BulletSpeed = 30f;
	[Export] public float MaxLifetime  = 3f;

	/// <summary>True on the server instance; false on every client.</summary>
	public bool IsAuthoritative { get; set; } = false;

	/// <summary>Multiplayer authority id of the player who fired.</summary>
	public int ShooterId { get; set; } = 1;

	/// <summary>
	/// Unique id stamped by the spawner so every peer can look up and destroy
	/// the correct local bullet instance when the server confirms a hit.
	/// </summary>
	public int BulletId { get; set; } = -1;

	private float _lifetime = 0f;
	private bool  _dead     = false;

	public override void _Ready()
	{
		// Defensive: strip any leftover NetID from the packed scene.
		foreach (var child in GetChildren())
		{
			if (child is MultiplayerSynchronizer || child is NetID)
			{
				GD.PushWarning($"SnowBullet: removing unexpected synchronizer '{child.Name}'. Delete it from the packed scene.");
				child.QueueFree();
			}
		}

		Velocity = GlobalTransform.Basis.Z * BulletSpeed;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead) return;

		_lifetime += (float)delta;
		if (_lifetime >= MaxLifetime)
		{
			_dead = true;
			QueueFree();
			return;
		}

		if (IsAuthoritative)
			HandleAuthoritativeMovement();
		else
			HandleClientMovement((float)delta);
	}

	// ── Server ────────────────────────────────────────────────────────

	private void HandleAuthoritativeMovement()
	{
		KinematicCollision3D collision = MoveAndCollide(Velocity * (float)GetPhysicsProcessDeltaTime());
		if (collision == null) return;

		Node collider = collision.GetCollider() as Node;
		GD.Print($"SnowBullet: hit something: {(collider as Node)?.Name ?? "unknown"}, type={collider?.GetType().Name}");

		if (collider is Node hitNode && hitNode.IsInGroup("enemies"))
		{
			hitNode.Call("OnHitByBullet");
		}
		else if (collider is Player player)
		{
			GD.Print($"SnowBullet: collider IS a Player, ShooterId={ShooterId}, playerAuthority={player.GetMultiplayerAuthority()}");
			if (player.GetMultiplayerAuthority() != ShooterId)
				player.TakeDamage(player.BulletDamage);
			else
				GD.Print("SnowBullet: skipping damage, player is the shooter");
		}
		else if(collider is Base playerBase)
		{
			playerBase.Hit(ShooterId, 1);
		}
		else
		{
			GD.Print("SnowBullet: hit something that is neither enemy nor Player");
		}

		Rpc(MethodName.DestroyOnClient, GlobalPosition);
		_dead = true;
		QueueFree();
	}

	// ── Client ────────────────────────────────────────────────────────

	private void HandleClientMovement(float delta)
	{
		// Deterministic movement — same velocity, same fixed delta on all peers,
		// so client position stays in lock-step with the server without syncing.
		GlobalPosition += Velocity * delta;
	}

	// ── RPC ───────────────────────────────────────────────────────────

	/// <summary>
	/// Server → all clients. Destroys the client-side bullet at the confirmed
	/// impact position. Snapping to hitPos corrects any tiny drift before death.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void DestroyOnClient(Vector3 hitPos)
	{
		if (_dead) return;
		_dead = true;
		GlobalPosition = hitPos; // snap to server-confirmed impact point
		// TODO: spawn a hit-effect particle here if desired
		QueueFree();
	}
}
