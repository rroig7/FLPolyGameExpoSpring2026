using Godot;
using System;

public partial class GlobalTimers : Node
{
	public static GlobalTimers Instance {get; private set;}
	

	public override void _Ready() {
		// Claim the singleton synchronously before any await — two instances
		// awaiting in parallel would otherwise both pass a post-await null check.
		if (Instance != null && Instance != this) { QueueFree(); return; }
		Instance = this;
	}

	public Timer OneShotTimer(float t)
	{
		var timer = RepeatTimer(t);
		timer.Timeout += timer.QueueFree;
		timer.Start();
		return timer;
	}

	public Timer RepeatTimer(float t)
	{
		var timer = new Timer();
		AddChild(timer);
		timer.WaitTime = t;
		timer.OneShot = true;
		return timer;
	}

	public void StopAll()
	{
		foreach(var child in GetChildren())
		{
			child.QueueFree();
		}
	}


	/*
		Responsibilities:
		- Handles all global timers for the game
		- Provides an interface for other nodes to access timers
	*/
}
