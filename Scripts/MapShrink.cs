using Godot;
using System;

public partial class MapShrink : Node
{
	[Export] float ShrinkTime = 20;
	[Export] CylinderMesh Floor;
	[Export] CylinderShape3D FloorCollider;
	[Export] float minSize;
	[Export] float curSize
	{	
		get { return _curSize; }
		set { 			
				Floor.TopRadius = Floor.BottomRadius = value;
				FloorCollider.Radius = value;
				_curSize = value;
			}
	}

	[Export] Area3D BodyDrop;

	float _curSize;
	float maxSize;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GameMaster.Instance.SuddenDeathTrigger += Shrink;
		curSize = maxSize = FloorCollider.Radius;

		BodyDrop.BodyExited += DeleteObj;
	}

	async void Shrink()
	{
		var curTime = 0f;
		var MaxTime = ShrinkTime;

		while(curTime < MaxTime)
		{
			curSize = Mathf.Lerp(maxSize, minSize, curTime/MaxTime);
			GD.Print("Shrinking: " + curSize);

			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			curTime += (float)GetProcessDeltaTime();
		}

	}
	
	void DeleteObj(Node obj)
	{
		GD.Print($"{obj.GetParent().Name} is to be deleted");
		obj.GetParent().QueueFree();
	}
}
