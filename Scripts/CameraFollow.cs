using Godot;
using System;

public partial class CameraFollow : Camera3D
{
	[Export] public Control LoadingScreen;

	[ExportGroup("Orbit")]
	[Export] float radius         = 8f;
	[Export] float YOffset        = 2f;    // pivot height above player origin
	[Export] float swivelSpeed    = 0.25f; // degrees per pixel (horizontal)
	[Export] float pitchSpeed     = 0.20f; // degrees per pixel (vertical)
	[Export] float pitchMin       = -20f;  // degrees (look up limit)
	[Export] float pitchMax       =  60f;  // degrees (look down limit)
	[Export] float smoothingSpeed = 12f;

	[ExportGroup("Collision")]
	[Export] float collisionMargin = 0.3f;

	Node3D Target;
	float swivelAngle = 270f;
	float pitchAngle  =  20f;

	// -------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------

	public void SetTarget(Node3D target)
	{
		if (!GenericCore.Instance.IsServer)
			Target = target;
		
		var LS = GetTree().GetFirstNodeInGroup("LoadingScreen") as Control; 
		GD.Print($"CameraFollow: Turning off loading screen: {LS != null} {LS.Visible}");
		LS?.Hide();
	}

	public float GetFacingYaw()
	{
		float radians = Mathf.DegToRad(swivelAngle + 180f);
		return Mathf.Atan2(Mathf.Cos(radians), Mathf.Sin(radians));
	}

	// -------------------------------------------------------------------
	// Input
	// -------------------------------------------------------------------

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMove && IsInstanceValid(Target) && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			swivelAngle = ((swivelAngle + mouseMove.Relative.X * swivelSpeed) % 360f + 360f) % 360f;
			pitchAngle  = Mathf.Clamp(pitchAngle + mouseMove.Relative.Y * pitchSpeed, pitchMin, pitchMax);
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (keyEvent.Keycode == Key.T)
			{
				Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
					? Input.MouseModeEnum.Visible
					: Input.MouseModeEnum.Captured;
			}
		}
	}

	// -------------------------------------------------------------------
	// Per-frame update
	// -------------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (!IsInstanceValid(Target)) return;

		float radYaw   = Mathf.DegToRad(swivelAngle);
		float radPitch = Mathf.DegToRad(pitchAngle);

		// Unit vector pointing from pivot toward the camera in world space
		Vector3 orbitDir = new(
			Mathf.Cos(radYaw) * Mathf.Cos(radPitch),
			Mathf.Sin(radPitch),
			Mathf.Sin(radYaw) * Mathf.Cos(radPitch)
		);

		Vector3 pivot      = Target.GlobalPosition + Vector3.Up * YOffset;
		Vector3 desiredPos = pivot + orbitDir * radius;
		Vector3 finalPos   = GetCollisionSafePosition(pivot, desiredPos);

		// Lerp position for smooth follow
		GlobalPosition = GlobalPosition.Lerp(finalPos, smoothingSpeed * (float)delta);

		// Build rotation purely from the orbit angles – completely independent
		// of GlobalPosition so the lerp never introduces look-direction wobble.
		//
		// The camera looks in the OPPOSITE direction of orbitDir (it faces the
		// pivot, not away from it), then we build a Basis from that.
		Vector3 lookDir = -orbitDir;
		Basis = Basis.LookingAt(lookDir, Vector3.Up);
	}

	// -------------------------------------------------------------------
	// Collision helper
	// -------------------------------------------------------------------

	private Vector3 GetCollisionSafePosition(Vector3 pivot, Vector3 desired)
	{
		var spaceState = GetWorld3D().DirectSpaceState;

		var query = PhysicsRayQueryParameters3D.Create(
			pivot,
			desired,
			collisionMask: 0b11
		);

		if (Target is CollisionObject3D playerCol)
			query.Exclude = new Godot.Collections.Array<Rid> { playerCol.GetRid() };

		var result = spaceState.IntersectRay(query);

		if (result.Count == 0)
			return desired;

		Vector3 hitPos    = result["position"].AsVector3();
		Vector3 hitNormal = result["normal"].AsVector3();
		return hitPos + hitNormal * collisionMargin;
	}
}