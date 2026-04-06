using Godot;
using System;

public partial class GameMaster : Node
{
	public static GameMaster Instance {get; private set;}
	public static bool GameActive = false;

    /// <summary>
	/// Length of the round in Minutes
	/// </summary>
	[Export] int TotalRoundLength;

	/// <summary>
	/// Time when sudden death starts in Minutes
	/// </summary>
	[Export] int SuddenDeathTime;

	float RoundTime => (TotalRoundLength - SuddenDeathTime) * 60;

	[Signal] public delegate void GameStartTriggerEventHandler();
	[Signal] public delegate void GameEndTriggerEventHandler();
	[Signal] public delegate void SpawnBossTriggerEventHandler();
	[Signal] public delegate void SuddenDeathTriggerEventHandler();



	public override void _Ready() {
		if(!GenericCore.Instance.IsServer) {QueueFree(); return; }
		
		Instance ??= this;
	}
	
	void GameStart()
	{
		//Maybe should wait until all players have loaded in before starting the game
	
		GameActive = true;
		EmitSignal(SignalName.GameStartTrigger);
		GlobalTimers.Instance.OneShotTimer(RoundTime/2).Timeout += SpawnBoss;
	}

	void GameEnd()
	{
		GameActive = false;
		EmitSignal(SignalName.GameEndTrigger);

		//Display end game screen across all clients
		//Interact with GenericCore to shut down the game server
	}

	void SpawnBoss()
	{
		EmitSignal(SignalName.SpawnBossTrigger);
		GlobalTimers.Instance.OneShotTimer(RoundTime/2).Timeout += SuddenDeath;
		//Boss Spawn logic here
	}

	void SuddenDeath()
	{
		EmitSignal(SignalName.SuddenDeathTrigger);

		//Sudden Death Logic Here
		GlobalTimers.Instance.OneShotTimer(SuddenDeathTime * 60).Timeout += GameEnd;
	}

	/*
		Responsibilities:
		- Handles game state
		- Handles game progression
		- Bosses Spawning
		- Sudden Death
	*/
}
