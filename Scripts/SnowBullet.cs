using Godot;
using System.Linq;

public partial class SnowBullet : RigidBody3D
{
	// ── Configuration ────────────────────────────────────────────────

	[Export] Area3D CollisionArea;

	/// <summary>Bullet travel speed in units/second. Set by Level after spawn.</summary>
	[Export] public float Speed = 30f;

	/// <summary>Maximum lifetime in seconds before auto-despawn.</summary>
	[Export] public float MaxLifetime = 3f;

	/// <summary>Multiplayer authority id of the player who fired. Set by Level after spawn.</summary>
	public int ShooterId { get; set; } = 1;

	public float Damage;

	/// <summary>
	/// Travel direction (normalised). Set by Level immediately after spawn,
	/// matching the Direction/Speed pattern used by PlayerProjectile.
	/// </summary>
	public Vector3 Direction { get; set; } = Vector3.Zero;

	// ── Internal state ───────────────────────────────────────────────

	private float _lifetime = 0f;
	private bool  _dead     = false;

	// ── Lifecycle ────────────────────────────────────────────────────

	public override void _Ready()
	{
		// RigidBody3D: disable gravity to drive movement manually.
		GravityScale = 0f;

		// --- ADJUSTMENT: Use the Exported Area3D for collision detection ---
		if (CollisionArea != null)
		{
			CollisionArea.BodyEntered += OnBodyEntered;
		}
		else
		{
			GD.PushError("SnowBullet: CollisionArea is not assigned in the inspector!");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead) return;

		_lifetime += (float)delta;
		if (_lifetime >= MaxLifetime)
		{
			MarkDeadAndFree();
			return;
		}

		// Drive movement every frame by setting LinearVelocity.
		if (Direction != Vector3.Zero)
			LinearVelocity = Direction * Speed;
	}

	// ── Collision (server-authoritative) ────────────────────────────

	private void OnBodyEntered(Node body)
	{
		if (_dead) return;

		// Resolved on server only.
		if (!Multiplayer.IsServer()) return;

		GD.Print($"SnowBullet: Area hit '{body.Name}' (type={body.GetType().Name})");

		bool validHit = false;
		switch (body)
		{
			case MeleeEnemy meleeEnemy:
				meleeEnemy.OnHitByBullet(ShooterId, Damage);
				validHit = true;
				break;

			case Player player:
				// Do not damage the shooter.
				if (player.GetMultiplayerAuthority() != ShooterId)
				{
					var attacker = GetTree().GetNodesInGroup("players")
						.OfType<Player>()
						.FirstOrDefault(p => p.MyId.OwnerId == ShooterId);
					player.TakeDamage(Damage, attacker);
					validHit = true;
				}
				break;

			case Base playerBase:
				if (ShooterId != -1)
				{
					playerBase.Hit(ShooterId, Damage);
					validHit = true;
				}
				break;

			case BossEnemy boss:
				// ShooterId == -1 is the sentinel for boss-fired bullets; don't self-damage.
				if (ShooterId != -1)
				{
					boss.OnHitByBullet(ShooterId, Damage);
					validHit = true;
				}
				break;

			default:
				GD.Print("SnowBullet: Area hit geometry or unhandled type");
				break;
		}

		if (validHit && ShooterId != -1)
			GameMaster.Instance.NotifyHit(ShooterId);

		// Confirm hit to all clients via RPC.
		Rpc(MethodName.ClientDestroyOnHit, GlobalPosition);
		MarkDeadAndFree();
	}

	// ── RPC ─────────────────────────────────────────────────────────

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientDestroyOnHit(Vector3 hitPos)
	{
		if (_dead) return;
		GlobalPosition = hitPos; // snap to server-confirmed impact point
		MarkDeadAndFree();
	}

	// ── Helpers ──────────────────────────────────────────────────────

	private void MarkDeadAndFree()
	{
		if (_dead) return;
		_dead = true;
		QueueFree();
	}
}
