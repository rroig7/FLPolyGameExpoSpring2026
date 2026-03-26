using Godot;
using System;

public partial class CameraFollow : Camera3D
{
	[Export] Control LoadingScreen;
	[Export] float radius;
	[Export] float YOffset;
	[Export] float swivelSpeed;
	[Export] float smoothingSpeed;
	Node3D Target;
	float swivelAngle = 270;
	float MaxAngle = 360;

	public override void _Ready() {
		
		/* This was with spawn eggs, however, server doesn't replicate spawn to clients
		if(GenericCore.Instance.IsServer)
		{
			//QueueFree();
			return;
		}
		
		var player = GetTree().GetFirstNodeInGroup("PLAYER") as Player;

		if(player.isLocal)
		{
			Target = player;
			player.SetCamera(this);			
		}
		else { QueueFree(); }
		*/
	}

	public void SetTarget(Node3D target)
	{
		if(!GenericCore.Instance.IsServer)
			Target = target;
		
		LoadingScreen.Hide();
	}

	public override void _Process(double delta) 
	{
		if(IsInstanceValid(Target))
		{
			//Compute the direction and magnitude of the offset on the XZ, apply Y offset
			var radianSwivelAngle = Mathf.DegToRad(swivelAngle);
			Vector3 completeOffset = new(Mathf.Cos(radianSwivelAngle), 0f, Mathf.Sin(radianSwivelAngle));
			completeOffset *= radius;
			completeOffset.Y = YOffset;

			//Apply the offset to the current position of the tatget and ensure camera is looking at target
			var TargetPosition = completeOffset + Target.GlobalPosition;
			var LerpPosition = GlobalPosition.Lerp(TargetPosition, smoothingSpeed * (float)delta);
			var TargetDir = (Target.GlobalPosition - LerpPosition).Normalized();
			TargetDir.Y = 0;
			
			Basis = Basis.LookingAt(TargetDir, Vector3.Up);
			GlobalPosition = LerpPosition;			
		}
	}

    public override void _Input(InputEvent @event)
    {
        if(@event is InputEventMouseMotion mouseMove)
		{
			//Should work, target will  be null when game is over or a player DCs
			if(IsInstanceValid(Target))
			{
				var deltaTime = (float)GetProcessDeltaTime();
				Vector2 mouseVel;
				mouseVel = mouseMove.Relative;

				//Did the user move their mouse
				if(mouseVel.LengthSquared() > 0)
				{
					var xDir = (mouseVel.X > 0) ? 1 : -1;
					var yDir = (mouseVel.Y > 0) ? 1 : -1;

					swivelAngle = (swivelAngle + (swivelSpeed * xDir * deltaTime)) % MaxAngle;
					if(swivelAngle < 0) swivelAngle = MaxAngle; 
				}
			}
		}

	    if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (keyEvent.Keycode == Key.T)
			{
				if(Input.MouseMode == Input.MouseModeEnum.Captured)
					Input.MouseMode = Input.MouseModeEnum.Visible;
				
				else
					Input.MouseMode = Input.MouseModeEnum.Captured;
			}
		}
    }

}
