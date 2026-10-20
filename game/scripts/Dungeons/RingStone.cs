using System.Globalization;
using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;

namespace LizardWizard.Dungeons;

/// <summary>
/// A heavy stone with an iron ring in it, set in a wall of cracked rocks. Bombs don't break it and nobody can push it,
/// but a lizard's tongue (any class) catches the ring and drags the stone out, opening a gap. Walls and pits stop it.
/// It stays where it was dragged to.
/// </summary>
public partial class RingStone : CharacterBody2D, IHittable, IPullable, Saving.ISaveable
{
    private const string Texture = "res://assets/kenney/rock.png";

    private Vector2 _pull;
    private Vector2 _start;
    private float _clunk;

    public bool CanBeHit => true;

    public bool CanBePulled => true;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -6);

    public float HurtRadius => 7f;

    public override void _Ready()
    {
        AddToGroup(LeverSwitch.Group); // so the tongue (and its aim assist) finds it
        _start = Position;
        MotionMode = MotionModeEnum.Floating;
        // On the pits layer, like a lever's post: nobody walks through it, but a tongue reaches the ring.
        CollisionLayer = Pit.PitLayer;
        CollisionMask = 1 | 2 | Pit.PitLayer; // walls, rocks, heroes, pits
        AddChild(new Sprite2D { Texture = GD.Load<Texture2D>(Texture), Offset = new Vector2(0, -7), ShowBehindParent = true }); // the ring goes on top
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(14, 10) }, Position = new Vector2(0, -4) });
    }

    public string? SaveState() =>
        Position == _start ? null : string.Create(CultureInfo.InvariantCulture, $"{Position.X},{Position.Y}");

    public void LoadState(string state)
    {
        var xy = state.Split(',');
        Position = new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture));
    }

    public void Pull(Vector2 velocity) => _pull = velocity;

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        // Punches and shots just clunk off it (a hint that it's not like the cracked rocks).
        if (_clunk <= 0)
        {
            _clunk = 1f;
            FloatingText.Spawn(this, HurtCenter + new Vector2(0, -10), "Clunk", new Color(0.8f, 0.82f, 0.9f), 0.8f, 8f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _clunk -= (float)delta;
        if (_pull != Vector2.Zero)
        {
            // Slides along the rocks beside it rather than catching on them.
            Velocity = _pull;
            MoveAndSlide();
            Velocity = Vector2.Zero;
            _pull = Vector2.Zero;
        }
    }

    public override void _Draw()
    {
        // An iron ring on the front, so players can tell this one is different.
        var iron = new Color(0.08f, 0.09f, 0.18f);
        DrawArc(new Vector2(0, -6), 3f, 0, Mathf.Tau, 12, iron, 1.5f);
        DrawArc(new Vector2(0, -6), 3f, Mathf.Pi * 1.1f, Mathf.Pi * 1.6f, 6, new Color(0.75f, 0.75f, 0.8f), 1f);
    }
}
