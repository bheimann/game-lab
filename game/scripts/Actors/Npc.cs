using System.Linq;
using Godot;
using LizardWizard.Dungeons;
using LizardWizard.Util;

namespace LizardWizard.Actors;

/// <summary>What a townsperson does with their day.</summary>
public enum NpcBehaviour
{
    /// <summary>Stands in one spot, facing <see cref="Npc.FacingDirection"/> (a guard).</summary>
    Stand,

    /// <summary>Wanders around near where they were placed, stopping now and then.</summary>
    Roam,

    /// <summary>Wanders a little behind their <see cref="Npc.Counter"/>, and steps up to it (and stays) while a hero is
    /// at the counter.</summary>
    Shopkeeper,
}

/// <summary>
/// A townsperson: villagers, shopkeepers, knights, the king and queen. They can't be hurt and nobody bumps into them
/// (heroes walk through them; walls and furniture still stop them). Drawn from 3-view sprite strips like the heroes
/// ("&lt;prefix&gt;_down.png", "_up.png", "_side.png", made by tools/class_looks.py).
/// A townsperson with a <see cref="Greeting"/> says it each time the heroes come into their room.
/// </summary>
public partial class Npc : CharacterBody2D
{
    private const double WalkFps = 6;

    private AnimatedSprite2D _sprite = null!;
    private Vector2 _home;
    private Vector2 _target;
    private float _wait;
    private ulong _lastFrame;
    private Label? _bubble;
    private float _bubbleLeft;

    [Export] public string SpritePrefix { get; set; } = "res://assets/sprites/npcs/villager_fox";

    [Export] public int FrameWidth { get; set; } = 16;

    [Export] public int SideFrameWidth { get; set; } = 20;

    [Export] public NpcBehaviour Behaviour { get; set; } = NpcBehaviour.Stand;

    /// <summary>Which way a standing townsperson looks.</summary>
    [Export] public Vector2 FacingDirection { get; set; } = Vector2.Down;

    /// <summary>How far from home a roaming townsperson wanders, in pixels (shopkeepers: sideways only).</summary>
    [Export] public float RoamDistance { get; set; } = 48f;

    [Export] public float Speed { get; set; } = 26f;

    /// <summary>Said whenever the heroes walk into this room (e.g. a shopkeeper's welcome).</summary>
    [Export(PropertyHint.MultilineText)] public string Greeting { get; set; } = "";

    /// <summary>False for someone who only greets along with a partner (see <see cref="GreetsWith"/>).</summary>
    [Export] public bool GreetsOnEnter { get; set; } = true;

    /// <summary>Someone to greet the heroes with (the king and queen): on the way in, it's one of them or both.</summary>
    [Export] public NodePath? GreetsWith { get; set; }

    /// <summary>A shopkeeper's counter (see <see cref="ShopCounter"/>).</summary>
    [Export] public NodePath? Counter { get; set; }

    public Vector2 Facing { get; private set; } = Vector2.Down;

