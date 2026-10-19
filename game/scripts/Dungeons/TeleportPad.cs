using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Dungeons;

/// <summary>
/// A portal pad in a castle's teleport room, one for each village. Once that village's teleport is awake (its crystal,
/// see <see cref="TeleportCrystal"/>) the pad glows, and a hero standing on it for <see cref="StandSeconds"/> (like the
/// boss portals) takes them (and anyone else on it) to that village's teleport room. The pad for the village you're in is just a
/// "you are here" marker. The village's name is written under the pad.
/// </summary>
public partial class TeleportPad : Node2D
{
    private float _time;
    private float _standing;
    private float _messageCooldown;
    private bool _used;

    /// <summary>Where it goes (the village's name, as its crystal says it).</summary>
    [Export] public string Village { get; set; } = "";

    /// <summary>Where the team arrives: the "Arrival" doorway in that village's teleport room.</summary>
    [Export] public NodePath? Destination { get; set; }

    /// <summary>This room's own village: the pad doesn't go anywhere.</summary>
    [Export] public bool Here { get; set; }

    [Export] public float Radius { get; set; } = 12f;

    [Export] public float StandSeconds { get; set; } = 1f;

    public bool IsOpen => !Here && Teleports.IsUnlocked(Village);

    public override void _Ready()
    {
        ZIndex = -1; // on the floor
        // The name under the pad, wrapped to the pad's width so neighbours' names don't run together.
        AddChild(new Label
        {
            Text = Here ? $"{Village} (you are here)" : Village,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Position = new Vector2(-22, 9),
            Size = new Vector2(44, 30),
            ZIndex = 2,
            LabelSettings = new LabelSettings { FontSize = 6, OutlineSize = 3, OutlineColor = new Color(0.08f, 0.09f, 0.18f) },
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        _messageCooldown -= dt;
        var hero = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
            .FirstOrDefault(h => h.IsInGame && !h.IsDown && h.GlobalPosition.DistanceTo(GlobalPosition) <= Radius);
        if (hero is null)
        {
            _standing = 0;
            _used = false;
            QueueRedraw();
            return;
        }

        if (!IsOpen)
        {
            if (!Here && _messageCooldown <= 0)
            {
                _messageCooldown = 3f;
                FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -16), $"Asleep: wake the crystal in {Village}'s castle",
                    new Color(0.8f, 0.8f, 0.85f), 2f, 7f);
            }

            QueueRedraw();
            return;
        }

        _standing += dt;
        if (_standing >= StandSeconds && !_used && Destination is not null && GetNodeOrNull<Door>(Destination) is { } arrival
            && Dungeon.Find(this) is { } dungeon)
        {
            _used = true;
            dungeon.Travel(arrival, GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
                .Where(h => h.IsInGame && !h.IsDown && h.GlobalPosition.DistanceTo(GlobalPosition) <= Radius));
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var open = IsOpen;
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.5f));
        DrawCircle(Vector2.Zero, 16f, new Color(0.08f, 0.09f, 0.18f, 0.85f));
        if (!open)
        {
            DrawArc(Vector2.Zero, 13f, 0, Mathf.Tau, 24, Here ? new Color(1f, 0.85f, 0.4f) : new Color(0.45f, 0.47f, 0.52f), 2f);
        }
        else
        {
            // A swirl of blue rings, like the boss portals.
            for (var i = 0; i < 3; i++)
            {
                var start = _time * (2f + i) + i * 2f;
                DrawArc(Vector2.Zero, 13f - i * 4f, start, start + 4f, 20, new Color(0.4f + i * 0.2f, 0.75f, 1f), 2f);
            }

            if (_standing > 0)
            {
                DrawArc(Vector2.Zero, 17f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Mathf.Min(1f, _standing / StandSeconds), 32, Colors.White, 2f);
            }
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
