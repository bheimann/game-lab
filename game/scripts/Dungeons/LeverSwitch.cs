using Godot;
using LizardWizard.Combat;
using LizardWizard.Effects;

namespace LizardWizard.Dungeons;

/// <summary>
/// A lever you flip by hitting it (punch, bite, tongue...). It stays on. Hittable things that aren't
/// enemies live in the <see cref="Group"/> group so heroes' attacks (and the tongue's aim assist) find them.
/// </summary>
public partial class LeverSwitch : Node2D, IHittable, ISwitch, Saving.ISaveable
{
    public const string Group = "targets";

    private const string Texture = "res://assets/third_party/tiny_dungeon/Misc/Switches/Switch_blue_orange.png";

    private Sprite2D _sprite = null!;

    public bool IsOn { get; private set; }

    public bool CanBeHit => !IsOn;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -8);

    public float HurtRadius => 6f;

    public override void _Ready()
    {
        AddToGroup(Group);
        _sprite = new Sprite2D
        {
            Texture = new AtlasTexture { Atlas = GD.Load<Texture2D>(Texture), Region = new Rect2(0, 0, 10, 16) },
            Offset = new Vector2(0, -8),
        };
        AddChild(_sprite);

        // A small post you bump into. It's on the pits layer, so tongues and thrown bombs pass over it.
        var body = new StaticBody2D { CollisionLayer = Pit.PitLayer, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(8, 4) }, Position = new Vector2(0, -2) });
        AddChild(body);
    }

    public string? SaveState() => IsOn ? "On" : null;

    public void LoadState(string state) => TurnOn();

    private void TurnOn()
    {
        IsOn = true;
        ((AtlasTexture)_sprite.Texture).Region = new Rect2(10, 0, 10, 16);
    }

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        if (IsOn)
        {
            return;
        }

        TurnOn();
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -10), "Click!", new Color(1f, 0.75f, 0.35f), 1f, 8f);
    }
}
