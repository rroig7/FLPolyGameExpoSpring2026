using Godot;
using System;

public partial class NetworkPlayerManager : Control
{
	[Export] public string PlayerName = "Player";
	[Export] public bool IsReady = false;

	public NetID MyNetID {get; private set;}

	[Export] public LineEdit _nameEdit;
	[Export] public Button _readyButton;

	[Signal] public delegate void PlayerReadyEventHandler(long ownerId);

	private bool _isLocalPlayer = false;
	private bool _isInitialized = false;
	private bool _uiConnected = false;

	public override void _Ready()
	{
		base._Ready();

		AddToGroup("NPM");

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
			_nameEdit.TextSubmitted    += OnNameSubmitted;
			_readyButton.Pressed       += OnReadyPressed;
		}

		RefreshUI();
	}

	public void RefreshUI()
	{
		if (_nameEdit == null) return;

		_nameEdit.Text = PlayerName;
		_readyButton.Text = IsReady ? "✓ Ready" : "Ready";
		_readyButton.Modulate = IsReady ? new Color(0.4f, 1f, 0.4f) : Colors.White;
	}

	private void OnNameSubmitted(string newText)
	{
		PlayerName = newText;
	}

	private void OnReadyPressed()
	{
		IsReady = !IsReady;

		_nameEdit.Editable = !IsReady;

		RefreshUI();

		if (IsReady)
			EmitSignalPlayerReady(MyNetID.OwnerId);
	}

	public void ResetReady()
	{
		IsReady = false;
		if (_nameEdit != null)
		{
			_nameEdit.Editable = _isLocalPlayer;
		}

		RefreshUI();
	}
}
