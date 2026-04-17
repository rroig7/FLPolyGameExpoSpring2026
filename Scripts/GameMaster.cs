using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameMaster : Node
{
	public static GameMaster Instance { get; private set; }
	public static bool GameActive { get; private set; } = false;
	public static bool SuddenDeath { get; private set; } = false;

	[Export] NetworkCore MainSpawner;

	[Export] int minPlayerCount = 2;
	[Export] float TotalRoundLength = 8;
	float RoundTime => TotalRoundLength * 60;

	[Export] float BossSpawn = 3;
	float BossTime => BossSpawn * 60;

	[Export] float EndScreenDuration = 10f;
	[Export] Control EndScreen;

	public List<NetworkPlayerManager> Players = new();
	public Timer RoundTimer;
	float Eliminations = 0;

	[Signal] public delegate void GameStartTriggerEventHandler();
	[Signal] public delegate void GameEndTriggerEventHandler();
	[Signal] public delegate void SpawnBossTriggerEventHandler();
	[Signal] public delegate void SuddenDeathTriggerEventHandler();

	public async override void _Ready()
	{
		while (!GenericCore.Instance.IsGenericCoreConnected)
			await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);

		GenericCore.Instance.ClientDisconnected += PlayerDC;

		if (Instance != this && Instance != null) { QueueFree(); return; }
		Instance = this;
	}

	void PlayerDC(long id)
	{
		var npm = Players.First(p => p.MyNetID.OwnerId == id);
		Players.Remove(npm);
		GD.PushError($"Player {id} disconnected. Remaining players: {Players.Count}");

		if (GameActive) PlayerEliminated();
		else PlayerReady();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public async void GameStart()
	{
		EmitSignal(SignalName.GameStartTrigger);
		GameActive = true;

		if (!GenericCore.Instance.IsServer) return;

		var level = GenericCore.Instance.MainNetworkCore.NetCreateObject(0, Vector3.Zero, Quaternion.Identity);
		
		RoundTimer = GlobalTimers.Instance.OneShotTimer(RoundTime);
		RoundTimer.Timeout += TriggerSuddenDeath;
		GlobalTimers.Instance.OneShotTimer(BossTime).Timeout += SpawnBoss;

		while (!level.IsInsideTree()) await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);

		var Bases = GetTree().GetNodesInGroup("PlayerBase");
		for (int i = 0; i < Bases.Count; i++)
		{
			if (i < Players.Count) 
				Players[i].SpawnPlayer((Base)Bases.First(p => p.GetParent().Name == $"Igloo{i + 1}"));
			else 
				RemoveBase(Bases.First(p => p.GetParent().Name == $"Igloo{i + 1}") as Base);
		}

		// --- NEW: Wait for nodes to initialize and then connect signals ---
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		ConnectObjectSignals();
		
		GD.PushWarning("Game Started and Signals Connected");
	}

	/// <summary>
	/// Scans the scene for players and enemies to connect their projectile signals to the GameMaster.
	/// </summary>
	private void ConnectObjectSignals()
	{
		// Connect Player Snowball signals
		foreach (Node node in GetTree().GetNodesInGroup("players"))
		{
			if (node is Player player)
			{
				// Ensure we don't double-connect if this is called multiple times
				if (!player.IsConnected("BulletSpawnRequested", Callable.From<Vector3, Quaternion, int, int>(OnBulletSpawnRequested)))
					player.BulletSpawnRequested += OnBulletSpawnRequested;
			}
		}
	}

	// ── Projectile Handlers (Migrated from Level.cs) ──────────────────

	public async void OnBulletSpawnRequested(Vector3 origin, Quaternion rotation, int bulletId, int shooterId)
	{
		var node = MainSpawner.NetCreateObject(
			index: 3, // SnowBullet
			initialPosition: origin,
			rotation: rotation,
			owner: 1
		);

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (node is SnowBullet bullet && IsInstanceValid(bullet))
		{
			bool isBossBullet = shooterId == -1;
			bullet.Direction = new Basis(rotation).Z;
			bullet.Speed = isBossBullet ? 80f : 30f;
			bullet.ShooterId = shooterId;
			bullet.GlobalPosition = origin;
			if (isBossBullet)
				bullet.Scale = Vector3.One * 4f;
		}
	}

	// ── Existing Boilerplate ─────────────────────────────────────────

	void RemoveBase(Base b) => GenericCore.Instance.MainNetworkCore.NetDestroyObject(b.MyID);

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ReturnToLobby() => GenericCore.Instance.DisconnectFromGame();

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	async void GameEnd()
	{
		GameActive = false;
		EmitSignal(SignalName.GameEndTrigger);

		EndScreen.Show();

		if (!GenericCore.Instance.IsServer) return;

		await ToSignal(GlobalTimers.Instance.OneShotTimer(EndScreenDuration), Timer.SignalName.Timeout);
		Rpc("ReturnToLobby");
		LobbyStreamlined.Instance.DisconnectFromLobbySystem();
		GetTree().Quit();
	}

	public void SpawnBoss()
	{
		var node = MainSpawner.NetCreateObject(
			index: 4, // Boss
			Vector3.Zero,
			Quaternion.Identity,
			owner: 1
		);
		
		foreach (Node bossNode in GetTree().GetNodesInGroup("boss"))
		{
			if (bossNode is BossEnemy boss)
			{
				// Ensure we don't double-connect if this is called multiple times
				if (!boss.IsConnected("BulletSpawnRequested", Callable.From<Vector3, Quaternion, int, int>(OnBulletSpawnRequested)))
					boss.BulletSpawnRequested += OnBulletSpawnRequested;
			}
		}
	}
	
	public void RegisterTurret(Turret turret)
	{
		if (!turret.IsConnected("BulletSpawnRequested", Callable.From<Vector3, Quaternion, int, int>(OnBulletSpawnRequested)))
			turret.BulletSpawnRequested += OnBulletSpawnRequested;
	}

	void TriggerSuddenDeath() { EmitSignal(SignalName.SuddenDeathTrigger); SuddenDeath = true; }

	public void AddPlayer(NetworkPlayerManager npm)
	{
		Players.Add(npm);
		PlayerReady();
	}

	public void PlayerReady()
	{
		if (Players.Count < minPlayerCount) return;
		if (Players.All(p => p.IsReady)) Rpc("GameStart");
	}

	public void PlayerEliminated()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (++Eliminations >= Players.Count - 1) Rpc(MethodName.GameEnd);
	}
}
