using Godot;
using System;

[GlobalClass]
public partial class Player : BaseNetworkedPlayer
{
	CameraFollow playerCam;
	public bool isLocal => MyId.IsLocal;
	public enum PlayerAbilities { ABILITY1, ABILITY2, ABILITY3 }

	// --- Dash settings ---
	[Export] float dashSpeed    = 20f;
	[Export] float dashDuration = 0.15f;
	[Export] float dashCooldown = 1.0f;

	float _dashCooldownTimer = 0f;
	float _dashDurationTimer = 0f;
	bool  _isDashing         = false;
	
	// --- Bullet Settings ---
	[Export] public PackedScene SnowBulletScene;
	[Export] public float FireRate = 0.2f;
	[Export] public float shootTimer = 0f;
	[Export] public Node3D _muzzle;

	private bool _canShoot = true;


	public override void _Ready()
	{
		base._Ready();
		MyId.NetIDReady += SlowStart;
	}

	public void SlowStart()
	{
		if (!MyId.IsLocal) return;
		TryAssignCamera();
		Input.MouseMode = Input.MouseModeEnum.Captured;
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

	public override void LocalProcess(float delta)
	{
		if (_dashCooldownTimer > 0f)
			_dashCooldownTimer -= delta;

		if (!_canShoot && shootTimer > 0f)
			shootTimer -= delta;

		if (playerCam == null)
		{
			TryAssignCamera();
			return;
		}

		// Derive character yaw from the camera's swivel angle so the
		// character faces the same direction the camera is looking
		float camYaw = playerCam.GetFacingYaw();
		Rpc(MethodName.ProcessMouseLook, camYaw);

		var input = Input.GetVector("Left", "Right", "Forward", "Back");
		Rpc(MethodName.ProcessInput, new Vector3(input.X, 0, input.Y));
		
		if (Input.IsActionJustPressed("shoot"))
		{
			GD.Print("Blicky activated");
			Rpc(MethodName.ProcessBlicky);
			_canShoot = false;
		}

		if (Input.IsActionJustPressed("Dash") && _dashCooldownTimer <= 0f && input != Vector2.Zero)
		{
			_dashCooldownTimer = dashCooldown;
			Rpc(MethodName.ProcessDash);
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
		 TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	void ProcessInput(Vector3 playerInput)
	{
		if (!GenericCore.Instance.IsServer) return;
		if (_isDashing) return;

		if (playerInput.LengthSquared() > 0)
		{
			Velocity = (-Basis.Z * playerInput.Z + -Basis.X * playerInput.X).Normalized() * baseSpeed;
		}
		else
			Velocity = Vector3.Zero;
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
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ProcessBlicky()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (!_canShoot) return;

		var bullet = SnowBulletScene.Instantiate<SnowBullet>();
		bullet.GlobalTransform = _muzzle.GlobalTransform;
		GetTree().CurrentScene.AddChild(bullet);

		shootTimer = FireRate;
		_canShoot = true;
	}
}
