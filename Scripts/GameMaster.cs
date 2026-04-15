using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameMaster : Node
{
	public static GameMaster Instance {get; private set;}
	public static bool GameActive {get; private set;} = false;
	public static bool SuddenDeath {get; private set;} = false;


	/// <summary>
	/// Minimum number of players required to start the game.
	/// </summary>
	[Export] int minPlayerCount = 2;

	/// <summary>
	/// Length of the round in Minutes
	/// </summary>
	[Export] float TotalRoundLength = 8;
	float RoundTime => TotalRoundLength * 60;

	/// <summary>
	/// Time when the boss spawns in Minutes
	/// </summary>
	[Export] float BossSpawn = 3;
	float BossTime => BossSpawn * 60;


	/// <summary>
	/// Duration of the end screen in seconds
	/// </summary>
	[Export] float EndScreenDuration = 10f;

	public List<NetworkPlayerManager> Players = new();
	public Timer RoundTimer;
	float Eliminations = 0;

	[Signal] public delegate void GameStartTriggerEventHandler();
	[Signal] public delegate void GameEndTriggerEventHandler();
	[Signal] public delegate void SpawnBossTriggerEventHandler();
	[Signal] public delegate void SuddenDeathTriggerEventHandler();

	public async override void _Ready() 
	{
		while(!GenericCore.Instance.IsGenericCoreConnected) 
			await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);

		GenericCore.Instance.ClientDisconnected += PlayerDC;
		
		if(Instance != this && Instance != null) { QueueFree(); return; }
		Instance = this;
	}

	void PlayerDC(long id)
	{
		var npm = Players.First(p => p.MyNetID.OwnerId == id);
		Players.Remove(npm);
		GD.PushError($"Player {id} disconnected. Remaining players: {Players.Count}");

		if(GameActive)
		{
			PlayerEliminated();
		}
		else
		{
			PlayerReady();
		}
	}
	
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public async void GameStart()
	{
		EmitSignal(SignalName.GameStartTrigger);
		GameActive = true;

		if(!GenericCore.Instance.IsServer) return;

		var level = GenericCore.Instance.MainNetworkCore.NetCreateObject(0, Vector3.Zero, Quaternion.Identity);
		GD.PushWarning($"Vars: {TotalRoundLength}, {BossSpawn}, \nTimes: {RoundTime}, {BossTime}");

		RoundTimer = GlobalTimers.Instance.OneShotTimer(RoundTime);
		RoundTimer.Timeout += TriggerSuddenDeath;
		GlobalTimers.Instance.OneShotTimer(BossTime).Timeout += SpawnBoss;

		while(!level.IsInsideTree()) await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);

		var Bases = GetTree().GetNodesInGroup("PlayerBase");
		for(int i = 0; i < Bases.Count; i++)
		{
			if(i < Players.Count) Players[i].SpawnPlayer((Base)Bases.First(p => p.GetParent().Name == $"Igloo{i+1}"));
			else {  RemoveBase(Bases.First(p => p.GetParent().Name == $"Igloo{i+1}") as Base); }
		}
		

		GD.PushWarning("Game Started");
	}

	void RemoveBase(Base b)
	{
		GenericCore.Instance.MainNetworkCore.NetDestroyObject(b.MyID);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ReturnToLobby()
	{
		GenericCore.Instance.DisconnectFromGame();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	async void GameEnd()
	{
		GameActive = false;
		EmitSignal(SignalName.GameEndTrigger);

		if(!GenericCore.Instance.IsServer) return;

		//Display end game screen across all clients

		GD.PushWarning("Game Ended");

		await ToSignal(GlobalTimers.Instance.OneShotTimer(EndScreenDuration), Timer.SignalName.Timeout);

		Rpc("ReturnToLobby");

		//I don't think we ever "disconnect" from the lobby system
		LobbyStreamlined.Instance.DisconnectFromLobbySystem();
		GetTree().Quit();
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
		SuddenDeath = true;

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
	public void AddPlayer(NetworkPlayerManager npm)
	{
		Players.Add(npm);
		PlayerReady(); // re-evaluate in case this was the last needed registration
	}

	public void PlayerReady()
	{
		GD.PushWarning($"PlayerReady called. Players registered: {Players.Count}");
		if (Players.Count < minPlayerCount) return; // not enough registered yet
		if (Players.All(p => p.IsReady)) Rpc("GameStart");
	}

	public void PlayerEliminated()
	{
		if(!GenericCore.Instance.IsServer) return;
		if(++Eliminations >= Players.Count-1) Rpc(MethodName.GameEnd);
	}
}