    public override void _Ready()
    {
        // Their own layer: solid things stop them, but heroes, enemies and shots go right through.
        CollisionLayer = 0;
        CollisionMask = 1;
        MotionMode = MotionModeEnum.Floating;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(10, 6) }, Position = new Vector2(0, -3) });

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        foreach (var (view, width) in new[] { ("down", FrameWidth), ("up", FrameWidth), ("side", SideFrameWidth) })
        {
            var strip = GD.Load<Texture2D>($"{SpritePrefix}_{view}.png");
            SpriteSheet.AddFrames(frames, $"idle_{view}", strip, width, 1, new[] { 0 });
            SpriteSheet.AddFrames(frames, $"walk_{view}", strip, width, WalkFps, new[] { 1, 0, 2, 0 });
        }

        var height = frames.GetFrameTexture("idle_down", 0).GetHeight();
        _sprite = new AnimatedSprite2D { SpriteFrames = frames, Offset = new Vector2(0, -height / 2f) };
        AddChild(_sprite);

        _home = Position;
        if (Counter is not null && !Counter.IsEmpty && GetNodeOrNull<ShopCounter>(Counter) is { } counter)
        {
            counter.Keeper = this;
        }

        _target = _home;
        _wait = (float)GD.RandRange(0.5, 2.5);
        Facing = FacingDirection.Normalized();
        Look(walking: false);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;

        // The room only runs while the heroes are in it: a gap in the frames means they just walked in.
        var frame = Engine.GetPhysicsFrames();
        if (frame - _lastFrame > 2 && Greeting != "" && GreetsOnEnter)
        {
            GreetHeroes();
        }

        _lastFrame = frame;
        UpdateBubble(dt);

        var goal = Behaviour switch
        {
            NpcBehaviour.Roam => Wander(dt),
            NpcBehaviour.Shopkeeper => Tend(dt),
            _ => (Vector2?)null,
        };

        var walking = goal is { } at && Position.DistanceTo(at) > 1.5f;
        if (walking)
        {
            var step = (goal!.Value - Position).Normalized();
            Velocity = step * Speed;
            Facing = Vector2.Right.Rotated(Mathf.Snapped(step.Angle(), Mathf.Pi / 2));
            MoveAndSlide();
            if (GetSlideCollisionCount() > 0)
            {
                _target = Position; // bumped into something: stop and pick somewhere else later
            }
        }

        Look(walking);
    }

    /// <summary>Show a speech bubble over this townsperson's head.</summary>
    public void Say(string text, float seconds = 3.5f)
    {
        _bubble?.QueueFree();
        // Wraps onto more lines rather than running off the screen; the bottom line sits just over the head.
        _bubble = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Size = new Vector2(130, 60),
            Position = new Vector2(-65, _sprite.Offset.Y * 2 - 62),
            ZIndex = 20,
            LabelSettings = new LabelSettings { FontSize = 8, OutlineSize = 3, OutlineColor = new Color(0.08f, 0.09f, 0.18f) },
        };
        AddChild(_bubble);
        _bubbleLeft = seconds;
    }

    private void GreetHeroes()
    {
        if (GreetsWith is not null && !GreetsWith.IsEmpty && GetNodeOrNull<Npc>(GreetsWith) is { } partner)
        {
            // One of them or both.
            var who = GD.RandRange(0, 2);
            if (who != 1)
            {
                Say(Greeting);
            }

            if (who != 0)
            {
                partner.Say(partner.Greeting);
            }

            return;
        }

        Say(Greeting);
    }

    private void UpdateBubble(float dt)
    {
        if (_bubble is null)
        {
            return;
        }

        _bubbleLeft -= dt;
        _bubble.Modulate = Colors.White with { A = Mathf.Clamp(_bubbleLeft / 0.4f, 0, 1) };
        if (_bubbleLeft <= 0)
        {
            _bubble.QueueFree();
            _bubble = null;
        }
    }

    private Vector2? Wander(float dt)
    {
        if (Position.DistanceTo(_target) > 1.5f)
        {
            return _target;
        }

        _wait -= dt;
        if (_wait <= 0)
        {
            _wait = (float)GD.RandRange(1.5, 4.0);
            _target = _home + Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * GD.Randf() * RoamDistance;
        }

        return null;
    }

    private Vector2? Tend(float dt)
    {
        if (Counter is not null && !Counter.IsEmpty && GetNodeOrNull<ShopCounter>(Counter) is { HasCustomer: true })
        {
            // A customer: step up to the middle of the counter and stay there, facing them.
            _target = _home;
            if (Position.DistanceTo(_home) <= 1.5f)
            {
                Facing = Vector2.Down;
            }

            return _home;
        }

        // Pottering about behind the counter.
        if (Position.DistanceTo(_target) > 1.5f)
        {
            return _target;
        }

        _wait -= dt;
        if (_wait <= 0)
        {
            _wait = (float)GD.RandRange(1.0, 3.0);
            _target = _home + new Vector2((float)GD.RandRange(-RoamDistance, RoamDistance), (float)GD.RandRange(-6.0, 0.0));
        }

        return null;
    }

    private void Look(bool walking)
    {
        var view = Mathf.Abs(Facing.Y) > Mathf.Abs(Facing.X) + 0.01f ? (Facing.Y > 0 ? "down" : "up") : "side";
        var animation = $"{(walking ? "walk" : "idle")}_{view}";
        if (_sprite.Animation != animation)
        {
            _sprite.Play(animation);
        }

        _sprite.FlipH = view == "side" && Facing.X < 0;
    }
}
