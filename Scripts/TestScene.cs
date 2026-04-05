using Godot;

public partial class TestScene : Node3D
{
	[Export] public NetworkCore PlayerSpawner;
	[Export] public Node3D SpawnPoint;
	[Export] public CanvasLayer MyCanvas;

	public override void _Ready()
	{
		slowStart();
	}

	private async void slowStart()
	{
		while (GenericCore.Instance == null)
		{
			await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
		}

		Multiplayer.PeerConnected += (long id) => OnPeerConnected(id);

		GD.Print("Level: Ready. Awaiting connections.");
	}

	private void OnPeerConnected(long peerId)
	{
		GD.Print($"Level: PeerConnected fired for peer {peerId}, IsServer={GenericCore.Instance.IsServer}");
		MyCanvas.Visible = false;

		if (!GenericCore.Instance.IsServer) return;

		SpawnPlayerFor(peerId);
	}

	private async void SpawnPlayerFor(long peerId)
	{
		var player = PlayerSpawner.NetCreateObject(
			index: 0,
			initialPosition: SpawnPoint.GlobalPosition,
			rotation: Quaternion.Identity,
			owner: peerId
		);

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (player is Player player3D && IsInstanceValid(player3D))
		{
			player3D.GlobalPosition = SpawnPoint.GlobalPosition;
		}
	}
}
