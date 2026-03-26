using Godot;
using System;

[GlobalClass]
public partial class Player : BaseNetworkedPlayer
{
	Camera3D playerCam;
	public bool isLocal => MyId.IsLocal;
	public enum PlayerAbilities {ABILITY1, ABILITY2, ABILITY3}


    public void SlowStart()
    {
		if(!MyId.IsLocal)
		{
		}
		else
		{
			var PlayerCam = GetTree().GetFirstNodeInGroup("PLAYERCAMERA") as CameraFollow;
			PlayerCam.SetTarget(this);
			playerCam = PlayerCam;
		}
    }

	public void SetCamera(Camera3D cam)
	{
		playerCam = cam;
	}


	public override void LocalProcess(float delta)
	{
		var input = Input.GetVector("Left", "Right", "Forward", "Back");

		if(playerCam != null)
		{
			//GD.PushWarning("Input Reccieved locally, pushing to server");
			Rpc(MethodName.ProcessInput, new Vector3(input.X, 0, input.Y), -playerCam.Basis.Z);
		}
	}

	//------ Remote Calls ------\\

	/// <summary>
	/// Server processes user input and applies velocity to player
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	void ProcessInput(Vector3 playerInput, Vector3 playerCam)
	{
		if(GenericCore.Instance.IsServer)
		{
			if(playerInput.LengthSquared() > 0)
			{
				//GD.Print(Vector3.Forward.Dot(playerInput));

				var angDiff = Vector3.Forward.SignedAngleTo(playerInput, Vector3.Up);
				GD.Print(Mathf.RadToDeg(angDiff));
				
				//This works as you think it does..possibly
				var correctedDir = playerCam.Rotated(Vector3.Up, angDiff);
				GD.Print($"PlayerInput Dir: {playerInput}, CorrectedDir: {correctedDir}");

				
				LinearVelocity = correctedDir * baseSpeed;				
			}
			else
				LinearVelocity = Vector3.Zero;
			
		}
	}
}
