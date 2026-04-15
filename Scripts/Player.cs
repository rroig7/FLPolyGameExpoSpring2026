using Godot;
using System;
using System.Runtime.CompilerServices;

[GlobalClass]
public partial class Player : BaseNetworkedPlayer
{
	CameraFollow playerCam;
	public bool isLocal => MyId.IsLocal;

	[Export]
	public Vector3 SyncVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}

	[Export]
	public Vector3 SyncPosition
	{
		get => GlobalPosition;
		set => OnServerPositionReceived(value);  // intercept the snap
	}

	// --- HP/XP Settings ---
	[ExportGroup("HP & XP")]
	[Export] public float MaxHp = 100f;
	[Export] public float CurrentHp
	{
		get => _currentHp;
		set { _currentHp = value; if (isLocal) UpdateHpBar(); }
	}
	[Export] public int XP
	{
		get => _xp;
		set { _xp = value; if (isLocal) UpdateXpLabel(); }
	}
	[Export] public float RespawnDelay = 3f;

	private float _currentHp = 100f;
	private int _xp = 0;
	private bool _isDead = false;
	public Base PlayerBase;

	// --- Dash settings ---
	[ExportGroup("Dash Settings")]
	[Export] float dashSpeed    = 15f;
	[Export] float dashDuration = 0.10f;
	[Export] float dashCooldown = 3.0f;

	float _dashCooldownTimer = 0f;
	float _dashDurationTimer = 0f;
	bool  _isDashing         = false;

	// --- Jump Settings
	[ExportGroup("Jump Settings")]
	[Export] public float JumpVelocity = 4.5f;
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	// --- Bullet Settings ---
	[ExportGroup("Bullet Settings")]
	[Export] public PackedScene SnowBulletScene;
	[Export] public float BulletDamage = 20f;
	[Export] public float FireRate = 0.3f;
	[Export] public float shootTimer = 0f;
	[Export] public Node3D _muzzle;

	private int _bulletCounter = 0;

	// --- Ultimate Settings ---
	[ExportGroup("Ultimate Ability")]
	[Export] public float UltimateCooldown = 30f;
	[Export] public float UltimateRadius   = 5f;
	[Export] public float UltimateDamage   = 80f;
	[Export] public Node3D UltimateIndicator;

	private float _ultimateTimer   = 0f;
	private bool  _isAimingUltimate = false;

	// --- HUD Settings ---
	[ExportGroup("HUD")]
	[Export] CanvasLayer HUD;
	[Export] ProgressBar HpBar;
	[Export] Label XpLabel;
	[Export] TextureRect UltIcon;
	[Export] Label UltCDLabel;
	[Export] TextureRect DashIcon;
	[Export] Label DashCDLabel;
	[Export] Label RoundTimer;

	public override void _Ready()
	{
		base._Ready();
		CurrentHp = MaxHp;
		_ultimateTimer = UltimateCooldown;

		if (HUD != null)
			HUD.Visible = false;

		MyId.NetIDReady += SlowStart;
	}

	public void SlowStart()
	{
		GD.Print($"Player: SlowStart called. IsLocal={MyId.IsLocal}, IsServer={GenericCore.Instance.IsServer}");
		if (!MyId.IsLocal) return;
		TryAssignCamera();
		Input.MouseMode = Input.MouseModeEnum.Captured;

		if (HUD != null)
			HUD.Visible = true;

		if (UltimateIndicator != null)
			UltimateIndicator.Visible = false;

		// Initialise HUD to current state
		UpdateHpBar();
		UpdateXpLabel();
		UpdateUltimateHud();
		UpdateDashHud();
	}

	private void TryAssignCamera()
	{
		var cam = GetTree().GetFirstNodeInGroup("PLAYERCAMERA") as CameraFollow;
		GD.Print($"Player: PLAYERCAMERA found = {cam != null}");
		if (cam != null)
		{
			cam.SetTarget(this);
			playerCam = cam;
			GD.Print("Player: Camera assigned successfully.");
		}
	}

	public void SetCamera(Camera3D cam) => playerCam = cam as CameraFollow;

	// -------------------------------------------------------
	//  HUD Helpers  (local-only; all guard-checked)
	// -------------------------------------------------------

	/// <summary>Sets the HP bar fill and colour.</summary>
	private void UpdateHpBar()
	{
		if (!isLocal || HpBar == null) return;

		HpBar.MaxValue = MaxHp;
		HpBar.Value    = CurrentHp;

		float ratio = CurrentHp / MaxHp;

		Color barColor;
		if (ratio > 0.5f)
			barColor = Colors.Green.Lerp(Colors.Yellow, 1f - ((ratio - 0.5f) * 2f));
		else
			barColor = Colors.Yellow.Lerp(Colors.Red, 1f - (ratio * 2f));

		var fill = HpBar.GetThemeStylebox("fill") as StyleBoxFlat;
		if (fill != null)
			fill.BgColor = barColor;
	}

	/// <summary>Refreshes the XP counter label.</summary>
	private void UpdateXpLabel()
	{
		if (!isLocal || XpLabel == null) return;
		XpLabel.Text = $"XP: {XP}";
	}

	/// <summary>
	/// Updates the ultimate icon opacity and cooldown label every frame.
	/// Call from LocalProcess so it stays in sync with _ultimateTimer.
	/// </summary>
	private void UpdateUltimateHud()
	{
		if (!isLocal) return;

		bool onCooldown = _ultimateTimer > 0f;

		if (UltIcon != null)
			UltIcon.Modulate = onCooldown ? new Color(1, 1, 1, 0.35f) : Colors.White;

		if (UltCDLabel != null)
		{
			if (onCooldown)
			{
				UltCDLabel.Visible = true;
				UltCDLabel.Text    = Mathf.CeilToInt(_ultimateTimer).ToString();
			}
			else
			{
				UltCDLabel.Visible = false;
			}
		}
	}

	/// <summary>
	/// Updates the dash icon opacity and cooldown label every frame.
	/// Call from LocalProcess so it stays in sync with _dashCooldownTimer.
	/// </summary>
	private void UpdateDashHud()
	{
		if (!isLocal) return;

		bool onCooldown = _dashCooldownTimer > 0f;

		if (DashIcon != null)
			DashIcon.Modulate = onCooldown ? new Color(1, 1, 1, 0.35f) : Colors.White;

		if (DashCDLabel != null)
		{
			if (onCooldown)
			{
				DashCDLabel.Visible = true;
				DashCDLabel.Text    = Mathf.CeilToInt(_dashCooldownTimer).ToString();
			}
			else
			{
				DashCDLabel.Visible = false;
			}
		}
	}

	// -------------------------------------------------------

	public override void LocalProcess(float delta)
	{
		if (_isDead) return;

		if (_dashCooldownTimer > 0f)
			_dashCooldownTimer -= delta;

		if (shootTimer > 0f)
			shootTimer -= delta;

		if (_ultimateTimer > 0f)
			_ultimateTimer -= delta;

		// --- Tick HUD every local frame ---
		UpdateUltimateHud();
		UpdateDashHud();

		if (playerCam == null)
		{
			TryAssignCamera();
			return;
		}

		float camYaw = playerCam.GetFacingYaw();
		//Rpc(MethodName.ProcessMouseLook, camYaw);

		var input = Input.GetVector("Left", "Right", "Forward", "Back");
		Rpc(MethodName.ProcessInput, new Vector3(input.X, 0, input.Y), camYaw);

		if (Input.IsActionJustPressed("Jump"))
		{
			Rpc(MethodName.ProcessJump);
		}

		if (Input.IsActionJustPressed("Dash") && _dashCooldownTimer <= 0f && input != Vector2.Zero)
		{
			_dashCooldownTimer = dashCooldown;
			Rpc(MethodName.ProcessDash);
		}

		if (_isAimingUltimate)
		{
			UpdateUltimateIndicator();

			if (Input.IsActionJustPressed("shoot"))
			{
				GD.Print("Ultimate: confirm input received");
				ConfirmUltimate();
			}

			if (Input.IsActionJustPressed("cancel_ability") ||
				Input.IsActionJustPressed("ui_cancel"))
				CancelUltimate();

			return;
		}

		if (Input.IsActionJustPressed("shoot") && shootTimer <= 0f)
		{
			Vector3 aimPoint = GetCrosshairAimPoint();
			Vector3 aimDir   = _muzzle.GlobalPosition.DirectionTo(aimPoint).Normalized();
			if (aimDir == Vector3.Zero) aimDir = -_muzzle.GlobalTransform.Basis.Z;

			Rpc(MethodName.ProcessBlicky, aimDir);
			shootTimer = FireRate;
		}

		if (Input.IsActionJustPressed("ultimate") && _ultimateTimer <= 0f)
		{
			GD.Print($"Ultimate: entering aim mode, timer={_ultimateTimer}");
			EnterUltimateAiming();
		}
		else if (Input.IsActionJustPressed("ultimate"))
		{
			GD.Print($"Ultimate: on cooldown, timer={_ultimateTimer}");
		}
	}

	public override void AllProcess(float delta)
	{
		if (!IsOnFloor())
		{
			Velocity -= new Vector3(0, gravity * delta, 0);
		}
	}

	public override void ServerProcess(float delta)
	{
		if (_isDashing)
		{
			_dashDurationTimer -= delta;
			if (_dashDurationTimer <= 0f)
			{
				_isDashing = false;
				Velocity = Vector3.Zero;
			}
		}

		if(GameMaster.GameActive && !GameMaster.SuddenDeath)
			Rpc(MethodName.UpdateRoundTimer, MathF.Round((float)GameMaster.Instance.RoundTimer.TimeLeft));
	}

	// -------------------------------------------------------
	//  Remote Calls
	// -------------------------------------------------------

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	void ProcessMouseLook(float yaw)
	{
		Rotation = new Vector3(0, yaw, 0);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, 
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ProcessJump()
	{
		if (!GenericCore.Instance.IsServer) return;
		
		if (IsOnFloor())
		{
			// Set the Y velocity without affecting horizontal momentum
			Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	void ProcessInput(Vector3 playerInput, float yaw)
	{
		Rotation = new Vector3(0, yaw, 0);

		if (!GenericCore.Instance.IsServer) return;
		if (_isDashing) return;

		// Optimized to preserve Velocity.Y (the jump/gravity)
		Vector3 nextVelocity = Velocity;
		
		if (playerInput.LengthSquared() > 0)
		{
			Vector3 dir = (-Basis.Z * playerInput.Z + -Basis.X * playerInput.X).Normalized();
			nextVelocity.X = dir.X * baseSpeed;
			nextVelocity.Z = dir.Z * baseSpeed;
		}
		else
		{
			nextVelocity.X = 0;
			nextVelocity.Z = 0;
		}

		Velocity = nextVelocity;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ProcessDash()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (_isDashing) return;

		_isDashing         = true;
		_dashDurationTimer = dashDuration;
		Velocity           = Basis.Z * dashSpeed;
	}

	/// <summary>
	/// Received by the server from the shooting client.
	/// Server validates the shot then broadcasts spawn data to all peers.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
	TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ProcessBlicky(Vector3 aimDir)
	{
		if (!GenericCore.Instance.IsServer) return;

		int     bulletId = _bulletCounter++;
		Vector3 spawnPos = _muzzle.GlobalPosition;

		Quaternion spawnRot = Transform3D.Identity
			.LookingAt(-aimDir, Vector3.Up)
			.Basis.GetRotationQuaternion();

		Rpc(MethodName.SpawnBulletOnAllPeers, spawnPos, spawnRot, bulletId);
	}

	/// <summary>
	/// Runs on EVERY peer (Authority + CallLocal = true).
	/// Spawns the bullet locally. Server instance gets IsAuthoritative = true;
	/// clients get false so they only move visually.
	///
	/// SetMultiplayerAuthority(1) is called on the bullet so that the server
	/// (peer 1) is allowed to call Rpc(DestroyOnClient) from the bullet node.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void SpawnBulletOnAllPeers(Vector3 spawnPos, Quaternion spawnRot, int bulletId)
	{
		if (SnowBulletScene == null)
		{
			GD.PushWarning("Player: SnowBulletScene is not assigned!");
			return;
		}

		var bullet = SnowBulletScene.Instantiate<SnowBullet>();
		bullet.IsAuthoritative = GenericCore.Instance.IsServer;
		bullet.ShooterId       = (int)MyId.OwnerId;
		bullet.BulletId        = bulletId;

		// Authority must be the server (1) so the bullet's Rpc(DestroyOnClient)
		// call is permitted — RpcMode.Authority means "only the authority may call this"
		bullet.SetMultiplayerAuthority(1);

		var t = Transform3D.Identity;
		t.Origin = spawnPos;
		t.Basis  = new Basis(spawnRot);
		bullet.GlobalTransform = t;

		GetTree().CurrentScene.AddChild(bullet);
	}

	/// <summary>
	/// Client → Server. Server validates then broadcasts the effect to all peers.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ProcessUltimate(Vector3 center)
	{
		GD.Print($"ProcessUltimate: called, IsServer={GenericCore.Instance.IsServer}, center={center}");
		if (!GenericCore.Instance.IsServer) return;

		var spaceState = GetWorld3D().DirectSpaceState;
		var shape = new SphereShape3D();
		shape.Radius = UltimateRadius;

		var query = new PhysicsShapeQueryParameters3D();
		query.Shape        = shape;
		query.Transform    = new Transform3D(Basis.Identity, center);
		query.CollisionMask = 1 << (3 - 1);
		query.Exclude      = new Godot.Collections.Array<Rid> { GetRid() };

		var results = spaceState.IntersectShape(query);
		GD.Print($"ProcessUltimate: found {results.Count} colliders in range");

		foreach (var hit in results)
		{
			var collider = ((Godot.Collections.Dictionary)hit)["collider"].As<GodotObject>();
			GD.Print($"ProcessUltimate: collider={collider}, type={collider?.GetType().Name}, isPlayer={collider is Player}");

			if (collider is Node hitNode)
			{
				if (hitNode.IsInGroup("enemies"))
					hitNode.Call("OnHitByBullet");
				else if (hitNode is Player hitPlayer)
				{
					GD.Print($"ProcessUltimate: found player {hitPlayer.Name}, OwnerId={hitPlayer.MyId.OwnerId}, myOwnerId={MyId.OwnerId}");
					if (hitPlayer.MyId.OwnerId != MyId.OwnerId)
						hitPlayer.TakeDamage(UltimateDamage);
					else
						GD.Print("ProcessUltimate: skipping self");
				}
			}
		}

		Rpc(MethodName.PlayUltimateEffectOnClients, center);
	}

	/// <summary>
	/// Server → all clients. Trigger the particle effect at the confirmed position.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void PlayUltimateEffectOnClients(Vector3 center)
	{
		// TODO: instantiate your explosion/impact particle scene here
		GD.Print($"Ultimate effect at {center}");
	}

	/// <summary>
	/// Server → all peers. Disables the player visually and blocks input.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void OnDiedOnAllPeers()
	{
		_isDead = true;
		Visible = false; // hide the player model

		// Hide HUD on death (local player only)
		if (isLocal && HUD != null)
			HUD.Visible = false;
	}

	/// <summary>
	/// Server → all peers. Re-enables the player at the spawn position.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void OnRespawnedOnAllPeers(Vector3 spawnPos)
	{
		_isDead    = false;
		CurrentHp  = MaxHp;
		ResetServerPosition(spawnPos);
		Visible    = true;

		// Restore HUD and refresh all values on respawn
		if (isLocal)
		{
			if (HUD != null) HUD.Visible = true;
			UpdateHpBar();
			UpdateXpLabel();
			UpdateUltimateHud();
			UpdateDashHud();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	void UpdateRoundTimer(float t)
	{
		if(!isLocal) return;

		RoundTimer.Text = $"{t:F1}";		
		if(t < 0.1f) RoundTimer.Text = "SUDDEN DEATH";
	}

	/// <summary>
	/// Called server-side only. Applies damage and handles death.
	/// </summary>
	public void TakeDamage(float amount)
	{
		if (!GenericCore.Instance.IsServer) return;
		if (_isDead) return;

		CurrentHp = Mathf.Max(CurrentHp - amount, 0f);
		GD.Print($"{Name} took {amount} damage, HP={CurrentHp}/{MaxHp}");

		if (CurrentHp <= 0f)
			Die();
	}

	private void Die()
	{
		if (_isDead) return;
		_isDead = true;
		GD.Print($"{Name} died");

		Rpc(MethodName.OnDiedOnAllPeers);

		// Start respawn timer on the server if base is alive, otherwise player is eliminated
		if(IsInstanceValid(PlayerBase)) GlobalTimers.Instance.OneShotTimer(RespawnDelay).Timeout += Respawn;
		else GameMaster.Instance.PlayerEliminated();
	}

	private void Respawn()
	{
		if (!GenericCore.Instance.IsServer) return;

		CurrentHp = MaxHp;
		_isDead    = false;

		// Find a spawn point — looks for nodes in the SpawnPoints group
		Vector3 spawnPos = PlayerBase.Spawnpoint.GlobalPosition;
		GD.Print($"{Name} respawning at {spawnPos}");

		Rpc(MethodName.OnRespawnedOnAllPeers, spawnPos);
	}

	// --- Helper Functions ---

	/// <summary>
	/// Returns the world-space point the crosshair is aimed at.
	/// Falls back to a point far ahead of the muzzle if nothing is hit.
	/// </summary>
	private Vector3 GetCrosshairAimPoint()
	{
		if (playerCam == null) return _muzzle.GlobalPosition + _muzzle.GlobalTransform.Basis.Z * 100f;

		var viewport = GetViewport();
		Vector2 screenCenter = viewport.GetVisibleRect().Size / 2f;

		Vector3 rayOrigin = playerCam.ProjectRayOrigin(screenCenter);
		Vector3 rayDir    = playerCam.ProjectRayNormal(screenCenter);
		float   rayLength = 500f;
		Vector3 rayEnd    = rayOrigin + rayDir * rayLength;

		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd, collisionMask: 0b11);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var result = spaceState.IntersectRay(query);
		return result.Count > 0 ? result["position"].AsVector3() : rayEnd;
	}

	private void EnterUltimateAiming()
	{
		_isAimingUltimate = true;
		if (UltimateIndicator != null)
		{
			UltimateIndicator.Visible = true;
			// Scale the indicator to match the configured radius
			UltimateIndicator.Scale = Vector3.One * UltimateRadius;
		}
	}

	private void CancelUltimate()
	{
		_isAimingUltimate = false;
		if (UltimateIndicator != null)
			UltimateIndicator.Visible = false;
	}

	private void ConfirmUltimate()
	{
		Vector3 aimPoint = GetGroundAimPoint();
		CancelUltimate(); // hides indicator

		_ultimateTimer = UltimateCooldown;
		Rpc(MethodName.ProcessUltimate, aimPoint);
	}

	/// <summary>
	/// Casts a ray from the camera to the ground plane only (layer 1),
	/// so the indicator always lands on terrain rather than on characters.
	/// </summary>
	private Vector3 GetGroundAimPoint()
	{
		if (playerCam == null) return GlobalPosition;

		var viewport = GetViewport();
		Vector2 screenCenter = viewport.GetVisibleRect().Size / 2f;

		Vector3 rayOrigin = playerCam.ProjectRayOrigin(screenCenter);
		Vector3 rayDir    = playerCam.ProjectRayNormal(screenCenter);
		Vector3 rayEnd    = rayOrigin + rayDir * 500f;

		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(
			rayOrigin, rayEnd,
			collisionMask: 1 | 2 // terrain/static layer only
		);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var result = spaceState.IntersectRay(query);
		return result.Count > 0 ? result["position"].AsVector3() : GlobalPosition;
	}

	/// <summary>
	/// Called every frame while aiming — moves the indicator to where the
	/// crosshair ray hits the ground.
	/// </summary>
	private void UpdateUltimateIndicator()
	{
		if (UltimateIndicator == null) return;
		Vector3 groundPoint = GetGroundAimPoint();
		// Lift slightly off the ground so it isn't clipped by the terrain
		UltimateIndicator.GlobalPosition = groundPoint + Vector3.Up * 0.05f;
	}
}
