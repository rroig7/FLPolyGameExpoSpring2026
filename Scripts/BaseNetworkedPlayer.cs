using Godot;
using System;

public partial class BaseNetworkedPlayer : CharacterBody3D
{
	[Export] protected AnimationPlayer Animator;
	[Export] protected NetID MyId;
	[Export] protected float baseSpeed;

	// How far the client can drift from the server position before a
	// hard correction is applied. Below this threshold, drift is
	// smoothed out gradually instead of snapping.
	[Export] float correctionThreshold = 3.0f;

	// How quickly the client smoothly corrects toward the server position.
	// Higher = snappier correction, lower = smoother but more drift.
	[Export] float correctionSpeed = 15f;

	// Last authoritative position received from the server via the
	// MultiplayerSynchronizer. Keep this separate from GlobalPosition
	// so we can interpolate toward it rather than snapping.
	Vector3 _serverPosition;
	bool     _serverPositionInitialized = false;

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
			// Dead-reckon using replicated velocity so the character
			// moves smoothly between network snapshots.
			MoveAndSlide();

			// On the first snapshot, initialize without smoothing
			if (!_serverPositionInitialized)
			{
				_serverPosition      = GlobalPosition;
				_serverPositionInitialized = true;
			}

			// Measure drift between where dead-reckoning put us and
			// where the server says we should be.
			float drift = GlobalPosition.DistanceTo(_serverPosition);

			if (drift > correctionThreshold)
			{
				// Large drift (e.g. teleport, spawn, hard desync) – snap immediately
				GlobalPosition = _serverPosition;
			}
			else if (drift > 0.001f)
			{
				// Small drift – nudge smoothly toward server position so
				// the correction is invisible to the player.
				float t = Mathf.Clamp(correctionSpeed * (float)delta * (drift / correctionThreshold), 0f, 1f);
    			GlobalPosition = GlobalPosition.Lerp(_serverPosition, t);
			}
		}

		if (MyId.IsLocal)
			LocalProcess((float)delta);

		if (!GenericCore.Instance.IsServer)
			AllPlayerProcess((float)delta);

		AllProcess((float)delta);
	}

	public void ResetServerPosition(Vector3 pos)
	{
		GlobalPosition = pos;
		_serverPosition = pos;
		_serverPositionInitialized = true; // ensure smoothing doesn't re-init stale
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
