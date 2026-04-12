using Godot;
using System;
using System.Diagnostics;

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
	public Player Character {get; private set;}

	public override void _Ready()
	{
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
			_nameEdit.TextChanged    += (name) => Rpc("OnNameChanged", name);
			_readyButton.Pressed       +=  () => Rpc("OnReadyPressed");
		}

		if(GenericCore.Instance.IsServer)
		{
			GameMaster.Instance.AddPlayer(this);
			GameMaster.Instance.GameStartTrigger += SpawnPlayer;
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

	void SpawnPlayer()
	{
		GD.PushWarning($"Spawning player for {PlayerName} with NetID {MyNetID.OwnerId}");
		Character = GenericCore.Instance.MainNetworkCore.NetCreateObject(1, Vector3.Zero, Quaternion.Identity, MyNetID.OwnerId) as Player;
	}

	public override void _Process(double delta) {
		if(!GameMaster.GameActive)
		{
			if (_nameEdit == null) return;

			_readyButton.Text = IsReady ? "✓ Ready" : "Ready";
			_readyButton.Modulate = IsReady ? new Color(0.4f, 1f, 0.4f) : Colors.White;			
		}
	}
}
