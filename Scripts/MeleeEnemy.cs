using Godot;
using System;
using System.Linq;

public partial class MeleeEnemy : CharacterBody3D
{
	[Export] public NetID myId;
	[Export] public AnimationPlayer myAnimation;
	[Export] ProgressBar HealthBar;

	[Export] private Marker3D[] patrolPoints;
	[Export] public float chaseRange = 10.0f;
	[Export] public float returnRange = 15.0f;
	[Export] public float moveSpeed = 4.0f;

	[Export] public float attackRange = 1.5f;
	[Export] public float attackCooldown = 2.0f;
	[Export] public float knockbackForce = 6.0f;
	
	[Export] public float MaxHp = 100f;
	private float _currentHp = 100f;
	[Export] public float CurrentHp
	{
		get => _currentHp;
		set
		{
			_currentHp = value;
			UpdateHealthBar();
		}
	}
	[Export] public int XP_Value = 10;

	private float _attackTimer = 0f;

	[Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public bool SyncedIsMoving = false;
	[Export] public bool SyncedIsChasing = false;

	private NavigationAgent3D navAgent;
	private RandomNumberGenerator rng = new RandomNumberGenerator();

	private enum EnemyState { Patrolling, Chasing, Returning }
	private EnemyState state = EnemyState.Patrolling;

	private Player targetPlayer = null;
	private Vector3 lastPatrolTarget;

	private bool _isDying = false;

	private AudioStreamPlayer3D _walkSfx;

	public override void _Ready()
	{
		navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		navAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.Edgecentered;

		_walkSfx = SoundFx.MakeLooped(this, SoundFx.MeleeEnemyWalk, -25f);

		if (patrolPoints == null || patrolPoints.Length == 0)
		{
			var pointParent = GetTree().GetFirstNodeInGroup("Patrol");
			patrolPoints = pointParent.GetChildren().OfType<Marker3D>().ToArray();
		}

		if (myId == null)
			myId = GetNodeOrNull<NetID>("MultiplayerSynchronizer");

		ChoosePatrolPoint();
		UpdateHealthBar();
		GameMaster.Instance.SuddenDeathTrigger += Die;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (myId == null || !myId.IsNetworkReady) return;
		if (_isDying) return;

		if (GenericCore.Instance.IsServer)
		{
			if (_attackTimer > 0f)
				_attackTimer -= (float)delta;

			if (!IsOnFloor())
			{
				Vector3 vel = Velocity;
				vel.Y -= 20f * (float)delta;
				Velocity = vel;
			}

			switch (state)
			{
				case EnemyState.Patrolling:
					ScanForPlayers();
					if (!navAgent.IsNavigationFinished())
					{
						MoveAlongPath();
						SyncedIsMoving = true;
						SyncedIsChasing = false;
					}
					else
					{
						SyncedIsMoving = false;
						SyncedIsChasing = false;
						ChoosePatrolPoint();
					}
					break;

				case EnemyState.Chasing:
					if (targetPlayer == null || !IsInstanceValid(targetPlayer))
					{
						ReturnToPatrol();
						break;
					}

					float distanceToPlayer = (targetPlayer.GlobalPosition - GlobalPosition).Length();

					if (distanceToPlayer > returnRange)
					{
						ReturnToPatrol();
						break;
					}

					if (distanceToPlayer <= attackRange)
					{
						TryAttackPlayer(targetPlayer);
						Velocity = new Vector3(0, Velocity.Y, 0);
						SyncedIsMoving = false;
					}
					else
					{
						navAgent.TargetPosition = targetPlayer.GlobalPosition;
						MoveAlongPath();
						SyncedIsMoving = true;
					}

					SyncedIsChasing = true;
					break;

				case EnemyState.Returning:
					ScanForPlayers();
					if (!navAgent.IsNavigationFinished())
					{
						MoveAlongPath();
						SyncedIsMoving = true;
						SyncedIsChasing = false;
					}
					else
					{
						state = EnemyState.Patrolling;
						SyncedIsMoving = false;
						ChoosePatrolPoint();
					}
					break;
			}
		}

		if (!GenericCore.Instance.IsServer)
			UpdateAnimation();

		SoundFx.SetLoopActive(_walkSfx, SyncedIsMoving && !_isDying);
	}

	private void TryAttackPlayer(Player player)
	{
		if (_attackTimer > 0f) return;

		_attackTimer = attackCooldown;

		Rpc(MethodName.ClientPlayAttackSfx);

		// Get horizontal direction from enemy to player, normalize while flat
		Vector3 knockbackDir = (player.GlobalPosition - GlobalPosition);
		knockbackDir.Y = 0;
		knockbackDir = knockbackDir.Normalized();

		// Add small upward nudge AFTER normalizing so horizontal stays dominant
		knockbackDir += new Vector3(0, 0.15f, 0);

		player.ApplyKnockback(knockbackDir * knockbackForce);
		player.TakeDamage(10f);
	}

	private void UpdateAnimation()
	{
		if (myAnimation == null) return;

		if (!SyncedIsMoving)
			myAnimation.Play("seal_idle_flutter");
		else
			myAnimation.Play("seal_hop_slow");
	}

	private void UpdateHealthBar()
	{
		if (HealthBar == null) return;
		HealthBar.MaxValue = MaxHp;
		HealthBar.Value = _currentHp;
		HealthBar.Visible = _currentHp < MaxHp; // optional: hide at full HP
	}

	private void ScanForPlayers()
	{
		Player closestPlayer = null;
		float closestDist = chaseRange;

		foreach (Player player in GetTree().GetNodesInGroup("players"))
		{
			//GD.Print("Player: " + player.Name);
			float dist = (player.GlobalPosition - GlobalPosition).Length();
			if (dist <= closestDist)
			{
				closestDist = dist;
				closestPlayer = player;
			}
		}

		if (closestPlayer != null)
		{
			targetPlayer = closestPlayer;
			state = EnemyState.Chasing;
			navAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.None;
		}
	}

	private void ReturnToPatrol()
	{
		state = EnemyState.Returning;
		targetPlayer = null;
		navAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.Edgecentered;
		navAgent.TargetPosition = lastPatrolTarget;
	}

	private void MoveAlongPath()
	{
		Vector3 destination = navAgent.GetNextPathPosition();
		Vector3 direction = (destination - GlobalPosition).Normalized();

		float currentY = Velocity.Y;
		Velocity = new Vector3(direction.X * moveSpeed, currentY, direction.Z * moveSpeed);

		Vector3 flatDirection = new Vector3(direction.X, 0, direction.Z).Normalized();
		if (flatDirection.Length() > 0.1f)
		{
			Transform3D t = Transform;
			t.Basis = Basis.LookingAt(flatDirection, Vector3.Up).Rotated(Vector3.Up, Mathf.DegToRad(90));
			Transform = t;
		}

		MoveAndSlide();
	}

	private void ChoosePatrolPoint()
	{
		if (patrolPoints == null || patrolPoints.Length == 0) return;

		rng.Randomize();
		int index = rng.RandiRange(0, patrolPoints.Length - 1);
		lastPatrolTarget = patrolPoints[index].GlobalPosition;
		navAgent.TargetPosition = lastPatrolTarget;
	}

	public void OnHitByBullet(int id, float dmg)
	{
		if (!GenericCore.Instance.IsServer) return;
		if (_isDying) return;

		CurrentHp -= dmg; // or wire this up to bullet damage later
		GD.Print($"{Name} took damage, HP={CurrentHp}/{MaxHp}");

		if (CurrentHp > 0f) return;

		var Players = GetTree().GetNodesInGroup("players").ToArray().Cast<Player>();

		var player = Players.First(p => p.MyId.OwnerId == id);
		player.XP += XP_Value;
		GD.Print($"{player.Name} gained {XP_Value} XP, total XP={player.XP}");

		Die();
	}
	
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPlayAttackSfx()
	{
		SoundFx.PlayOn(this, SoundFx.MeleeEnemyAttack, -6f);
	}

	public  void Die()
	{
		if (_isDying) return;
		_isDying = true;
		SoundFx.SetLoopActive(_walkSfx, false);

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
			GD.PushWarning("MeleeEnemy.Die: no valid NetID found, freeing locally only.");
			QueueFree();
		}
	}
}
