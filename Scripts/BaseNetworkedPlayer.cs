using Godot;
using System;

public partial class BaseNetworkedPlayer : RigidBody3D
{
	[Export] protected AnimationPlayer Animator;
	[Export] protected NetID MyId;
	[Export] protected float baseSpeed;


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(GenericCore.Instance.IsServer)
		{
			//Server Only
			ServerProcess((float)delta);
		}
		if(MyId.IsLocal)
		{
			//Local Player only
			LocalProcess((float)delta);
		}
		if(!GenericCore.Instance.IsServer)
		{
			//All players but server
			AllPlayerProcess((float)delta);
		}

		//Everyone
		AllProcess((float)delta);
	}

	void OnCollisionEntered(Node collider)
	{
		if(GenericCore.Instance.IsServer)
		{
			//Server Only
			ServerCollisionEvent(collider);
		}
		if(MyId.IsLocal)
		{
			//Local Player only
			LocalCollisionEvent(collider);
		}
		if(!GenericCore.Instance.IsServer)
		{
			//All players but server
			AllPlayerCollisionEvent(collider);
		}

		//Everyone
		AllCollisionEvent(collider);
	}

	//------ Process Handlers ------\\
	public virtual void ServerProcess(float delta) {}
	public virtual void LocalProcess(float delta) {}
	public virtual void AllPlayerProcess(float delta) {}
	public virtual void AllProcess(float delta) {}


	//------ Collision Handlers -------\\
	public virtual void ServerCollisionEvent(Node collider) {}
	public virtual void LocalCollisionEvent(Node collider) {}
	public virtual void AllPlayerCollisionEvent(Node collider) {}
	public virtual void AllCollisionEvent(Node collider) {}
	
}
