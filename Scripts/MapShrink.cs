using Godot;
using System;

public partial class MapShrink : Node
{

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
	float _curSize;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GameMaster.Instance.SuddenDeathTrigger += Shrink;
		curSize = FloorCollider.Radius;
	}

	async void Shrink()
	{
		var curTime = 0f;
		var MaxTime = GameMaster.Instance.SuddenDeathTime;

		while(curTime < MaxTime)
		{
			curSize = Mathf.Lerp(curSize, minSize, curTime/MaxTime);
			GD.Print("Shrinking: " + curSize);

			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			curTime += (float)GetProcessDeltaTime();
		}

	}

}
