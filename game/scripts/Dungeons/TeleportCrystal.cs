using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Dungeons;

/// <summary>
/// The crystal in a castle's teleport room. A hero walking up to it wakes up this village's teleport (see
/// <see cref="Teleports"/>): from then on the pad for this village glows in every teleport room. Asleep it's dull grey;
/// awake it glows and sparkles.
/// </summary>
public partial class TeleportCrystal : Node2D
{
    private float _time;

    /// <summary>The village this crystal belongs to (the same name as its pads, e.g. "Hoofhollow").</summary>
    [Export] public string Village { get; set; } = "";

    public bool IsAwake => Teleports.IsUnlocked(Village);

    public override void _PhysicsProcess(double delta)
    {
        _time += (float)delta;
        if (!IsAwake)
        {
            var hero = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
                .FirstOrDefault(h => h.IsInGame && !h.IsDown && h.GlobalPosition.DistanceTo(GlobalPosition) < 22);
            if (hero is not null && Teleports.Unlock(Village))
            {
                FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -40), $"The {Village} teleport is awake!", new Color(0.6f, 0.9f, 1f), 2.5f, 9f);
                Saving.SaveGame.Save(this);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var awake = IsAwake;
        var outline = new Color(0.08f, 0.09f, 0.18f);
        var light = awake ? new Color(0.65f, 0.95f, 1f) : new Color(0.6f, 0.62f, 0.68f);
        var dark = awake ? new Color(0.3f, 0.6f, 0.95f) : new Color(0.38f, 0.4f, 0.46f);
        var bob = awake ? Mathf.Sin(_time * 2f) * 2f : 0f;

        // A stone stand, and the crystal floating over it.
        DrawRect(new Rect2(-9, -6, 18, 6), outline);
        DrawRect(new Rect2(-8, -5, 16, 4), new Color(0.55f, 0.55f, 0.6f));
        var top = new Vector2(0, -34 + bob);
        var points = new[] { top, new Vector2(7, -22 + bob), new Vector2(0, -9 + bob), new Vector2(-7, -22 + bob) };
        DrawColoredPolygon(points, outline);
        DrawColoredPolygon(points.Select(p => p + (new Vector2(0, -22 + bob) - p) * 0.15f).ToArray(), dark);
        DrawColoredPolygon(new[] { top + new Vector2(0, 3), new Vector2(4, -22 + bob), new Vector2(0, -22 + bob) }, light);
        if (awake)
        {
            DrawCircle(new Vector2(0, -22 + bob), 12f + Mathf.Sin(_time * 3f) * 2f, new Color(0.5f, 0.85f, 1f, 0.15f));
            for (var i = 0; i < 3; i++)
            {
                var t = Mathf.PosMod(_time * 0.7f + i / 3f, 1f);
                DrawCircle(new Vector2(Mathf.Sin(i * 2.1f + _time) * 8f, -10 - t * 30f), 1f, Colors.White with { A = 1 - t });
            }
        }
    }
}
