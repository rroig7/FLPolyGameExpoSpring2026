using Godot;
using System;

public partial class RoamingEnemy : BaseNetworkedEnemy
{
	RandomNumberGenerator rng = new();

	public override void _Ready() {
		MyId.NetIDReady += SlowStart;
	}

	public async void SlowStart()
	{
		//Some Spawn Animation
		//await ToSignal(Animator, AnimationPlayer.SignalName.AnimationFinished);

		if(GenericCore.Instance.IsServer)
		{
			rng.Randomize();
			currentState = EnemyStates.ROAMING;
			curSpeed = baseSpeed;
			NextCheckPoint();
		}
		else if(!MyId.IsLocal)
		{
			
		}
	}

    public override void EnemyNavigationFinished()
	{
		GD.Print("Enemy reached a target");
		NextCheckPoint();
	}


	void NextCheckPoint()
	{
		NavAgent.TargetPosition = NavigationServer3D.MapGetRandomPoint(NavAgent.GetNavigationMap(), NavAgent.NavigationLayers, false);
	}

	public override void ServerMoveAndSlideHit(KinematicCollision3D collider)
	{
		var shape = collider.GetCollider() as Node;
		GD.PushWarning($"Collided With {shape.Name}");
	}
}
