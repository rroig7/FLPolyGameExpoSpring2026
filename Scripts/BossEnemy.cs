using Godot;
using System;
using System.Collections.Generic;

public partial class BossEnemy : CharacterBody3D
{
    // ── Exports ───────────────────────────────────────────────────────────────
    [Export] public NetID myId;
    [Export] public PackedScene BulletScene;

    [Export] public float FireRate       = 1.0f;
    [Export] public float BulletSpeed    = 20.0f;
    [Export] public float RotationSpeed  = 5.0f;

    [Export] public float MeleeRange     = 2.5f;
    [Export] public float MeleeCooldown  = 1.2f;
    [Export] public float MeleeDamage    = 25.0f;

    [Export] public float MoveSpeed      = 4.0f;
    [Export] public float PatrolRadius   = 20.0f;

    [Export] public uint CollisionMask   = 1;
    
    [Export] public Node3D Muzzle;

    // ── Synced properties (matched to MeleeEnemy pattern) ────────────────────
    [Export] public Vector3 SyncedVelocity
    {
        get => Velocity;
        set => Velocity = value;
    }
    [Export] public bool SyncedIsMoving   = false;
    [Export] public bool SyncedIsChasing  = false;

    // ── State ─────────────────────────────────────────────────────────────────
    private enum BossState { Idle, Chase, Melee, Ranged }
    private BossState _state = BossState.Idle;

    private Node3D       _currentTarget;
    private List<Node3D> _targetsInFOV  = new();
    private float        _fireCooldown  = 1f;
    private float        _meleeCooldown = 2.5f;
    private bool         _isAcquiring   = false;
    private Vector3      _spawnPosition;

    private Area3D _bossFOV;

    public override void _Ready()
    {
        _spawnPosition = GlobalPosition;

        if (myId == null)
            myId = GetNodeOrNull<NetID>("MultiplayerSynchronizer");

        _bossFOV = GetNode<Area3D>("BossFOV");
        _bossFOV.BodyEntered += OnBodyEntered;
        _bossFOV.BodyExited  += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        if (myId == null || !myId.IsNetworkReady) return;

        float dt = (float)delta;

        // ── Server drives all logic ───────────────────────────────────────────
        if (GenericCore.Instance.IsServer)
        {
            _fireCooldown  -= dt;
            _meleeCooldown -= dt;

            Node3D newTarget = GetClosestTarget();
            if (newTarget != _currentTarget)
            {
                _currentTarget = newTarget;
                _isAcquiring   = true;
            }

            _state = ChooseState();

            switch (_state)
            {
                case BossState.Idle:
                    SyncedIsMoving  = false;
                    SyncedIsChasing = false;
                    break;

                case BossState.Chase:
                    MoveToward(_currentTarget, dt);
                    FaceTarget(_currentTarget, dt);
                    SyncedIsMoving  = true;
                    SyncedIsChasing = true;
                    break;

                case BossState.Melee:
                    Velocity = new Vector3(0, Velocity.Y, 0);
                    FaceTarget(_currentTarget, dt);
                    SyncedIsMoving  = false;
                    SyncedIsChasing = true;
                    if (_meleeCooldown <= 0f)
                        DoMeleeAttack();
                    break;

                case BossState.Ranged:
                    Velocity = new Vector3(0, Velocity.Y, 0);
                    FaceTarget(_currentTarget, dt);
                    SyncedIsMoving  = false;
                    SyncedIsChasing = true;

                    if (_isAcquiring && IsAimedAt(_currentTarget))
                        _isAcquiring = false;

                    if (!_isAcquiring && _fireCooldown <= 0f && IsAimedAt(_currentTarget))
                    {
                        GD.Print("BOSS ATTEMPTED TO FIRE");
                        Fire();
                        _fireCooldown = 1f / FireRate;
                    }
                    break;
            }

            SyncedVelocity = Velocity;
        }

        // ── Clients only update visuals ───────────────────────────────────────
        if (!GenericCore.Instance.IsServer)
            UpdateAnimation();
    }

    // ── State selection ───────────────────────────────────────────────────────

