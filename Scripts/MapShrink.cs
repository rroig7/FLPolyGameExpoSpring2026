using Godot;
using System;

public partial class MapShrink : Node
{
	[Export] float ShrinkTime = 20;
	[Export] CylinderMesh Floor;
	[Export] CylinderShape3D FloorCollider;
	[Export] CylinderShape3D DissapearCollider;
	[Export] float minSize;
	[Export] float curSize
	{
		get { return _curSize; }
		set {
				if (Floor != null) Floor.TopRadius = Floor.BottomRadius = value;
				if (FloorCollider != null) FloorCollider.Radius = value;
				if (DissapearCollider != null) DissapearCollider.Radius = value;
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
		_curSize = maxSize = FloorCollider.Radius;

		BodyDrop.BodyExited += DeleteObj;
	}

	async void Shrink()
	{
		var curTime = 0f;
		var MaxTime = ShrinkTime;

		while(curTime < MaxTime && GameMaster.GameActive)
		{
			curSize = Mathf.Lerp(maxSize, minSize, curTime/MaxTime);
			GD.Print("Shrinking: " + curSize);

			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			curTime += (float)GetProcessDeltaTime();
		}

	}
	
	void DeleteObj(Node obj)
	{
		// Godot re-emits BodyExited when a body inside the area is freed.
		// Skip those — we only want to react to bodies that legitimately left.
		if (!IsInstanceValid(obj) || obj.IsQueuedForDeletion()) return;

		var parent = obj.GetParent() as Node3D;
		if (parent == null) return;

		var tween = GetTree().CreateTween()
			.SetEase(Tween.EaseType.In)
			.SetTrans(Tween.TransitionType.Back)
			.TweenProperty(parent, "position:y", parent.GlobalPosition.Y - parent.Scale.Y, 5);

		// Re-check validity when the tween finishes — parent may have been freed
		// by another system during the 5s animation.
		tween.Finished += () =>
		{
			if (IsInstanceValid(parent) && !parent.IsQueuedForDeletion())
				parent.QueueFree();
		};
	}
}
