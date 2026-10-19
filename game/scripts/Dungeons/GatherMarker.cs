using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;

namespace LizardWizard.Dungeons;

/// <summary>
/// A ring of stones on the floor of each dungeon's foyer and each castle's teleport room. A hero who waits on it for
/// <see cref="WaitSeconds"/> can be joined by the others from wherever they are: their ring menu (Start) gets a
/// "Join" ring with that hero in it, which brings them here (see <see cref="Dungeon.JoinAt"/>). The others are told
/// when someone starts waiting. The last one anyone reached is where the team starts after loading a save.
/// </summary>
public partial class GatherMarker : Node2D
{
    public const string Group = "gather_marker";

    private const float WaitSeconds = 1f;
    private static readonly Color Stone = new(0.55f, 0.56f, 0.62f);
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);

    private readonly Dictionary<Hero, float> _standing = new();
    private float _time;

    /// <summary>How close (in pixels) a hero's feet must be to the middle.</summary>
    [Export] public float Radius { get; set; } = 10f;

    public static IEnumerable<GatherMarker> All(SceneTree tree) => tree.GetNodesInGroup(Group).OfType<GatherMarker>();

    /// <summary>Heroes who've waited on it long enough for the others to join them.</summary>
    public IEnumerable<Hero> Waiting =>
        _standing.Where(s => s.Value >= WaitSeconds && IsInstanceValid(s.Key) && s.Key.IsInGame && IsOn(s.Key)).Select(s => s.Key);

    /// <summary>Where someone who joins appears: just below the stones (so they aren't waiting on it themselves).</summary>
    public Vector2 JoinPoint => GlobalPosition + new Vector2(0, 22);

    /// <summary>What to call this place ("Mossmire Keep's foyer", "Scalebrook Teleport Room").</summary>
    public string PlaceName => DungeonRoom.Of(this) is { } room ? room.Area != "" ? $"{room.Area}'s {room.Title.ToLowerInvariant()}" : room.Title : "here";

    public override void _Ready()
    {
        AddToGroup(Group);
        ZIndex = -1; // on the floor
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        var heroes = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(h => h.IsInGame).ToList();
        foreach (var gone in _standing.Keys.Where(h => !IsInstanceValid(h) || !h.IsInGame || !IsOn(h)).ToList())
        {
            _standing.Remove(gone);
        }

        foreach (var hero in heroes.Where(IsOn))
        {
            var before = _standing.GetValueOrDefault(hero);
            _standing[hero] = before + dt;
            if (before < WaitSeconds && before + dt >= WaitSeconds)
            {
                Announce(hero, heroes);
            }
        }

        QueueRedraw();
    }

    /// <summary>Tell the hero they're waiting, and everyone in another room that they can join.</summary>
    private void Announce(Hero hero, List<Hero> heroes)
    {
        var dungeon = Dungeon.Find(this);
        var others = heroes.Where(h => h != hero && (dungeon is null || dungeon.RoomOf(h) != dungeon.RoomOf(hero))).ToList();
        if (others.Count == 0)
        {
            return;
        }

        FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), "Waiting here.\nThe others can join you from their menu.",
            hero.PlayerColour.Lightened(0.4f), 2.5f, 7f);
        foreach (var other in others)
        {
            dungeon?.Hud.ShowBanner($"{hero.Name} is waiting in {PlaceName}", "Open your menu (Start) to join them", 3f, other);
        }
    }

    private bool IsOn(Hero hero) => !hero.IsDown && hero.GlobalPosition.DistanceTo(GlobalPosition) <= Radius;

    public override void _Draw()
    {
        // A flat ring of stones with a rune in the middle; it glows in the colour of whoever is waiting on it.
        var waiting = Waiting.FirstOrDefault();
        var glow = waiting?.PlayerColour ?? new Color(0.75f, 0.8f, 0.95f);
        var pulse = waiting is null ? 0.35f : 0.65f + 0.35f * Mathf.Sin(_time * 4f);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.5f));
        DrawCircle(Vector2.Zero, 15f, Outline with { A = 0.55f });
        DrawArc(Vector2.Zero, 9f, 0, Mathf.Tau, 24, glow with { A = pulse }, 2f);
        for (var i = 0; i < 8; i++)
        {
            var at = Vector2.Right.Rotated(i * Mathf.Tau / 8) * 14f;
            DrawCircle(at, 3f, Outline);
            DrawCircle(at, 2.2f, Stone);
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var rune = glow with { A = pulse };
        DrawLine(new Vector2(-3, -2), new Vector2(3, 2), rune, 1.2f);
        DrawLine(new Vector2(-3, 2), new Vector2(3, -2), rune, 1.2f);
    }
}
