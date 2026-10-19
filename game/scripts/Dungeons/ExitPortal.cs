using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;

namespace LizardWizard.Dungeons;

/// <summary>
/// The way out of a beaten dungeon: a swirling portal that appears in the boss room once the dungeon is complete
/// (and is still there when the heroes come back, or after loading a save). Any hero who stands on it for
/// <see cref="StandSeconds"/> goes back out to the overworld (with anyone else standing on it). Until then they can keep exploring.
/// </summary>
public partial class ExitPortal : Node2D
{
    private float _time;
    private float _standing;
    private bool _shown;
    private bool _used;

    /// <summary>How close (in pixels) a hero's feet must be to the middle.</summary>
    [Export] public float Radius { get; set; } = 12f;

    [Export] public float StandSeconds { get; set; } = 1f;

    public bool IsOpen => _shown;

    public override void _Ready()
    {
        ZIndex = -1; // on the floor
        Visible = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        if (Dungeon.Find(this) is not { } dungeon || DungeonRoom.Of(this) is not { } room || !dungeon.IsDungeonComplete(room.Area))
        {
            return;
        }

        if (!_shown)
        {
            _shown = true;
            Visible = true;
            if (room.IsActive && _time > 1f)
            {
                // Just beaten (not loaded from a save): make a bit of a show of it.
                Scale = new Vector2(0.1f, 0.1f);
                CreateTween().TweenProperty(this, "scale", Vector2.One, 0.6f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -24), "A way out!", new Color(0.7f, 0.85f, 1f), 2f, 10f);
            }
        }

        var standingOnIt = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
            .Where(h => h.IsInGame && !h.IsDown && h.GlobalPosition.DistanceTo(GlobalPosition) <= Radius).ToList();
        var onIt = standingOnIt.Count > 0;
        _standing = onIt ? _standing + dt : 0;
        if (_standing >= StandSeconds && !_used)
        {
            _used = true;
            dungeon.LeaveThroughPortal(room, standingOnIt);
        }
        else if (!onIt)
        {
            _used = false;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        // A flat swirl of blue and violet rings on the floor, with sparks rising out of it.
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.5f));
        DrawCircle(Vector2.Zero, 18f, new Color(0.25f, 0.35f, 0.9f, 0.35f));
        for (var i = 0; i < 3; i++)
        {
            var start = _time * (2f + i) + i * 2f;
            var colour = i % 2 == 0 ? new Color(0.55f, 0.75f, 1f) : new Color(0.8f, 0.55f, 1f);
            DrawArc(Vector2.Zero, 16f - i * 4f, start, start + 4f, 20, colour, 2f);
        }

        DrawCircle(Vector2.Zero, 4f, new Color(0.9f, 0.95f, 1f, 0.9f));

        // How long you've stood on it: a ring that fills up.
        if (_standing > 0)
        {
            DrawArc(Vector2.Zero, 20f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Mathf.Min(1f, _standing / StandSeconds), 32, Colors.White, 2f);
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        for (var i = 0; i < 6; i++)
        {
            var t = (_time * 0.8f + i / 6f) % 1f;
            var x = Mathf.Sin(i * 2.3f + _time) * 10f;
            DrawCircle(new Vector2(x, -t * 26f), 1.2f * (1 - t), new Color(0.75f, 0.85f, 1f, 1 - t));
        }
    }
}
