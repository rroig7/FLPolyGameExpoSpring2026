using Godot;
using System;
using System.Diagnostics;

public partial class BossCube : RigidBody3D
{
	
	[Export] public PackedScene SnowBulletScene;
	[Export] public float FireInterval = 5.0f;

	private double _timeSinceLastShot = 0;

	public override void _Process(double delta)
	{
		_timeSinceLastShot += delta;

		if (_timeSinceLastShot >= FireInterval)
		{
			Shoot();
			_timeSinceLastShot = 0;
		}
	}

	private void Shoot()
	{
		var bullet = SnowBulletScene.Instantiate<SnowBullet>();

		// Spawn at shooter position + rotation
		bullet.GlobalTransform = this.GlobalTransform;

		GetTree().CurrentScene.AddChild(bullet);
	}
	
}
