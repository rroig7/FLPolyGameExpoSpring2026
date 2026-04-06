using Godot;
using System;

public partial class BaseNetworkedPlayer : CharacterBody3D
{
	[Export] protected AnimationPlayer Animator;
	[Export] protected NetID MyId;
	[Export] protected float baseSpeed;

	public override void _PhysicsProcess(double delta)
	{
		//GD.Print($"BNP: IsServer={GenericCore.Instance.IsServer}, IsLocal={MyId.IsLocal}, Velocity={Velocity}");
		if (GenericCore.Instance.IsServer)
		{
			MoveAndSlide();
			ServerProcess((float)delta);
		}
		if (MyId.IsLocal)
		{
			LocalProcess((float)delta);
		}
		if (!GenericCore.Instance.IsServer)
		{
			AllPlayerProcess((float)delta);
		}

		AllProcess((float)delta);
	}

	void OnCollisionEntered(Node collider)
	{
		if (GenericCore.Instance.IsServer)
			ServerCollisionEvent(collider);
		if (MyId.IsLocal)
			LocalCollisionEvent(collider);
		if (!GenericCore.Instance.IsServer)
			AllPlayerCollisionEvent(collider);

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
