using Godot;
using System;

public partial class GlobalTimers : Node
{
	public static GlobalTimers Instance {get; private set;}
	

	public async override void _Ready() {		
		while(!GenericCore.Instance.IsGenericCoreConnected) await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);
		if(Instance != this && Instance != null) { QueueFree(); return; }

		Instance ??= this;		
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