    private BossState ChooseState()
    {
        if (_currentTarget == null)
            return BossState.Idle;

        float dist = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

        if (GlobalPosition.DistanceTo(_spawnPosition) > PatrolRadius)
            return BossState.Ranged;

        if (dist <= MeleeRange)
            return BossState.Melee;

        if (!HasLineOfSight(_currentTarget))
            return BossState.Chase;

        return BossState.Ranged;
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
            if (d < minDist) { minDist = d; closest = body; }
        }
        return closest;
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private void MoveToward(Node3D target, float delta)
    {
        Vector3 dir = target.GlobalPosition - GlobalPosition;
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.01f) return;

        Velocity = new Vector3(dir.Normalized().X * MoveSpeed, Velocity.Y, dir.Normalized().Z * MoveSpeed);
        MoveAndSlide();
    }

    // ── Rotation ──────────────────────────────────────────────────────────────

    private void FaceTarget(Node3D target, float delta)
    {
        Vector3 toTarget = target.GlobalPosition - GlobalPosition;
        toTarget.Y = 0f;
        if (toTarget.IsZeroApprox()) return;

        Basis desired   = Basis.LookingAt(toTarget.Normalized(), Vector3.Up)
            .Rotated(Vector3.Up, Mathf.DegToRad(180));
        Quaternion from = new Quaternion(GlobalBasis.Orthonormalized());
        Quaternion to   = new Quaternion(desired.Orthonormalized());

        GlobalBasis = new Basis(from.Slerp(to, RotationSpeed * delta)).Orthonormalized();
    }

    private bool IsAimedAt(Node3D target)
    {
        Vector3 toTarget = target.GlobalPosition - GlobalPosition;
        toTarget.Y = 0f;
        toTarget = toTarget.Normalized();

        Vector3 forward = -GlobalTransform.Basis.Z;
        forward.Y = 0f;
        forward = forward.Normalized();
        
        // return forward.Dot(toTarget) > 0.98f; // slightly relaxed threshold
        return true;
    }

    // ── Line of sight ─────────────────────────────────────────────────────────

    private bool HasLineOfSight(Node3D target)
    {
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
            GlobalPosition,
            target.GlobalPosition,
            CollisionMask
        );
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return result.Count == 0 ||
               result["collider"].Obj is Node hit && hit == target;
    }

    // ── Melee ─────────────────────────────────────────────────────────────────

    private void DoMeleeAttack()
    {
        _meleeCooldown = MeleeCooldown;

        if (_currentTarget != null && _currentTarget.HasMethod("TakeDamage"))
            _currentTarget.Call("TakeDamage", MeleeDamage);
    }

    // ── Firing ────────────────────────────────────────────────────────────────

    private void Fire()
    {
        if (BulletScene is null)
        {
            GD.PushWarning("BossEnemy: BulletScene is not assigned.");
            return;
        }

        var bullet = BulletScene.Instantiate<Node3D>();

        // DO NOT TOUCH THIS CODE HOLY FUCK
        // Vector3 forward = -GlobalTransform.Basis.Z;
        // bullet.GlobalTransform = new Transform3D(
        //     Basis.LookingAt(forward, Vector3.Up),
        //     Muzzle.GlobalPosition
        // );
        
        Vector3 forward = -GlobalTransform.Basis.Z;

        bullet.GlobalTransform = new Transform3D(
            Basis.LookingAt(Muzzle.GlobalPosition + forward, Vector3.Up),
            Muzzle.GlobalPosition
        );
        // DO NOT TOUCH THIS CODE HOLY FUCK

        GetTree().CurrentScene.AddChild(bullet);
        // GetTree().Root.AddChild(bullet);
    }

    // ── Animation (clients only) ──────────────────────────────────────────────

    private void UpdateAnimation()
    {
        // Wire up your AnimationPlayer here the same way MeleeEnemy does it.
        // Example (add [Export] public AnimationPlayer myAnimation; at the top):
        //
        // if (myAnimation == null) return;
        // if (!SyncedIsMoving && SyncedIsChasing)
        //     myAnimation.Play("Attacking");
        // else if (SyncedIsMoving && SyncedIsChasing)
        //     myAnimation.Play("Running");
        // else
        //     myAnimation.Play("Idle");
    }

    // ── FOV callbacks ─────────────────────────────────────────────────────────

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("players") && !_targetsInFOV.Contains(body))
        {
            _targetsInFOV.Add(body);
            GD.Print($"{body.Name} entered Boss Radius.");
        }
        
    }

    private void OnBodyExited(Node3D body) => _targetsInFOV.Remove(body);
}