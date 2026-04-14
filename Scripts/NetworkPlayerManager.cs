using Godot;
using System.Linq;

public partial class NetworkPlayerManager : Control
{
	[Export] public string PlayerName = "Player";
	[Export] public bool IsReady = false;

	[Export] public NetID MyNetID {get; private set;}

	[Export] public LineEdit _nameEdit;
	[Export] public Button _readyButton;

	private bool _isLocalPlayer = false;
	private bool _isInitialized = false;
	private bool _uiConnected = false;
	public Player PlayerCharacter {get; private set;}
	public Node3D PlayerBase {get; private set;}

	public override void _Ready()
	{
		AddToGroup("NetworkPlayerManagers");

		if (MyNetID == null)
			MyNetID = GetChildren().OfType<NetID>().FirstOrDefault();

		if (MyNetID != null)
			MyNetID.NetIDReady += OnNetIDReady;
	}

	private void OnNetIDReady()
	{
		_isLocalPlayer = MyNetID.IsLocal;
		_isInitialized = true;

		bool canEdit = _isLocalPlayer;
		_nameEdit.Editable = canEdit;
		_readyButton.Disabled = !canEdit;

		if (canEdit && !_uiConnected)
		{
			_uiConnected = true;
			_nameEdit.TextChanged += (name) => Rpc("OnNameChanged", name);
			_readyButton.Pressed  += () => Rpc("OnReadyPressed");
		}

		if(GenericCore.Instance.IsServer && GameMaster.Instance != null)
		{
			if(!GameMaster.Instance.Players.Contains(this))
			{
				GameMaster.Instance.AddPlayer(this);
			}
		}

		_readyButton.Modulate = Colors.White;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void OnNameChanged(string newText)
	{
		PlayerName = newText;
		if(!_isLocalPlayer) _nameEdit.PlaceholderText = PlayerName;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void OnReadyPressed()
	{
		_nameEdit.Editable = !IsReady;

		if(GenericCore.Instance.IsServer)
		{
			IsReady = !IsReady;
			if(IsReady) GameMaster.Instance.PlayerReady();
		}
	}

	// Change from implicit private to public
	public void SpawnPlayer(int pNumber, Node3D Base)
	{
		GD.PushWarning($"Spawning player for {PlayerName} with NetID {MyNetID.OwnerId} and Player Number {pNumber}");
		PlayerBase = Base;

		var playerSpawnpoints = GetTree().GetNodesInGroup("SpawnPoints");

		GD.PushWarning($"Query: P{pNumber} Basespawn; P{pNumber} SpawnPoint");

		var playerSpawn = playerSpawnpoints.First(p => p.Name == $"P{pNumber} SpawnPoint") as Node3D;

		PlayerCharacter = GenericCore.Instance.MainNetworkCore
			.NetCreateObject(1, playerSpawn.GlobalPosition, Quaternion.Identity, MyNetID.OwnerId) as Player;
	}

	public override void _Process(double delta) {
		if(!GameMaster.GameActive)
		{
			if (_nameEdit == null) return;

			_readyButton.Text = IsReady ? "✓ Ready" : "Ready";
			_readyButton.Modulate = IsReady ? new Color(0.4f, 1f, 0.4f) : Colors.White;			
		}
	}

	private void OnDeferredRegister()
	{
		if (GameMaster.Instance == null) return;
		GetTree().ProcessFrame -= OnDeferredRegister;
		GameMaster.Instance.AddPlayer(this);
	}
}
