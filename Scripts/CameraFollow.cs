using Godot;
using System;

public partial class CameraFollow : Camera3D
{
	[Export] Control LoadingScreen;
	[Export] float radius       = 8f;
	[Export] float YOffset      = 4f;
	[Export] float pitchAngle   = 25f;   // degrees downward; tweak in Inspector
	[Export] float swivelSpeed  = 0.3f;  // degrees per pixel of mouse movement
	[Export] float smoothingSpeed = 8f;
	Node3D Target;
	float swivelAngle = 270f;
	const float MaxAngle = 360f;

	// -------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------

	public void SetTarget(Node3D target)
	{
		if (!GenericCore.Instance.IsServer)
			Target = target;
	}

	// -------------------------------------------------------------------
	// Per-frame update
	// -------------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (!IsInstanceValid(Target)) return;

		float radSwivel = Mathf.DegToRad(swivelAngle);
		float radPitch  = Mathf.DegToRad(pitchAngle);

		// Horizontal offset on XZ plane
		Vector3 flatDir = new(Mathf.Cos(radSwivel), 0f, Mathf.Sin(radSwivel));

		// Lift the offset point upward by radius * sin(pitch), pull it
		// outward by radius * cos(pitch) so the true distance stays = radius.
		Vector3 offset = flatDir * (radius * Mathf.Cos(radPitch));
		offset.Y = YOffset + radius * Mathf.Sin(radPitch);

		Vector3 targetPos  = Target.GlobalPosition + offset;
		Vector3 lerpPos    = GlobalPosition.Lerp(targetPos, smoothingSpeed * (float)delta);

		// Look directly at the player from the lerped position (full 3-D direction,
		// no Y-zeroing) so the camera pitches down naturally.
		Vector3 lookDir = (Target.GlobalPosition - lerpPos).Normalized();

		GlobalPosition = lerpPos;
		Basis = Basis.LookingAt(lookDir, Vector3.Up);
	}

	// -------------------------------------------------------------------
	// Input – mouse swivel & cursor toggle
	// -------------------------------------------------------------------

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMove && IsInstanceValid(Target))
		{
			// Use the event's own relative movement.  swivelSpeed is now
			// "degrees per pixel", so no delta scaling is needed – the
			// relative value is already pixel-space and frame-independent.
			float dx = mouseMove.Relative.X;
			if (Mathf.Abs(dx) > 0f)
			{
				swivelAngle = ((swivelAngle + dx * swivelSpeed) % MaxAngle + MaxAngle) % MaxAngle;
			}
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
	// Helpers
	// -------------------------------------------------------------------

	/// <summary>
	/// Returns the yaw (in radians) the character should face so it looks
	/// in the same direction as the camera.
	/// </summary>
	public float GetFacingYaw()
	{
		// Camera sits swivelAngle *behind* the target → character faces opposite.
		float radians = Mathf.DegToRad(swivelAngle + 180f);
		return Mathf.Atan2(Mathf.Cos(radians), Mathf.Sin(radians));
	}
}