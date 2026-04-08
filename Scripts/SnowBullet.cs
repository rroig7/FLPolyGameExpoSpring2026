using Godot;
using System;

public partial class SnowBullet : CharacterBody3D
{
	// Called when the node enters the scene tree for the first time.
	[Export] public float bulletSpeed = 15.0f;
	[Export] public float bulletLifetime = 3.0f;
	
	public override void _Ready()
	{
		GetTree().CreateTimer(bulletLifetime).Timeout += QueueFree;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	
	public override void _PhysicsProcess(double delta)
	{
		// Move forward in the bullet's local Z direction
		Velocity = Transform.Basis.Z * bulletSpeed;
        
		KinematicCollision3D collision = MoveAndCollide(Velocity * (float)delta);
        
		if (collision != null)
		{
			GodotObject hit = collision.GetCollider();
			if (hit is Node3D node && node.HasMethod("TakeDamage"))
			{
				node.Call("TakeDamage", 10);
			}
			QueueFree();
		}
	}
}
