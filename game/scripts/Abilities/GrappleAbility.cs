using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Dungeons;

namespace LizardWizard.Abilities;

/// <summary>
/// The peacock's grapple gun: fires a hook on a rope. What happens depends on what it catches:
/// a light enemy (<see cref="IPullable"/>) gets reeled in like the lizard's tongue; a heavy one (a boss) pulls the
/// hero over to it; a wall, rock or bush pulls the hero over to that, flying over pits and water on the way.
/// Misses come back. Aim assist bends the hook toward an enemy or a rock/bush roughly in front.
/// </summary>
/// <remarks>
/// The hook travels at foot level (so it catches rocks and bushes, which are only solid at the bottom) but is drawn
/// at the height of the gun. If a zip ends with the hero over a pit (it got hit halfway), it goes back to where it started.
/// </remarks>
public partial class GrappleAbility : Ability
{
    private const float FootHeight = 3f;

    private State _state = State.Idle;
    private Vector2 _direction;
    private Vector2 _tip; // global, at foot level
    private float _travelled;
    private float _time;
    private Vector2 _start;
    private Vector2 _zipTarget;
    private IHittable? _caught;
    private Vector2 _caughtLastPosition;
    private float _pullSpeed;
    private float _stuckTime;
    private Vector2 _lastFeet;

    private enum State
    {
        Idle,
        Out,
        Back,
        Reel,
        Zip,
    }

    [Export] public int Damage { get; set; } = 2;

    /// <summary>How far the hook flies, in pixels.</summary>
    [Export] public float Range { get; set; } = 130f;

    [Export] public float HookSpeed { get; set; } = 420f;

    /// <summary>How fast the rope pulls the hero along, in pixels per second.</summary>
    [Export] public float ZipSpeed { get; set; } = 240f;

    /// <summary>How fast a caught enemy gets reeled in, in pixels per second.</summary>
    [Export] public float PullSpeed { get; set; } = 200f;

    [Export] public float AimAssistDegrees { get; set; } = 25f;

    [Export] public Color RopeColour { get; set; } = new(0.85f, 0.75f, 0.55f);

    public override bool IsActive => _state != State.Idle;

    public override float MoveFactor => 0f;

    public override Vector2? Motion => _state == State.Zip ? (_zipTarget - Hero.GlobalPosition).Normalized() * ZipSpeed : null;

    public override bool CrossesPits => _state == State.Zip;

    /// <summary>From the hook's foot-level path up to where it's drawn (the gun's height).</summary>
    private Vector2 Lift => Hero.AttackOrigin + new Vector2(0, FootHeight);

    private Vector2 Feet => Hero.GlobalPosition + new Vector2(0, -FootHeight);

    protected override void Use()
    {
        _state = State.Out;
        _tip = Feet;
        _travelled = 0;
        _caught = null;

        // Aim assist: enemies (by their middle) and rocks / bushes (by their solid bottom).
        var gun = Hero.ToGlobal(Hero.AttackOrigin);
        var enemies = Hero.EnemyTargets().Select(t => t.HurtCenter - gun);
        var props = GetTree().GetNodesInGroup(Prop.Group).OfType<Node2D>().Select(p => p.GlobalPosition + new Vector2(0, -FootHeight) - Feet);
        var assisted = enemies.Concat(props)
            .Where(d => d.Length() <= Range && Mathf.RadToDeg(Mathf.Abs(Hero.Facing.AngleTo(d))) <= AimAssistDegrees)
            .OrderBy(d => d.Length())
            .FirstOrDefault();
        _direction = assisted == Vector2.Zero ? Hero.Facing : assisted.Normalized();
        Hero.Lunge(-_direction * 2f);
    }

    public override void Cancel()
    {
        // Let go in mid-air over a pit: back to where the zip started.
        if (_state == State.Zip && Hero.OverPit(Hero.GlobalPosition))
        {
            Hero.Teleport(_start);
        }

        _state = State.Idle;
        _caught = null;
        QueueRedraw();
    }

    protected override void Tick(float dt)
    {
        switch (_state)
        {
            case State.Out:
                FlyOut(dt);
                break;
            case State.Back:
                // Reel the empty hook back in.
                _tip = _tip.MoveToward(Feet, HookSpeed * 1.5f * dt);
                if (_tip.DistanceTo(Feet) < 2f)
                {
                    _state = State.Idle;
                }

                break;
            case State.Reel:
                Reel(dt);
                break;
            case State.Zip:
                Zip(dt);
                break;
        }

        QueueRedraw();
    }

    private void FlyOut(float dt)
    {
        var from = _tip;
        var to = from + _direction * Mathf.Min(HookSpeed * dt, Range - _travelled);
        var lift = Lift;

        var wall = Hero.GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(from, to, collisionMask: 1));
        if (wall.Count > 0)
        {
            to = (Vector2)wall["position"];
        }

