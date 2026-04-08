using Godot;
using System;

public partial class CameraFollow : Camera3D
{
	[Export] public Control LoadingScreen;

	[ExportGroup("Orbit")]
	[Export] float radius        = 8f;
	[Export] float YOffset       = 2f;   // pivot height above player origin
	[Export] float swivelSpeed   = 0.25f; // degrees per pixel (horizontal)
	[Export] float pitchSpeed    = 0.20f; // degrees per pixel (vertical)
	[Export] float pitchMin      = -20f;  // degrees (look up limit)
	[Export] float pitchMax      =  60f;  // degrees (look down limit)
	[Export] float smoothingSpeed = 12f;

	[ExportGroup("Collision")]
	[Export] float collisionMargin = 0.3f; // pull-in buffer so cam doesn't clip

	Node3D Target;
	float swivelAngle = 270f; // horizontal, degrees
	float pitchAngle  =  20f; // vertical,   degrees (positive = looking down)

	// -------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------

	public void SetTarget(Node3D target)
	{
		if (!GenericCore.Instance.IsServer)
			Target = target;
	}

	public float GetFacingYaw()
	{
		float radians = Mathf.DegToRad(swivelAngle + 180f);
		return Mathf.Atan2(Mathf.Cos(radians), Mathf.Sin(radians));
	}

	// -------------------------------------------------------------------
	// Per-frame update
	// -------------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (!IsInstanceValid(Target)) return;

		// --- 1. Compute the pivot point (at player's head height) ---------
		Vector3 pivot = Target.GlobalPosition + Vector3.Up * YOffset;

		// --- 2. Desired camera offset in spherical coordinates ------------
		//   swivelAngle rotates around Y
		//   pitchAngle  tilts up/down (positive = camera above, looking down)
		float radYaw   = Mathf.DegToRad(swivelAngle);
		float radPitch = Mathf.DegToRad(pitchAngle);

		Vector3 desiredOffset = new(
			Mathf.Cos(radYaw)   * Mathf.Cos(radPitch),
			Mathf.Sin(radPitch),
			Mathf.Sin(radYaw)   * Mathf.Cos(radPitch)
		);
		desiredOffset *= radius;

		Vector3 desiredPos = pivot + desiredOffset;

		// --- 3. Collision – raycast from pivot toward desired position -----
		Vector3 finalPos = GetCollisionSafePosition(pivot, desiredPos);

		// --- 4. Smooth camera position ------------------------------------
		GlobalPosition = GlobalPosition.Lerp(finalPos, smoothingSpeed * (float)delta);

		// --- 5. Always look at the STABLE pivot, not the lerping position -
		//   This is the key fix for vertical jitter: we never derive the
		//   look direction from GlobalPosition (which is mid-lerp and
		//   changes every frame), we always aim at the fixed pivot point.
		LookAt(pivot, Vector3.Up);
	}

	// -------------------------------------------------------------------
	// Input
	// -------------------------------------------------------------------

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMove && IsInstanceValid(Target))
		{
			// Horizontal swivel – degrees per pixel, no delta scaling needed
			float dx = mouseMove.Relative.X;
			swivelAngle = ((swivelAngle + dx * swivelSpeed) % 360f + 360f) % 360f;

			// Vertical pitch – clamp so camera stays within comfortable range
			float dy = mouseMove.Relative.Y;
			pitchAngle = Mathf.Clamp(pitchAngle + dy * pitchSpeed, pitchMin, pitchMax);
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
	// Collision helper
	// -------------------------------------------------------------------

	/// <summary>
	/// Casts a ray from <paramref name="pivot"/> toward <paramref name="desired"/>.
	/// Returns the desired position if clear, or pulls the camera back to just
	/// in front of the hit surface so it never phases through geometry.
	/// </summary>
	private Vector3 GetCollisionSafePosition(Vector3 pivot, Vector3 desired)
	{
		var spaceState = GetWorld3D().DirectSpaceState;

		var query = PhysicsRayQueryParameters3D.Create(
			pivot,
			desired,
			// Bitmask: layers 1 + 2 (world/static + rigidbodies). Adjust to match your collision layers.
			collisionMask: 0b11
		);
		// Exclude the player's physics body so the ray doesn't immediately
		// hit the character it's orbiting around.
		if (Target is CollisionObject3D playerCol)
			query.Exclude = new Godot.Collections.Array<Rid> { playerCol.GetRid() };

		var result = spaceState.IntersectRay(query);

		if (result.Count == 0)
			return desired; // clear line of sight – use the full radius

		// Pull back to just in front of the hit surface
		Vector3 hitPos    = result["position"].AsVector3();
		Vector3 hitNormal = result["normal"].AsVector3();
		return hitPos + hitNormal * collisionMargin;
	}
}