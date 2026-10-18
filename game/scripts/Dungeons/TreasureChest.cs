using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Effects;
using LizardWizard.Util;

namespace LizardWizard.Dungeons;

public enum ChestReward
{
    /// <summary>A key for a locked door (shared by the team).</summary>
    Key,

    /// <summary>Healing herbs for the team: one for every hero playing.</summary>
    Herbs,
}

/// <summary>A chest that opens when a hero walks up to it. It can stay hidden until the room is cleared.</summary>
public partial class TreasureChest : StaticBody2D, Saving.ISaveable
{
    private const string Strip = "res://assets/kenney/chest.png";

    private AnimatedSprite2D _sprite = null!;
    private CollisionShape2D _shape = null!;
    private DungeonRoom? _room;
    private bool _opened;

    [Export] public ChestReward Reward { get; set; } = ChestReward.Key;

    /// <summary>Hidden until every enemy in the room is beaten.</summary>
    [Export] public bool AppearsWhenCleared { get; set; }

    public bool IsOpened => _opened;

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        _sprite = new AnimatedSprite2D { SpriteFrames = SpriteSheet.Frames("open", Strip, 16, 10, loop: false), Offset = new Vector2(0, -8) };
        AddChild(_sprite);
        _shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(14, 8) }, Position = new Vector2(0, -4) };
        AddChild(_shape);

        _room = DungeonRoom.Of(this);
        if (AppearsWhenCleared && _room is not null)
        {
            Hide();
            _shape.Disabled = true;
            _room.Cleared += Appear;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_opened || !Visible)
        {
            return;
        }

        var hero = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>()
            .FirstOrDefault(h => !h.IsDown && h.GlobalPosition.DistanceTo(GlobalPosition) < 16);
        if (hero is not null)
        {
            Open(hero);
        }
    }

    public string? SaveState() => _opened ? "Opened" : AppearsWhenCleared && Visible ? "Appeared" : null;

    public void LoadState(string state)
    {
        Show();
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
        if (state == "Opened")
        {
            _opened = true;
            _sprite.Play("open");
            _sprite.Frame = _sprite.SpriteFrames.GetFrameCount("open") - 1;
        }
    }

    private void Appear()
    {
        if (Visible)
        {
            return; // already there (loaded from a save)
        }

        Show();
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
        Scale = new Vector2(0.2f, 0.2f);
        CreateTween().TweenProperty(this, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -20), "A chest appeared!", new Color(1f, 0.9f, 0.5f), 1.6f, 8f);
    }

    private void Open(Hero hero)
    {
        _opened = true;
        _sprite.Play("open");
        var heroes = GetTree().GetNodesInGroup(Hero.Group).OfType<Hero>().Where(h => h.IsInGame).ToList();
        switch (Reward)
        {
            case ChestReward.Key when Dungeon.Find(this) is { } dungeon:
                dungeon.Keys++;
                FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -22), "You found a key!", new Color(1f, 0.85f, 0.3f), 2f, 10f);
                break;

            case ChestReward.Herbs:
                var herbs = System.Math.Max(1, heroes.Count);
                Items.Purse.AddHerbs(herbs);
                FloatingText.Spawn(this, GlobalPosition + new Vector2(0, -22), $"Healing herbs for the team! (+{herbs})", new Color(0.5f, 1f, 0.5f), 2f, 10f);
                break;
        }
    }
}
