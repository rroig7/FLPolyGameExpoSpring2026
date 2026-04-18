using Godot;

public static class SoundFx
{
	private const string Root = "res://Assets/Sounds/";

	public const string BaseDestroyed    = Root + "BaseDestroyed.mp3";
	public const string BossDeath        = Root + "BossDeath.wav";
	public const string BossMeleeAttack  = Root + "BossMeleeAttack.mp3";
	public const string BossWalk         = Root + "BossWalk.mp3";
	public const string MeleeEnemyAttack = Root + "MeleeEnemyAttack.wav";
	public const string MeleeEnemyWalk   = Root + "MeleeEnemyWalk.wav";
	public const string PlayerDamaged    = Root + "PlayerDamaged.wav";
	public const string PlayerDeath      = Root + "PlayerDeath.wav";
	public const string PlayerSlide      = Root + "PlayerSlide.wav";
	public const string PlayerUltimate   = Root + "PlayerUltimate.wav";
	public const string PlayerUpgrade    = Root + "PlayerUpgrade.wav";
	public const string PlayerWalk       = Root + "PlayerWalk.wav";
	public const string ThrowSnowball    = Root + "ThrowSnowball.mp3";
	public const string InGameMusic      = "res://Assets/Music/InGameMusic.mp3";
	public const string BlizzardSound    = "res://Assets/Music/Blizzard Sound.mp3";

	public static AudioStreamPlayer3D PlayOn(Node3D source, string path, float volumeDb = 0f)
	{
		if (source == null || !GodotObject.IsInstanceValid(source)) return null;
		var stream = GD.Load<AudioStream>(path);
		if (stream == null) return null;

		var p = new AudioStreamPlayer3D { Stream = stream, Bus = "Master", VolumeDb = volumeDb };
		source.AddChild(p);
		p.Finished += () => p.QueueFree();
		p.Play();
		return p;
	}

	public static void PlayAt(Node sceneRoot, Vector3 globalPos, string path, float volumeDb = 0f)
	{
		if (sceneRoot == null || !GodotObject.IsInstanceValid(sceneRoot)) return;
		var stream = GD.Load<AudioStream>(path);
		if (stream == null) return;

		var p = new AudioStreamPlayer3D { Stream = stream, Bus = "Master", VolumeDb = volumeDb };
		sceneRoot.AddChild(p);
		p.GlobalPosition = globalPos;
		p.Finished += () => p.QueueFree();
		p.Play();
	}

	public static void PlayLocal(Node parent, string path, float volumeDb = 0f)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent)) return;
		var stream = GD.Load<AudioStream>(path);
		if (stream == null) return;

		var p = new AudioStreamPlayer { Stream = stream, Bus = "Master", VolumeDb = volumeDb };
		parent.AddChild(p);
		p.Finished += () => p.QueueFree();
		p.Play();
	}

	public static AudioStreamPlayer3D MakeLooped(Node3D source, string path, float volumeDb = 0f)
	{
		var stream = GD.Load<AudioStream>(path);
		if (stream == null || source == null) return null;

		var p = new AudioStreamPlayer3D { Stream = stream, Bus = "Master", VolumeDb = volumeDb };
		source.AddChild(p);

		p.Finished += () =>
		{
			if (!GodotObject.IsInstanceValid(p)) return;
			if (p.HasMeta("loop_on") && (bool)p.GetMeta("loop_on"))
				p.Play();
		};
		return p;
	}

	public static void SetLoopActive(AudioStreamPlayer3D p, bool active)
	{
		if (p == null || !GodotObject.IsInstanceValid(p)) return;
		p.SetMeta("loop_on", active);
		if (active && !p.Playing) p.Play();
		else if (!active && p.Playing) p.Stop();
	}
}