        // Enemies are checked along the rope as drawn (at gun height), since that's where their bodies are.
        // Ones behind the gun don't count (an enemy brushing the hero's back).
        var enemy = Hero.EnemyTargets()
            .Where(e => Hits.Touches(e, from + lift, to + lift) && (e.HurtCenter - (Feet + lift)).Dot(_direction) > 0)
            .OrderBy(e => e.HurtCenter.DistanceTo(from + lift))
            .FirstOrDefault();
        if (enemy is not null)
        {
            Hero.Hit(enemy, Damage, Hero.GlobalPosition, DamageType.Physical);
            _tip = enemy.HurtCenter - lift;
            if (enemy is IPullable { CanBePulled: true })
            {
                _state = State.Reel;
                _caught = enemy;
                _caughtLastPosition = enemy.HurtCenter;
                _pullSpeed = 0;
                _stuckTime = 0;
                _time = 0;
            }
            else if (enemy is IEnemy)
            {
                _caught = enemy; // too heavy to pull: fly over to it instead
                StartZip();
            }
            else
            {
                _state = State.Back; // a lever: flip it and come back
            }

            return;
        }

        _tip = to;
        _travelled += from.DistanceTo(to);
        if (wall.Count > 0)
        {
            StartZip();
        }
        else if (_travelled >= Range)
        {
            _state = State.Back;
        }
    }

    private void StartZip()
    {
        _state = State.Zip;
        _start = Hero.GlobalPosition;
        _lastFeet = Hero.GlobalPosition;
        _stuckTime = 0;
        _time = 0;
        UpdateZipTarget();
    }

    /// <summary>Where the hero's feet should end up: just short of the hook (or of the enemy it's stuck in).</summary>
    private void UpdateZipTarget()
    {
        if (_caught is not null)
        {
            if (IsInstanceValid((GodotObject)_caught))
            {
                _tip = _caught.HurtCenter - Lift;
            }

            _zipTarget = _tip + new Vector2(0, FootHeight) - _direction * (_caught.HurtRadius + 8f);
        }
        else
        {
            _zipTarget = _tip + new Vector2(0, FootHeight) - _direction * 6f;
        }
    }

    private void Zip(float dt)
    {
        _time += dt;
        UpdateZipTarget();

        // Bumping into something solid on the way ends the zip early.
        var moved = Hero.GlobalPosition.DistanceTo(_lastFeet);
        _lastFeet = Hero.GlobalPosition;
        _stuckTime = _time > 0.05f && moved < ZipSpeed * dt * 0.25f ? _stuckTime + dt : 0;

        var arrived = Hero.GlobalPosition.DistanceTo(_zipTarget) <= ZipSpeed * dt;
        var overPit = Hero.OverPit(Hero.GlobalPosition);
        if ((arrived && !overPit) || _stuckTime >= 0.1f || _time >= Range / ZipSpeed + 0.4f)
        {
            if (Hero.OverPit(Hero.GlobalPosition))
            {
                Hero.Teleport(_start);
            }

            _caught = null;
            _state = State.Idle;
        }
    }

    /// <summary>The hook is stuck in a light enemy: drag it in until it's right in front of us, then let go.</summary>
    private void Reel(float dt)
    {
        var caught = _caught!;
        if (!IsInstanceValid((GodotObject)caught))
        {
            _caught = null;
            _state = State.Back;
            return;
        }

        _time += dt;
        _tip = caught.HurtCenter - Lift;

        // A pit or wall in the way stops the enemy, and then the hook slips off.
        var moved = caught.HurtCenter.DistanceTo(_caughtLastPosition);
        _caughtLastPosition = caught.HurtCenter;
        _stuckTime = _pullSpeed > 0 && moved < _pullSpeed * dt * 0.25f ? _stuckTime + dt : 0;

        var toGun = Hero.ToGlobal(Hero.AttackOrigin) - caught.HurtCenter;
        var gap = caught.HurtRadius + 6f;
        if (toGun.Length() <= gap || _time >= Range / PullSpeed + 0.3f || _stuckTime >= 0.1f)
        {
            _caught = null;
            _state = State.Back;
            return;
        }

        _pullSpeed = Mathf.Min(PullSpeed, (toGun.Length() - gap) / dt);
        ((IPullable)caught).Pull(toGun.Normalized() * _pullSpeed);
    }

    public override void _Draw()
    {
        if (_state == State.Idle)
        {
            return;
        }

        // The grapple gun in hand, the rope, and a three-pronged hook at the end.
        var gun = Hero.AttackOrigin;
        var tip = ToLocal(_tip) + Lift;
        var outline = new Color(0.08f, 0.09f, 0.18f);
        DrawRect(new Rect2(gun - new Vector2(2, 2), new Vector2(4, 4)), new Color(0.45f, 0.5f, 0.55f));
        DrawLine(gun, tip, RopeColour, 1f);
        var along = (tip - gun).Normalized();
        if (along == Vector2.Zero)
        {
            along = _direction;
        }

        foreach (var angle in new[] { -0.7f, 0f, 0.7f })
        {
            DrawLine(tip, tip + along.Rotated(angle) * 3.5f, new Color(0.75f, 0.78f, 0.85f), 1.5f);
        }

        DrawCircle(tip, 1.5f, outline);
    }
}
