using Godot;
using System;

public partial class GlobalTimers : Node
{
	public static GlobalTimers Instance {get; private set;}
	

	public override void _Ready() {		
		Instance ??= this;
		GameMaster.Instance.GameEndTrigger += StopAll;
	}

	public Timer OneShotTimer(float t)
	{
		var timer = Timer(t);
		timer.Timeout += timer.QueueFree;
		return timer;
	}

	public Timer Timer(float t)
	{
		var timer = new Timer();
		AddChild(timer);
		timer.WaitTime = t;
		timer.OneShot = true;
		timer.Start();
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
