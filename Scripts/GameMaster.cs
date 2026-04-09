using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

public partial class GameMaster : Node
{
	public static GameMaster Instance {get; private set;}
	public static bool GameActive = false;

	/// <summary>
	/// Minimum number of players required to start the game.
	/// </summary>
	[Export] int minPlayerCount = 2;

    /// <summary>
	/// Length of the round in Minutes
	/// </summary>
	[Export] float TotalRoundLength;
	float RoundTime => TotalRoundLength * 60;

	/// <summary>
	/// Time when the boss spawns in Minutes
	/// </summary>
	[Export] float BossSpawn;
	float BossTime => BossSpawn * 60;

	/// <summary>
	/// Time when sudden death starts in Minutes
	/// </summary>
	[Export] float SuddenDeath;
	float SuddenDeathTime => SuddenDeath * 60;

	List<NetworkPlayerManager> Players = new();

	[Signal] public delegate void GameStartTriggerEventHandler();
	[Signal] public delegate void GameEndTriggerEventHandler();
	[Signal] public delegate void SpawnBossTriggerEventHandler();
	[Signal] public delegate void SuddenDeathTriggerEventHandler();



	public async override void _Ready() {
		
		while(!GenericCore.Instance.IsGenericCoreConnected) await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);
		if(Instance != this && Instance != null) {QueueFree(); return; }

		Instance = this;
	}
	
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void GameStart()
	{
		EmitSignal(SignalName.GameStartTrigger);

		if(!GenericCore.Instance.IsServer) return;

		GenericCore.Instance.MainNetworkCore.NetCreateObject(0, Vector3.Zero, Quaternion.Identity);

		GlobalTimers.Instance.OneShotTimer(RoundTime).Timeout += GameEnd;
		GlobalTimers.Instance.OneShotTimer(BossTime).Timeout += SpawnBoss;
		GlobalTimers.Instance.OneShotTimer(SuddenDeathTime).Timeout += TriggerSuddenDeath;

		GD.PushWarning("Game Started");
		GameActive = true;
	}

	void GameEnd()
	{
		GameActive = false;
		EmitSignal(SignalName.GameEndTrigger);

		//Display end game screen across all clients
		//Interact with GenericCore to shut down the game server
		GD.PushWarning("Game Ended");
	}

	void SpawnBoss()
	{
		EmitSignal(SignalName.SpawnBossTrigger);
		//Boss Spawn logic here

		GD.PushWarning("SpawnBoss");
	}

	void TriggerSuddenDeath()
	{
		EmitSignal(SignalName.SuddenDeathTrigger);

		//Sudden Death Logic Here
		GD.PushWarning("Sudden Death!");
	}

	/*
		Responsibilities:
		- Handles game state
		- Handles game progression
		- Bosses Spawning
		- Sudden Death
		- Starting/Ending the game
	*/

	//What should we do when a player DC's?
	public void AddPlayer(NetworkPlayerManager npm) => Players.Add(npm);

	public void PlayerReady()
	{
		GD.PushWarning($"{Players.Count}");
		if(Players.All(p => p.IsReady) && Players.Count >= minPlayerCount) Rpc("GameStart");
	}
}
