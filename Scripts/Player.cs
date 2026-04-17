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
		set => OnServerPositionReceived(value);
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
	private int   _xp        = 0;
	private bool  _isDead    = false;
	public Base PlayerBase;
	public string PName = "Player";
	[Export] public bool inBase = true;

	// --- Dash Settings ---
	[ExportGroup("Dash Settings")]
	[Export] public float dashSpeed    = 15f;
	[Export] public float dashDuration = 0.10f;
	[Export] public float dashCooldown = 3.0f;

	float _dashCooldownTimer = 0f;
	float _dashDurationTimer = 0f;
	bool  _isDashing         = false;

	// --- Jump Settings ---
	[ExportGroup("Jump Settings")]
	[Export] public float JumpVelocity = 4.5f;
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	// --- Bullet Settings ---
	[ExportGroup("Bullet Settings")]
	[Export] public float BulletDamage = 20f;
	[Export] public float FireRate     = 0.3f;
	[Export] public float shootTimer   = 0f;
	[Export] public Node3D _muzzle;

	[Signal] public delegate void BulletSpawnRequestedEventHandler(Vector3 origin, Quaternion rotation, int bulletId, int shooterId);

	private int _bulletCounter = 0;

	// --- Ultimate Settings ---
	[ExportGroup("Ultimate Ability")]
	[Export] public float UltimateCooldown = 30f;
	[Export] public float UltimateRadius   = 5f;
	[Export] public float UltimateDamage   = 80f;
	[Export] public Node3D UltimateIndicator;
	[Export] public Node3D UltimateModel;
	[Export] public float UltimateRiseHeight   = 3f;
	[Export] public float UltimateRiseDuration = 0.4f;
	[Export] public float UltimateFallDuration = 0.3f;
	[Export] public float UltimateGroundOffset = 0f;
	[Export] public float UltimateSubmergeDepth = 2f;
	[Export] public AnimationPlayer UltimateAnimPlayer;
	[Export] public string UltimateAnimName = "";

	private float _ultimateTimer    = 0f;
	private bool  _isAimingUltimate = false;

	// --- HUD ---
	[ExportGroup("HUD")]
	[Export] CanvasLayer HUD;
	[Export] Label NameLabel;
	[Export] ProgressBar HpBar;
	[Export] Label XpLabel;
	[Export] TextureRect UltIcon;
	[Export] Label UltCDLabel;
	[Export] TextureRect DashIcon;
	[Export] Label DashCDLabel;
	[Export] Label RoundTimer;
	[Export] Control UpgradeUIButton;
	[Export] Upgrades UpgradeMenu;

	[ExportGroup("Animation")]
	[Export] public AnimationTree AnimTree;
	private AnimationNodeStateMachinePlayback StateMachine;
	private StringName _pendingActionAnim = null;

	// --- Internal state ---
	private Vector3 _knockbackVelocity = Vector3.Zero;
	private Vector3 _lastSentInput     = Vector3.Zero;
	private Vector3 _serverLastInput   = Vector3.Zero;
	private float   _lastSentYaw       = float.MaxValue;

	// -------------------------------------------------------
	//  Lifecycle
	// -------------------------------------------------------

	public override void _Ready()
	{
		base._Ready();
		CurrentHp      = MaxHp;
		_ultimateTimer = UltimateCooldown;

		if (HUD != null)
			HUD.Visible = false;

		StateMachine = (AnimationNodeStateMachinePlayback)
			AnimTree.Get("parameters/playback");

		UpdatePName(); // show whatever we have initially
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

		UpdateHpBar();
		UpdateXpLabel();
		UpdateUltimateHud();
		UpdateDashHud();
		UpdatePName();
		GameMaster.Instance.SuddenDeathTrigger += ExitBase;
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
	//  HUD Helpers
	// -------------------------------------------------------

	private void UpdatePName()
	{
		if (NameLabel != null)
			NameLabel.Text = PName;
	}

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

	private void UpdateXpLabel()
	{
		if (!isLocal || XpLabel == null) return;
		XpLabel.Text = $"XP: {XP}";
	}

	private void UpdateUltimateHud()
	{
		if (!isLocal) return;
		bool onCooldown = _ultimateTimer > 0f;

		if (UltIcon != null)
			UltIcon.Modulate = onCooldown ? new Color(1, 1, 1, 0.35f) : Colors.White;

		if (UltCDLabel != null)
		{
			UltCDLabel.Visible = onCooldown;
			if (onCooldown) UltCDLabel.Text = Mathf.CeilToInt(_ultimateTimer).ToString();
		}
	}

	private void UpdateDashHud()
	{
		if (!isLocal) return;
		bool onCooldown = _dashCooldownTimer > 0f;

		if (DashIcon != null)
			DashIcon.Modulate = onCooldown ? new Color(1, 1, 1, 0.35f) : Colors.White;

		if (DashCDLabel != null)
		{
			DashCDLabel.Visible = onCooldown;
			if (onCooldown) DashCDLabel.Text = Mathf.CeilToInt(_dashCooldownTimer).ToString();
		}
	}

	// -------------------------------------------------------
	//  Per-frame — client input collection
	// -------------------------------------------------------

	// -------------------------------------------------------
	//  Animation Handling
	// -------------------------------------------------------

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (_isDead || StateMachine == null) return;

		bool isMoving = new Vector2(Velocity.X, Velocity.Z).LengthSquared() > 0.05f;
		UpdateAnimation(isMoving);
	}

	public override void LocalProcess(float delta)
	{
		if (_isDead || !GameMaster.GameActive) return;

		if (_dashCooldownTimer > 0f) _dashCooldownTimer -= delta;
		if (shootTimer > 0f)         shootTimer         -= delta;
		if (_ultimateTimer > 0f)     _ultimateTimer     -= delta;

		UpdateUltimateHud();
		UpdateDashHud();

		if (playerCam == null) { TryAssignCamera(); return; }

		float  camYaw   = playerCam.GetFacingYaw();
		var    input    = Input.GetVector("Left", "Right", "Forward", "Back");
		var    inputVec = new Vector3(input.X, 0, input.Y);

		bool inputChanged = inputVec != _lastSentInput;
		bool yawChanged   = Mathf.Abs(camYaw - _lastSentYaw) > 0.001f;

		if (inputChanged || yawChanged)
		{
			_lastSentInput = inputVec;
			_lastSentYaw   = camYaw;
			RpcId(1, MethodName.ServerReceiveMovement, inputVec, camYaw);
		}

		// ── Jump ─────────────────────────────────────────────
		if (Input.IsActionJustPressed("Jump"))
		{
			RpcId(1, MethodName.ServerReceiveJump);
		}

		// ── Dash ──────────────────────────────────────────────
		if (Input.IsActionJustPressed("Dash") && _dashCooldownTimer <= 0f && input != Vector2.Zero)
		{
			_dashCooldownTimer = dashCooldown;
			_lastSentInput     = Vector3.Zero;
			RpcId(1, MethodName.ServerReceiveDash);
		}

		// ── Ultimate aiming mode ──────────────────────────────
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

		if (Input.IsActionJustPressed("shoot") && shootTimer <= 0f && !UpgradeMenu.Visible)
		{
			Vector3 aimPoint = GetCrosshairAimPoint();
			Vector3 aimDir   = _muzzle.GlobalPosition.DirectionTo(aimPoint).Normalized();
			if (aimDir == Vector3.Zero) aimDir = -_muzzle.GlobalTransform.Basis.Z;

			RpcId(1, MethodName.ServerReceiveFire, aimDir);
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

		// ── Upgrade Menu ──────────────────────────────
		if(Input.IsActionJustPressed("Upgrade") && inBase && !_isAimingUltimate)
		{
			UpgradeMenu.Visible = !UpgradeMenu.Visible;
			(UpgradeUIButton.GetParent() as Control).Visible = !UpgradeMenu.Visible;
			HpBar.Visible = !UpgradeMenu.Visible;
			XpLabel.Visible = !UpgradeMenu.Visible;
			
			if(UpgradeMenu.Visible)
				Input.MouseMode = Input.MouseModeEnum.Confined;
			else
				Input.MouseMode = Input.MouseModeEnum.Captured;

			GD.Print($"Opening Menu: ");
		}
	}

	// -------------------------------------------------------
	//  Per-frame — server physics
	// -------------------------------------------------------

	public override void ServerProcess(float delta)
	{
		if (!IsOnFloor())
			Velocity -= new Vector3(0, gravity * delta, 0);

		if (_isDashing)
		{
			_dashDurationTimer -= delta;
			if (_dashDurationTimer <= 0f)
			{
				_isDashing = false;
				if (_serverLastInput.LengthSquared() > 0)
				{
					Vector3 dir = (-Basis.Z * _serverLastInput.Z + -Basis.X * _serverLastInput.X).Normalized();
					Velocity = new Vector3(dir.X * baseSpeed, Velocity.Y, dir.Z * baseSpeed);
				}
				else
				{
					Velocity = new Vector3(0, Velocity.Y, 0);
				}
			}
		}

		if (GameMaster.GameActive && !GameMaster.SuddenDeath && IsInstanceValid(GameMaster.Instance.RoundTimer))
			Rpc(MethodName.ClientSyncRoundTimer, MathF.Round((float)GameMaster.Instance.RoundTimer.TimeLeft));

		if (_knockbackVelocity != Vector3.Zero)
		{
			Velocity           += _knockbackVelocity;
			_knockbackVelocity  = _knockbackVelocity.Lerp(Vector3.Zero, 0.3f);
			if (_knockbackVelocity.Length() < 0.1f)
				_knockbackVelocity = Vector3.Zero;
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void EnteredBase()
	{
		if(!isLocal) return;
		else UpgradeUIButton.Show();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ExitBase()
	{
		if(!isLocal) return;
		else
		{
			UpgradeUIButton.Hide();
			UpgradeMenu.Hide();
			Input.MouseMode = Input.MouseModeEnum.Captured;
			
			foreach(Control menu in UpgradeMenu.GetChildren())
			{
				if(menu.Name.ToString().Contains("Upgrades")) menu.Visible = false;
			}

			(UpgradeUIButton.GetParent() as Control).Visible = true;
			HpBar.Visible = true;
			XpLabel.Visible = true;			
		}
	}
	// -------------------------------------------------------
	//  Server-side RPC receivers (AnyPeer → server only)
	// -------------------------------------------------------

	/// <summary>
	/// Receives movement direction and camera yaw from the owning client.
	/// Authoritative movement runs here on the server.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void ServerReceiveMovement(Vector3 playerInput, float yaw)
	{
		if (!GenericCore.Instance.IsServer) return;
		if (Multiplayer.GetRemoteSenderId() != MyId.OwnerId) return;

		Rotation         = new Vector3(0, yaw, 0);
		_serverLastInput = playerInput;

		if (_isDashing) return;
		if (_knockbackVelocity.Length() > 0.5f) return;

		if (playerInput.LengthSquared() > 0)
		{
			Vector3 dir = (-Basis.Z * playerInput.Z + -Basis.X * playerInput.X).Normalized();
			Velocity = new Vector3(dir.X * baseSpeed, Velocity.Y, dir.Z * baseSpeed);
		}
		else
		{
			Velocity = new Vector3(0, Velocity.Y, 0);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void ServerReceiveJump()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (Multiplayer.GetRemoteSenderId() != MyId.OwnerId) return;

		if (IsOnFloor())
			Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveDash()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (Multiplayer.GetRemoteSenderId() != MyId.OwnerId) return;
		if (_isDashing) return;

		_isDashing         = true;
		_dashDurationTimer = dashDuration;
		Velocity           = Basis.Z * dashSpeed;

		// Broadcast the Dash animation to all clients
		Rpc(MethodName.ClientPlayActionAnim, "Dash");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveFire(Vector3 aimDir)
	{
		if (!GenericCore.Instance.IsServer) return;
		if (Multiplayer.GetRemoteSenderId() != MyId.OwnerId) return;

		int      bulletId = _bulletCounter++;
		Vector3  spawnPos = _muzzle.GlobalPosition;
		Quaternion spawnRot = Transform3D.Identity
			.LookingAt(-aimDir, Vector3.Up)
			.Basis.GetRotationQuaternion();

		EmitSignal(SignalName.BulletSpawnRequested, spawnPos, spawnRot, bulletId, (int)MyId.OwnerId);

		// Broadcast the SnowBall animation for a standard shot
		Rpc(MethodName.ClientPlayActionAnim, "SnowBall");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveUltimate(Vector3 center)
	{
		GD.Print($"ServerReceiveUltimate: IsServer={GenericCore.Instance.IsServer}, center={center}");
		if (!GenericCore.Instance.IsServer) return;
		if (Multiplayer.GetRemoteSenderId() != MyId.OwnerId) return;

		var spaceState = GetWorld3D().DirectSpaceState;
		var shape = new SphereShape3D { Radius = UltimateRadius };

		var query = new PhysicsShapeQueryParameters3D
		{
			Shape         = shape,
			Transform     = new Transform3D(Basis.Identity, center),
			CollisionMask = 1 << (3 - 1) | 1 << (5 - 1),
			Exclude       = new Godot.Collections.Array<Rid> { GetRid() }
		};

		var results = spaceState.IntersectShape(query);
		GD.Print($"ServerReceiveUltimate: found {results.Count} colliders in range");

		foreach (var hit in results)
		{
			var collider = ((Godot.Collections.Dictionary)hit)["collider"].As<GodotObject>();
			if (collider is MeleeEnemy enemy)
			{
				enemy.Die();
				XP += enemy.XP_Value;
			}
			else if (collider is Player hitPlayer && hitPlayer.MyId.OwnerId != MyId.OwnerId)
			{
				hitPlayer.TakeDamage(UltimateDamage);
			}
		}

		Rpc(MethodName.ClientPlayUltimateEffect, center);
	}

	// -------------------------------------------------------
	//  Authority → all clients RPCs
	// -------------------------------------------------------

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
	 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SetPlayerName(string newName)
	{
		PName = newName;
		UpdatePName();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPlayActionAnim(string animName)
	{
		_pendingActionAnim = animName;
	}

	/// <summary>Broadcasts authoritative round-timer value to all clients.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void ClientSyncRoundTimer(float t)
	{
		if (!isLocal) return;
		RoundTimer.Text = t < 0.1f ? "SUDDEN DEATH" : $"{t:F1}";
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPlayUltimateEffect(Vector3 center)
	{
		GD.Print($"Ultimate effect at {center}");
		_pendingActionAnim = "SnowBall";

		// Caster already played the effect locally in ConfirmUltimate; skip to avoid double-play.
		if (!isLocal)
			PlayUltimateRiseFall(center);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientOnDied()
	{
		_isDead = true;
		Visible = false;

		if (isLocal && HUD != null)
			HUD.Visible = false;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientOnRespawned(Vector3 spawnPos)
	{
		_isDead               = false;
		CurrentHp             = MaxHp;
		ResetServerPosition(spawnPos);
		Visible               = true;

		if (isLocal)
		{
			if (HUD != null) HUD.Visible = true;
			UpdateHpBar();
			UpdateXpLabel();
			UpdateUltimateHud();
			UpdateDashHud();
		}
	}

	// -------------------------------------------------------
	//  Damage / Death / Respawn (server-authoritative)
	// -------------------------------------------------------

	public void TakeDamage(float amount)
	{
		if (!GenericCore.Instance.IsServer || _isDead || inBase) return;

		CurrentHp = Mathf.Max(CurrentHp - amount, 0f);
		GD.Print($"{Name} took {amount} damage, HP={CurrentHp}/{MaxHp}");

		if (CurrentHp <= 0f) Die();
	}

	private void Die()
	{
		if (_isDead) return;
		_isDead = true;
		GD.Print($"{Name} died");

		Rpc(MethodName.ClientOnDied);

		if (IsInstanceValid(PlayerBase))
			GlobalTimers.Instance.OneShotTimer(RespawnDelay).Timeout += Respawn;
		else
			GameMaster.Instance.PlayerEliminated();
	}

	private void Respawn()
	{
		if (!GenericCore.Instance.IsServer) return;

		CurrentHp = MaxHp;
		_isDead   = false;

		Vector3 spawnPos = PlayerBase.Spawnpoint.GlobalPosition;
		GD.Print($"{Name} respawning at {spawnPos}");

		Rpc(MethodName.ClientOnRespawned, spawnPos);
	}

	// -------------------------------------------------------
	//  Ultimate helpers (local-only)
	// -------------------------------------------------------

	private void EnterUltimateAiming()
	{
		_isAimingUltimate = true;
		if (UltimateIndicator != null)
		{
			UltimateIndicator.Visible = true;
			UltimateIndicator.Scale   = Vector3.One * UltimateRadius;
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
		CancelUltimate();
		_ultimateTimer = UltimateCooldown;
		PlayUltimateRiseFall(aimPoint); // Immediate local feedback for the caster.
		RpcId(1, MethodName.ServerReceiveUltimate, aimPoint);
	}

	private void PlayUltimateRiseFall(Vector3 groundPos)
	{
		if (UltimateModel == null) return;

		// If the model is childed to the indicator (which gets hidden + scaled
		// during aiming), reparent to the scene root so it animates independently
		// and doesn't inherit the indicator's scale. Do NOT keep global transform —
		// we want the model to use its own authored scale, not the indicator's.
		var desiredParent = GetTree().CurrentScene;
		if (desiredParent != null && UltimateModel.GetParent() != desiredParent)
		{
			var originalScale = UltimateModel.Scale;
			UltimateModel.Reparent(desiredParent, keepGlobalTransform: false);
			UltimateModel.Scale = originalScale;
		}

		Vector3 groundedPos  = groundPos   + Vector3.Up * UltimateGroundOffset;
		Vector3 submergedPos = groundedPos - Vector3.Up * UltimateSubmergeDepth;
		Vector3 peakPos      = groundedPos + Vector3.Up * UltimateRiseHeight;

		UltimateModel.Visible        = true;
		UltimateModel.GlobalPosition = submergedPos;

		// Play the model's animation alongside the tween, if configured.
		if (UltimateAnimPlayer != null && !string.IsNullOrEmpty(UltimateAnimName))
			UltimateAnimPlayer.Play(UltimateAnimName);

		var tween = CreateTween();
		tween.TweenProperty(UltimateModel, "global_position", peakPos, UltimateRiseDuration)
			 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(UltimateModel, "global_position", submergedPos, UltimateFallDuration)
			 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => UltimateModel.Visible = false));
	}

	private void UpdateUltimateIndicator()
	{
		if (UltimateIndicator == null) return;
		UltimateIndicator.GlobalPosition = GetGroundAimPoint() + Vector3.Up * 0.05f;
	}

	// -------------------------------------------------------
	//  Raycasting helpers (local-only)
	// -------------------------------------------------------

	private Vector3 GetCrosshairAimPoint()
	{
		if (playerCam == null)
			return _muzzle.GlobalPosition + _muzzle.GlobalTransform.Basis.Z * 100f;

		var viewport     = GetViewport();
		Vector2 center   = viewport.GetVisibleRect().Size / 2f;
		Vector3 rayOrigin = playerCam.ProjectRayOrigin(center);
		Vector3 rayEnd    = rayOrigin + playerCam.ProjectRayNormal(center) * 500f;

		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd, collisionMask: 0b11);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return result.Count > 0 ? result["position"].AsVector3() : rayEnd;
	}

	private Vector3 GetGroundAimPoint()
	{
		if (playerCam == null) return GlobalPosition;

		var viewport     = GetViewport();
		Vector2 center   = viewport.GetVisibleRect().Size / 2f;
		Vector3 rayOrigin = playerCam.ProjectRayOrigin(center);
		Vector3 rayEnd    = rayOrigin + playerCam.ProjectRayNormal(center) * 500f;

		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd, collisionMask: 1 | 2);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return result.Count > 0 ? result["position"].AsVector3() : GlobalPosition;
	}

	private void UpdateAnimation(bool isMoving)
	{
		if (StateMachine == null) return;

		// Consume any pending action anim here so it's the last Travel() call this frame,
		// preventing _Process locomotion Travel() from overriding the RPC-triggered anim.
		if (_pendingActionAnim != null)
		{
			StateMachine.Travel(_pendingActionAnim);
			_pendingActionAnim = null;
			return;
		}

		// Once Travel() is processed by the AnimationTree, GetCurrentNode() returns the
		// action state — guard against locomotion overriding it on subsequent frames.
		StringName currentState = StateMachine.GetCurrentNode();
		if (currentState == "Dash" || currentState == "SnowBall")
			return;

		if (!IsOnFloor())
			StateMachine.Travel("Jump");
		else if (isMoving)
			StateMachine.Travel("Walk");
		else
			StateMachine.Travel("Idle");
	}

	// -------------------------------------------------------
	//  Misc
	// -------------------------------------------------------

	public void ApplyKnockback(Vector3 force) => _knockbackVelocity = force;
}
