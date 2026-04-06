using Godot;
using System;

public partial class BaseNetworkedEnemy : CharacterBody3D
{
	[Export] protected AnimationPlayer Animator;
	[Export] protected NetID MyId;
	[Export] protected NavigationAgent3D NavAgent;
	[Export] protected float baseSpeed;

	protected Node3D Target;
	protected float curSpeed;
	protected enum EnemyStates {ROAMING, CHASING, ATTACKING};
	protected EnemyStates currentState;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		if(GenericCore.Instance.IsServer)
		{
			if (NavigationServer3D.MapGetIterationId(NavAgent.GetNavigationMap()) == 0)
			{
				return;
			}

			if(NavAgent.IsNavigationFinished()) EnemyNavigationFinished();
			else EnemyNaviagationNotFinished();

			//Server Only
			ServerPhysicsProcess((float)delta);

			var nextPos = NavAgent.GetNextPathPosition();
			var dir = GlobalPosition.DirectionTo(nextPos);
			dir.Y = 0;
			Velocity = Velocity.Lerp(dir * curSpeed, (float)delta);

			var Forward = Basis.LookingAt(dir);
			Basis = Forward;

			if(MoveAndSlide()) ServerMoveAndSlideHit(GetLastSlideCollision());
		}
		if(MyId.IsLocal)
		{
			//Local Player only
			LocalPhysicsProcess((float)delta);
		}
		if(!GenericCore.Instance.IsServer)
		{
			//All players but server
			AllPlayerPhysicsProcess((float)delta);
		}

		//Everyone
		AllPhysicsProcess((float)delta);
	}

	void OnPlayerDetected(Node collider)
	{
		if(GenericCore.Instance.IsServer)
		{
			//Server Only
			Server_PlayerEnteredEvent(collider);
		}
		if(MyId.IsLocal)
		{
			//Local Player only
			Local_PlayerEnteredEvent(collider);
		}
		if(!GenericCore.Instance.IsServer)
		{
			//All players but server
			AllPlayer_PlayerEnteredEvent(collider);
		}

		//Everyone
		All_PlayerEnteredEvent(collider);
	}

	void OnPlayerLeave(Node collider)
	{
		if(GenericCore.Instance.IsServer)
		{
			//Server Only
			Server_PlayerExitedEvent(collider);
		}
		if(MyId.IsLocal)
		{
			//Local Player only
			Local_PlayerExitedEvent(collider);
		}
		if(!GenericCore.Instance.IsServer)
		{
			//All players but server
			AllPlayer_PlayerExitedEvent(collider);
		}

		//Everyone
		All_PlayerExitedEvent(collider);
	}


		//------ Process Handlers ------\\
	public virtual void ServerPhysicsProcess(float delta) {}
	public virtual void LocalPhysicsProcess(float delta) {}
	public virtual void AllPlayerPhysicsProcess(float delta) {}
	public virtual void AllPhysicsProcess(float delta) {}

	
	//------ Detection Handlers -------\\
	public virtual void Server_PlayerEnteredEvent(Node collider) {}	
	public virtual void Local_PlayerEnteredEvent(Node collider) {}	
	public virtual void AllPlayer_PlayerEnteredEvent(Node collider) {}	
	public virtual void All_PlayerEnteredEvent(Node collider) {}	


	public virtual void Server_PlayerExitedEvent(Node collider) {}	
	public virtual void Local_PlayerExitedEvent(Node collider) {}	
	public virtual void AllPlayer_PlayerExitedEvent(Node collider) {}	
	public virtual void All_PlayerExitedEvent(Node collider) {}

	
	//------ Extras ------\\
	public virtual void ServerMoveAndSlideHit(KinematicCollision3D collider) {}
	public virtual void EnemyNavigationFinished() {}
	public virtual void EnemyNaviagationNotFinished() {}
}
