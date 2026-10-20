using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;
using LizardWizard.Effects;
using LizardWizard.Players;

namespace LizardWizard.Dungeons;

/// <summary>
/// A dead tree leaning over the edge of a pit or a bog. Only a <see cref="Hero.Strong"/> hero (the moose, whatever its
/// class) can knock it over: it falls across, and a <see cref="Pit"/> with <see cref="Pit.Log"/> set and this tree in
/// its <see cref="Pit.Switches"/> becomes a log to walk over. Other heroes' attacks don't touch it. Stays down.
/// Place it at the foot of the log's column, on the near side; the node's position is where it touches the ground.
/// </summary>
public partial class FallingTree : StaticBody2D, IHittable, IPickyTarget, ISwitch, Saving.ISaveable
{
    private const string Texture = "res://assets/kenney/deadtree.png";
    private const float FallSeconds = 0.45f;

    private Sprite2D _sprite = null!;
    private CollisionShape2D _solid = null!;

    public bool IsOn { get; private set; }

    public bool CanBeHit => !IsOn;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -8);

    public float HurtRadius => 7f;

    public override void _Ready()
    {
        AddToGroup(LeverSwitch.Group);
        CollisionLayer = 1;
        CollisionMask = 0;
        var texture = GD.Load<Texture2D>(Texture);
        _sprite = new Sprite2D { Texture = texture, Offset = new Vector2(0, -texture.GetHeight() / 2f + 1) };
        AddChild(_sprite);
        _solid = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(8, 5) }, Position = new Vector2(0, -2.5f) };
        AddChild(_solid);
    }

    public bool CanBeHitBy(Hero hero) => hero.Strong;

    public string? SaveState() => IsOn ? "Fallen" : null;

    public void LoadState(string state)
    {
        IsOn = true;
        _sprite.Hide();
        _solid.Disabled = true;
    }

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        if (IsOn)
        {
            return;
        }

        // Over it goes, away from whoever hit it (across the pit), and the log slides out over the gap (see Pit).
        IsOn = true;
        _solid.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -12), "Timber!", new Color(1f, 0.85f, 0.5f), 1.4f, 8f);
        CoopCamera.ShakeAll(this, 3f);
        var tween = CreateTween();
        tween.TweenProperty(_sprite, "scale", new Vector2(1.15f, 0.2f), FallSeconds).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tween.Parallel().TweenProperty(_sprite, "modulate:a", 0f, FallSeconds).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(_sprite.Hide));
    }
}
