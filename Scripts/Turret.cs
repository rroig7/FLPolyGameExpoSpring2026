using Godot;
using System.Collections.Generic;

public partial class Turret : Node3D
{
    [Export] public float RotationSpeed = 5.0f;
    [Export] public float FireRate = 1.0f;       // Shots per second
    [Export] public float BulletSpeed = 20.0f;
    [Export] public PackedScene BulletScene;      // Assign in Inspector

    // Node references — assign in Inspector or rely on GetNode paths below
    [Export] public Node3D TurretHead;
    [Export] public Node3D Muzzle;
    [Export] public Area3D TurretFOV;

    private readonly List<Node3D> _targetsInFOV = new();
    private Node3D _currentTarget;
    private float _fireCooldown = 0f;
    private bool _isAcquiring = false; // true while rotating toward a new target

    public override void _Ready()
    {
        // Fallback to path-based lookup if not set via Inspector
        TurretHead ??= GetNode<Node3D>("TurretHead");
        Muzzle     ??= GetNode<Node3D>("TurretHead/Muzzle");
        TurretFOV  ??= GetNode<Area3D>("TurretFOV");

        TurretFOV.BodyEntered += OnBodyEntered;
        TurretFOV.BodyExited  += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        _fireCooldown -= (float)delta;

        Node3D newTarget = GetClosestTarget();

        // Reset acquiring state whenever the target changes
        if (newTarget != _currentTarget)
        {
            _currentTarget = newTarget;
            _isAcquiring = true;   // Force the turret to fully rotate before firing
        }

        if (_currentTarget is null)
            return;

        AimAt(_currentTarget, (float)delta);

        // Only clear acquiring once we're truly aimed
        if (_isAcquiring && IsAimedAt(_currentTarget))
            _isAcquiring = false;

        if (!_isAcquiring && _fireCooldown <= 0f && IsAimedAt(_currentTarget))
        {
            Fire();
            _fireCooldown = 1f / FireRate;
        }
    }

    // ── Targeting ─────────────────────────────────────────────────────────────

    private Node3D GetClosestTarget()
    {
        Node3D closest = null;
        float  minDist = float.MaxValue;

        foreach (Node3D body in _targetsInFOV)
        {
            if (!IsInstanceValid(body)) continue;
            float d = GlobalPosition.DistanceSquaredTo(body.GlobalPosition);
            if (d < minDist)
            {
                minDist = d;
                closest = body;
            }
        }
        return closest;
    }

    // ── Aiming ────────────────────────────────────────────────────────────────

    private void AimAt(Node3D target, float delta)
    {
        // Flatten to XZ plane so the head only rotates on Y
        Vector3 toTarget = target.GlobalPosition - TurretHead.GlobalPosition;
        toTarget.Y = 0f;

        toTarget *= -1;

        if (toTarget.IsZeroApprox())
            return;

        // Build desired rotation facing the target
        Quaternion desired = new Quaternion(Basis.LookingAt(toTarget.Normalized(), Vector3.Up));

        // Slerp toward it
        TurretHead.Quaternion = TurretHead.Quaternion.Slerp(desired, RotationSpeed * delta);
    }

    /// Returns true when the muzzle is roughly pointing at the target (within 5°).
    private bool IsAimedAt(Node3D target)
    {
        Vector3 toTarget  = (target.GlobalPosition - Muzzle.GlobalPosition).Normalized();
        Vector3 muzzleFwd = -Muzzle.GlobalTransform.Basis.Z; // Godot: -Z is forward
        return muzzleFwd.Dot(toTarget) > 0.996f;             // cos(5°) ≈ 0.996
    }

    // ── Firing ────────────────────────────────────────────────────────────────

    private void Fire()
    {
        if (BulletScene is null)
        {
            GD.PushWarning("Turret: BulletScene is not assigned.");
            return;
        }

        var bullet = BulletScene.Instantiate<Node3D>();
        
        var t = Transform3D.Identity;
        t.Origin = Muzzle.GlobalPosition;
        t.Basis  = new Basis(Muzzle.GlobalTransform.Basis.GetRotationQuaternion());
        bullet.GlobalTransform = t;
        GD.Print("Y Rotation: " + bullet.Rotation.Y);
        
        // DO NOT TOUCH THIS CODE HOLY FUCK
        Vector3 rot = bullet.RotationDegrees;
        rot.Y += 180f;
        bullet.RotationDegrees = rot;
        // DO NOT TOUCH THIS CODE HOLY FUCK
        
        GetTree().CurrentScene.AddChild(bullet);
    }

    // ── FOV callbacks ─────────────────────────────────────────────────────────

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("players") && !_targetsInFOV.Contains(body))
            _targetsInFOV.Add(body);
    }

    private void OnBodyExited(Node3D body)
    {
        _targetsInFOV.Remove(body);
    }
}