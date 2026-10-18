using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;

namespace LizardWizard.Dungeons;

/// <summary>
/// A healing herb left out for the heroes (the castle kitchen's free herb), or with <see cref="Key"/> a dungeon key
/// lying on the floor (the Ice Hall's, out on the ice). The first hero to walk up (or slide over it) takes it.
/// It's only there once: the save remembers it's been taken.
/// </summary>
public partial class HerbPickup : Node2D, Saving.ISaveable
{
    private const string HerbIcon = "res://assets/sprites/icons/herb.png";
    private const string KeyIcon = "res://assets/kenney/key.png";

    private Texture2D _herb = null!;
    private bool _taken;
    private float _time;

    [Export] public string Message { get; set; } = "A free herb!";

    /// <summary>A key for the team (for a locked door) instead of a herb.</summary>
    [Export] public bool Key { get; set; }

    public override void _Ready() => _herb = GD.Load<Texture2D>(Key ? KeyIcon : HerbIcon);

    public override void _PhysicsProcess(double delta)
    {
        _time += (float)delta;
        if (_taken)
        {
            return;
        }

        var hero = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
            .FirstOrDefault(h => h.IsInGame && !h.IsDown && h.GlobalPosition.DistanceTo(GlobalPosition) < 12);
        if (hero is not null)
        {
            _taken = true;
            if (Key && Dungeon.Find(this) is { } dungeon)
            {
                dungeon.Keys++;
            }
            else
            {
                hero.AddHerbs(1);
            }

            FloatingText.Spawn(this, hero.HurtCenter + new Vector2(0, -14), Message, new Color(0.5f, 1f, 0.5f), 1.5f, 8f);
            Hide();
        }

        QueueRedraw();
    }

    public string? SaveState() => _taken ? "Taken" : null;

    public void LoadState(string state)
    {
        _taken = true;
        Hide();
    }

    public override void _Draw()
    {
        // On a little plate, bobbing gently.
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.4f));
        DrawCircle(Vector2.Zero, 7f, new Color(0.08f, 0.09f, 0.18f));
        DrawCircle(Vector2.Zero, 6f, new Color(0.92f, 0.92f, 0.95f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawTexture(_herb, new Vector2(-_herb.GetWidth() / 2f, -_herb.GetHeight() - 1 - Mathf.Sin(_time * 3f)));
    }
}
