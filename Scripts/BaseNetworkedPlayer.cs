using Godot;
using System;

public partial class BaseNetworkedPlayer : CharacterBody3D
{
	[Export] protected AnimationPlayer Animator;
	[Export] public NetID MyId {get; private set;}
	[Export] protected float baseSpeed;

	// How far the client can drift from the server position before a
	// hard correction is applied. Below this threshold, drift is
	// smoothed out gradually instead of snapping.
	[Export] float correctionThreshold = 3.0f;

	// How quickly the client smoothly corrects toward the server position.
	// Higher = snappier correction, lower = smoother but more drift.
	// FIX: lowered default from 15 to 10 — the old value combined with the
	// drift-ratio multiplier (now removed) could push 't' near 1.0 on large
	// small-drift frames, making corrections look like snaps.
	[Export] float correctionSpeed = 10f;

	Vector3 _serverPosition;
	bool    _serverPositionInitialized = false;

	public override void _Ready()
	{
		_serverPosition = GlobalPosition;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (GenericCore.Instance.IsServer)
		{
			MoveAndSlide();
			ServerProcess((float)delta);
		}
		else
		{
			MoveAndSlide();

			if (!_serverPositionInitialized)
			{
				_serverPosition            = GlobalPosition;
				_serverPositionInitialized = true;
			}

			float drift = GlobalPosition.DistanceTo(_serverPosition);

			if (drift > correctionThreshold)
			{
				// Large drift — snap immediately (teleport, spawn, hard desync).
				GlobalPosition = _serverPosition;
			}
			else if (drift > 0.001f)
			{
				// FIX: use a flat lerp factor instead of scaling by (drift / correctionThreshold).
				// The old formula let 't' spike toward 1.0 as drift approached the threshold,
				// which made corrections near the snap boundary look like visible pops.
				// A flat factor produces a consistent, invisible nudge every frame.
				float t = Mathf.Clamp(correctionSpeed * (float)delta, 0f, 1f);
				GlobalPosition = GlobalPosition.Lerp(_serverPosition, t);
			}
		}

		if (MyId.IsLocal)
			LocalProcess((float)delta);

		// FIX: AllPlayerProcess now runs on clients only (unchanged), but AllProcess
		// is intentionally NOT called here anymore — it was running gravity on both
		// server and clients, which caused vertical jitter. Gravity is now server-only,
		// applied inside ServerProcess in the subclass. If you need a true "runs
		// everywhere" hook for non-physics logic, re-add AllProcess calls here and
		// make sure subclass overrides never touch Velocity.Y inside it.
		if (!GenericCore.Instance.IsServer)
			AllPlayerProcess((float)delta);
	}

	public void ResetServerPosition(Vector3 pos)
	{
		GlobalPosition             = pos;
		_serverPosition            = pos;
		_serverPositionInitialized = true;
	}

	/// <summary>
	/// Called by the MultiplayerSynchronizer when a new authoritative
	/// position arrives from the server. Store it for smooth correction
	/// rather than applying it directly to GlobalPosition.
	/// </summary>
	public void OnServerPositionReceived(Vector3 serverPos)
	{
		_serverPosition = serverPos;
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
