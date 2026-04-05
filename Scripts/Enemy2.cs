using Godot;
using System;
using System.Linq;

public partial class Enemy2 : CharacterBody3D
{
	[Export] public NetID myId;
	[Export] public AnimationPlayer myAnimation;

	[Export] private Marker3D[] patrolPoints;
	[Export] public float chaseRange = 10.0f;
	[Export] public float returnRange = 15.0f;
	[Export] public float moveSpeed = 4.0f;

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

	public override void _Ready()
	{
		navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		navAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.Edgecentered;

		if (patrolPoints == null || patrolPoints.Length == 0)
		{
			var pointParent = GetTree().CurrentScene.GetNode("GameMaster/MainLevel/EnemyNavPoints");
			patrolPoints = pointParent.GetChildren().OfType<Marker3D>().ToArray();
		}

		ChoosePatrolPoint();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!myId.IsNetworkReady) return;

		if (GenericCore.Instance.IsServer)
		{
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

					navAgent.TargetPosition = targetPlayer.GlobalPosition;
					MoveAlongPath();
					SyncedIsMoving = true;
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
	}

	private void UpdateAnimation()
	{
		if (myAnimation == null) return;

		if (!SyncedIsMoving)
			myAnimation.Play("Attacking");
		else if (SyncedIsChasing)
			myAnimation.Play("Running");
		else
			myAnimation.Play("Walking");
	}

	private void ScanForPlayers()
	{
		Player closestPlayer = null;
		float closestDist = chaseRange;

		foreach (Player player in GetTree().GetNodesInGroup("players"))
		{
			Godot.GD.Print("Player: " + player.Name);
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

		// only rotate if the direction has meaningful horizontal movement
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

	public void OnHitByBullet()
	{
		if (!GenericCore.Instance.IsServer) return;

		if (myId.ReplicationConfig != null)
		{
			try
			{
				myId.ReplicationConfig = null;
			}
			catch { }
		}

		GenericCore.Instance.MainNetworkCore.NetDestroyObject(myId);
	}
}
