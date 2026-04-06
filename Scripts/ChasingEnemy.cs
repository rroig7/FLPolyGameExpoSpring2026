using Godot;
using System;

public partial class ChasingEnemy : BaseNetworkedEnemy
{
	[Export] protected Area3D DetectionArea;
	[Export] protected Area3D ChasingArea;
	[Export] protected float chaseSpeed;
	[Export] float maxRoamRange = 5;
	RandomNumberGenerator rng = new();
	Vector3 RandomVector => new(rng.RandfRange(-maxRoamRange, maxRoamRange), 0, rng.RandfRange(-maxRoamRange, maxRoamRange));

	public override void _Ready() {
		MyId.NetIDReady += SlowStart;
	}

	public async void SlowStart()
	{
		//Some Spawn Animation
		//await ToSignal(Animator, AnimationPlayer.SignalName.AnimationFinished);


		if(GenericCore.Instance.IsServer)
		{
			GD.PushWarning("Slow Starting Enemies");
			rng.Randomize();
			currentState = EnemyStates.ROAMING;
			NavAgent.TargetPosition = RandomVector + GlobalPosition;
			curSpeed = baseSpeed;
		}
		else if(!MyId.IsLocal)
		{
			
		}
		
	}

    public override void EnemyNavigationFinished()
	{
		GD.Print("Enemy reached a target");
		switch(currentState)
		{
			case EnemyStates.ROAMING:
				Reset();
				break; 

			case EnemyStates.CHASING:
				StopChase();
				break;
		}	
	}

    public override void EnemyNaviagationNotFinished()
    {
        if(currentState == EnemyStates.CHASING)
			NavAgent.TargetPosition = Target.GlobalPosition;
    }



	public override void Server_PlayerEnteredEvent(Node collider) => StartChase((Node3D)collider);
    public override void Server_PlayerExitedEvent(Node collider) => StopChase();

	void StartChase(Node3D target)
	{
		if(currentState == EnemyStates.CHASING) return;
		GD.Print("Found Player. Begin Chasing");
		Target = target;
		NavAgent.TargetPosition = Target.GlobalPosition;
		curSpeed = chaseSpeed;
		SetDeferred(Area3D.PropertyName.Monitoring, false);
		SetDeferred(Area3D.PropertyName.Monitoring, true);
		currentState = EnemyStates.CHASING;
	}

	void StopChase()
	{
		if(currentState != EnemyStates.CHASING) return;
		GD.Print("either lost player or caught player");
		Reset();
		curSpeed = baseSpeed;
		SetDeferred(Area3D.PropertyName.Monitoring, true);
		SetDeferred(Area3D.PropertyName.Monitoring, false);
		Target = null;
		currentState = EnemyStates.ROAMING;
	}

	void Reset()
	{
		var NextPos = RandomVector + GlobalPosition;
		NavAgent.TargetPosition = NextPos;
	}

	void Hit(KinematicCollision3D collider)
	{
		var shape = collider.GetCollider() as Node;
		GD.PushWarning($"Collided With {shape.Name}");
	}

}
