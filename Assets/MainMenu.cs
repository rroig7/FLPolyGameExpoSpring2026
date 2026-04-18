using Godot;
using System;

public partial class MainMenu : Node
{
	[Export] public Control TutorialPage;
	[Export] public float RotationSpeed = 10f;

	private Camera3D camera;

	public override void _Ready()
	{
		// Get camera
		camera = GetNode<Camera3D>("Node3D/Camera3D");

		// Get button
		Button playButton = GetNode<Button>("Control/PlayButton");
		playButton.Pressed += OnPlayButtonPressed;
	}

	public override void _Process(double delta)
	{
		if (camera != null)
		{
			float rotationAmount = Mathf.DegToRad(RotationSpeed * (float)delta);

			// Recommended: rotate the parent Node3D instead
			GetNode<Node3D>("Node3D").RotateY(rotationAmount);
		}
	}

	private void OnPlayButtonPressed()
	{
		GetTree().ChangeSceneToFile("res://NetworkCore/WanLobbySystem/generic_lobby_system.tscn");
	}
	
	private void OnTutorialButtonPressed()
	{
		TutorialPage.Visible = true;
	}
}
