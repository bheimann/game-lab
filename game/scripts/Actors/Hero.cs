using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Combat;
using LizardWizard.Dungeons;
using LizardWizard.Effects;
using LizardWizard.Items;
using LizardWizard.Players;
using LizardWizard.UI;
using LizardWizard.Util;

namespace LizardWizard.Actors;

/// <summary>
/// A player character (lizard, fox, ...): walking, health, getting knocked out and back up,
/// the player marker, and directional animations. What it can *do* comes from its child
/// <see cref="Ability"/> nodes, so each character is a scene: Hero + abilities.
/// Each hero has its own ring menu (gear, bombs, colours, items) that doesn't pause the game.
/// </summary>
public partial class Hero : CharacterBody2D, IHittable
{
    public const string Group = "players";

    private const double WalkFps = 8;
    private const string HerbIcon = "res://assets/sprites/icons/herb.png";

    private Ability[] _abilities = System.Array.Empty<Ability>();
    private bool _down;
    private bool _left;
    private float _downTime;
    private float _invulnerable;
    private Vector2 _knockback;
    private Vector2 _spawnPoint;
    private Vector2 _spriteOffset;
    private RingMenu? _menu;

    [Export] public string DisplayName { get; set; } = "Hero";

    /// <summary>Sprite strips are "&lt;prefix&gt;_down.png", "_up.png" and "_side.png" (side faces right).
    /// Each strip is: idle, step A, step B.</summary>
    [Export] public string SpritePrefix { get; set; } = "res://assets/sprites/chameleon/chameleon";

    [Export] public int FrameWidth { get; set; } = 16;

    [Export] public int SideFrameWidth { get; set; } = 20;

    /// <summary>Walking speed in pixels per second.</summary>
    [Export] public float Speed { get; set; } = 70f;

    [Export] public int MaxHealth { get; set; } = 20;

    /// <summary>Healing herbs the hero starts with (B / I / M eats one).</summary>
    [Export] public int StartingHerbs { get; set; } = 3;

    [Export] public int HerbHealing { get; set; } = 6;

    /// <summary>Walking speed while the menu is open and an enemy is after you (1 = no slowdown).</summary>
    [Export] public float MenuFightSpeedFactor { get; set; } = 0.5f;

    /// <summary>How long the hero can't be hurt again after being hit.</summary>
    [Export] public float InvulnerableSeconds { get; set; } = 1f;

    /// <summary>How long a knocked-out hero waits before getting back up.</summary>
    [Export] public float RespawnSeconds { get; set; } = 2f;

    /// <summary>Where attacks come from (mouth, paws) for each view, relative to the feet.</summary>
    [Export] public Vector2 AttackOriginDown { get; set; } = new(0, -17);

    [Export] public Vector2 AttackOriginUp { get; set; } = new(0, -19);

    /// <summary>Side view, facing right (mirrored automatically for left).</summary>
    [Export] public Vector2 AttackOriginSide { get; set; } = new(6, -18);

    public Health Health { get; private set; } = null!;

    /// <summary>0-based player number (P1 = 0). Decides the colour of the ring and arrow.</summary>
    public int PlayerIndex { get; private set; }

    /// <summary>The device controlling this hero. Updated by <see cref="PlayerManager"/> each frame.</summary>
    public PlayerInput Controls { get; private set; } = new(InputDevice.KeyboardLeft);

    public Color PlayerColour => PlayerManager.Colours[PlayerIndex % PlayerManager.Colours.Length];

    public AnimatedSprite2D Sprite { get; private set; } = null!;

    public IReadOnlyList<Ability> Abilities => _abilities;

    /// <summary>The direction the hero last moved in (one of 8 directions).</summary>
    public Vector2 Facing { get; private set; } = Vector2.Down;

    /// <summary>Which drawing is showing: "down", "up" or "side". Diagonals use the side view.</summary>
    public string View =>
        Mathf.Abs(Facing.Y) > Mathf.Abs(Facing.X) + 0.01f ? (Facing.Y > 0 ? "down" : "up") : "side";

    /// <summary>Seconds since the hero last moved, attacked or got hit (camouflage uses this).</summary>
    public float StillSeconds { get; private set; }

    public bool IsDown => _down;

    /// <summary>Counts attacks and hits taken; camouflage watches it to know when to break.</summary>
    public int ActionCount { get; private set; }

    /// <summary>What's in the armor slot (null = nothing).</summary>
    public Armor? Armor { get; private set; }

    public int Herbs { get; private set; }

    public bool IsMenuOpen => _menu?.IsOpen == true;

    /// <summary>What LB / RB cycle through: the first ability with choices (bombs, colours).</summary>
    public IHasOptions? QuickOptions => _abilities.OfType<IHasOptions>().FirstOrDefault();

    /// <summary>False once the player has left (the hero is about to be removed).</summary>
    public bool IsInGame => !_left && IsInsideTree();

    public bool CanStartAbility => !IsDown && !_abilities.Any(a => a.IsActive);

    /// <summary>Whether enemies can see this hero (camouflage and smoke hide it).</summary>
    public bool IsVisibleToEnemies =>
        IsInGame && !IsDown && !_abilities.Any(a => a.HidesHero) && !SmokeCloud.Hides(GetTree(), GlobalPosition);

    /// <summary>"In a fight": an enemy can see this hero and is targeting it.</summary>
    public bool IsInFight => GetTree().GetNodesInGroup(Enemies.Group).OfType<IEnemy>().Any(e => e.IsTargeting(this));

    public bool CanBeHit => !IsDown && _invulnerable <= 0;

    public Vector2 HurtCenter => GlobalPosition + new Vector2(0, -12);

    public float HurtRadius => 7f;

    /// <summary>Where attacks start, in this hero's local coordinates.</summary>
    public Vector2 AttackOrigin => View switch
    {
        "down" => AttackOriginDown,
        "up" => AttackOriginUp,
        _ => AttackOriginSide with { X = Facing.X < 0 ? -AttackOriginSide.X : AttackOriginSide.X },
    };

    /// <summary>Call before adding the hero to the scene.</summary>
    public void Setup(int playerIndex, PlayerInput input, Vector2 spawnPoint)
    {
        PlayerIndex = playerIndex;
        Controls = input;
        _spawnPoint = spawnPoint;
        Position = spawnPoint;
    }

    /// <summary>The player dropped out: stop being a target right away, then remove the hero.</summary>
    public void LeaveGame()
    {
        _left = true;
        RemoveFromGroup(Group);
        QueueFree();
    }

    public override void _Ready()
    {
        AddToGroup(Group);
        Health = new Health(MaxHealth);
        MotionMode = MotionModeEnum.Floating;
        CollisionMask |= Pit.PitLayer; // can't walk over pits
        _abilities = GetChildren().OfType<Ability>().ToArray();
        Herbs = StartingHerbs;
        _menu = RingMenu.Create(this, BuildMenu);

        Sprite = new AnimatedSprite2D { SpriteFrames = BuildAnimations() };
        Sprite.Offset = _spriteOffset = new Vector2(0, -Sprite.SpriteFrames.GetFrameTexture("idle_down", 0).GetHeight() / 2f);
        AddChild(Sprite);
        MoveChild(Sprite, 0); // abilities draw on top of the hero
        Sprite.Play("idle_down");

        // Small box at the feet, so the hero can walk right up to walls and overlap things above it.
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(10, 6) }, Position = new Vector2(0, -3) });
    }

    public override void _ExitTree()
    {
        _menu?.QueueFree();
        _menu = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _invulnerable -= dt;

        if (_down)
        {
            _menu?.Close();
            _downTime += dt;
            if (_downTime >= RespawnSeconds)
            {
                GetBackUp();
            }

            Velocity = _knockback;
            _knockback = _knockback.MoveToward(Vector2.Zero, 600f * dt);
            MoveAndSlide();
            QueueRedraw();
            return;
        }

        if (Controls.MenuPressed && _menu is not null)
        {
            if (_menu.IsOpen) _menu.Close(); else _menu.Open();
        }
        else if (_menu?.IsOpen == true)
        {
            _menu.HandleInput(Controls);
        }
        else
        {
            UseButtons();
        }

        var active = _abilities.FirstOrDefault(a => a.IsActive);
        var moveFactor = active?.MoveFactor ?? 1f;
        if (IsMenuOpen && IsInFight)
        {
            moveFactor *= MenuFightSpeedFactor;
        }

        var move = Controls.Move * moveFactor;

        if (Controls.Move.Length() > 0.2f && active is null)
        {
            // Snap to 8 directions.
            Facing = Vector2.Right.Rotated(Mathf.Snapped(Controls.Move.Angle(), Mathf.Pi / 4));
        }

        var walking = move.Length() > 0.2f;
        StillSeconds = walking || active is not null ? 0 : StillSeconds + dt;

        Velocity = move * Speed + _knockback;
        _knockback = _knockback.MoveToward(Vector2.Zero, 600f * dt);
        MoveAndSlide();
        StayOnScreen();
        UpdateLook(walking);
    }

    /// <summary>Put on a piece of armor (or take it off with null).</summary>
    public void Equip(Armor? armor)
    {
        Armor = armor;
        Health.SetMax(MaxHealth + (armor?.ExtraHealth ?? 0));
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -14), armor?.Name ?? "No armor", Colors.White, 1f, 8f);
    }

    public void AddHerbs(int count)
    {
        Herbs += count;
    }

    /// <summary>Come into a new room (through a door) at <paramref name="position"/>, facing <paramref name="facing"/>.</summary>
    public void ArriveAt(Vector2 position, Vector2 facing)
    {
        GlobalPosition = position;
        _spawnPoint = position;
        _knockback = Vector2.Zero;
        Facing = facing;
        ResetPhysicsInterpolation();
    }

    /// <summary>Is there nothing solid (walls, rocks) between these two points? Pits don't count.</summary>
    public bool ClearPath(Vector2 from, Vector2 to)
    {
        var query = PhysicsRayQueryParameters2D.Create(from, to, collisionMask: 1);
        return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    /// <summary>Eat a healing herb, if there's one left and it would help.</summary>
    public void UseHerb()
    {
        if (Herbs <= 0)
        {
            FloatingText.Spawn(this, HurtCenter + new Vector2(0, -14), "No herbs!", new Color(0.8f, 0.8f, 0.8f), 0.8f, 6f);
            return;
        }

        if (Health.Current >= Health.Max)
        {
            FloatingText.Spawn(this, HurtCenter + new Vector2(0, -14), "Already full", new Color(0.8f, 0.8f, 0.8f), 0.8f, 6f);
            return;
        }

        Herbs--;
        var healed = Health.Heal(HerbHealing);
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -10), $"+{healed}", new Color(0.45f, 1f, 0.45f));
    }

    private void UseButtons()
    {
        foreach (var ability in _abilities)
        {
            var pressed = ability.Slot switch
            {
                AbilitySlot.Primary => Controls.PrimaryPressed,
                AbilitySlot.Secondary => Controls.SecondaryPressed,
                AbilitySlot.Special => Controls.SpecialPressed,
                _ => false,
            };
            if (pressed)
            {
                ability.TryUse();
            }
        }

        // Quick buttons, so the menu is rarely needed mid-fight.
        if (QuickOptions is { } quick && (Controls.NextPressed || Controls.PreviousPressed))
        {
            quick.Select(quick.Selected + (Controls.NextPressed ? 1 : -1));
        }

        if (Controls.ItemPressed)
        {
            UseHerb();
        }
    }

    private List<MenuRing> BuildMenu()
    {
        var rings = _abilities.OfType<IHasOptions>()
            .Select(a => new MenuRing(a.OptionsTitle, a.Options, a.Selected, a.Select))
            .ToList();

        var armor = Items.Armor.All.Where(a => a.Fits(this)).ToList();
        var armorOptions = new[] { new MenuOption("No armor", "Take your armor off", null, Colors.Transparent) }
            .Concat(armor.Select(a => a.ToOption())).ToArray();
        rings.Add(new MenuRing("Armor", armorOptions, Armor is null ? 0 : armor.IndexOf(Armor) + 1, i => Equip(i == 0 ? null : armor[i - 1])));

        var herb = new MenuOption("Healing Herb", $"Heals {HerbHealing / 2} hearts", GD.Load<Texture2D>(HerbIcon), Colors.White)
        {
            Count = $"x{Herbs}",
            Enabled = Herbs > 0,
        };
        rings.Add(new MenuRing("Items", new[] { herb }, 0, _ => UseHerb()));
        return rings;
    }

    public override void _Draw()
    {
        // Player marker: a coloured ring at the feet and a small arrow over the head.
        if (!IsDown)
        {
            DrawSetTransform(Vector2.Zero, 0, new Vector2(1f, 0.45f));
            DrawArc(Vector2.Zero, 8f, 0, Mathf.Tau, 20, PlayerColour, 1.5f);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            var top = _spriteOffset.Y * 2 - 5;
            DrawColoredPolygon(new[] { new Vector2(-3, top), new Vector2(3, top), new Vector2(0, top + 3) }, PlayerColour);
        }
    }

    public void TakeHit(int damage, Vector2 from, DamageType type = DamageType.Physical)
    {
        if (!CanBeHit)
        {
            return;
        }

        var multiplier = _abilities.Aggregate(1f, (m, a) => m * a.DamageMultiplier(type));
        var dealt = Health.Damage(Mathf.Max(1, Mathf.RoundToInt(damage * multiplier)));
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -8), $"-{dealt}", new Color(1f, 0.35f, 0.35f));
        _knockback = (GlobalPosition - from).Normalized() * 140f;
        _invulnerable = InvulnerableSeconds;
        NotifyAction();
        foreach (var ability in _abilities)
        {
            ability.Cancel();
        }

        if (Health.IsEmpty)
        {
            GoDown();
        }
    }

    /// <summary>Abilities call this when used: breaks camouflage and similar "stand still" effects.</summary>
    public void NotifyAction()
    {
        StillSeconds = 0;
        ActionCount++;
    }

    /// <summary>Quick shove of the sprite (punch lunge), relative to its normal position.</summary>
    public void Lunge(Vector2 offset)
    {
        var tween = CreateTween();
        tween.TweenProperty(Sprite, "offset", _spriteOffset + offset, 0.05f);
        tween.TweenProperty(Sprite, "offset", _spriteOffset, 0.12f);
    }

    /// <summary>Things this hero's attacks can hit right now: enemies, plus levers and the like.</summary>
    public IHittable[] EnemyTargets() =>
        GetTree().GetNodesInGroup(Enemies.Group).Concat(GetTree().GetNodesInGroup(LeverSwitch.Group))
            .OfType<IHittable>().Where(t => t.CanBeHit).ToArray();

    private void GoDown()
    {
        _down = true;
        _downTime = 0;
        Sprite.RotationDegrees = 90;
        Sprite.Modulate = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        FloatingText.Spawn(this, HurtCenter + new Vector2(0, -12), "Ouch!", Colors.White, 1.2f);
    }

    private void GetBackUp()
    {
        // Get back up next to a teammate who's still standing, or where the team came into the room.
        var buddy = GetTree().GetNodesInGroup(Group).OfType<Hero>().FirstOrDefault(h => h != this && !h.IsDown);
        GlobalPosition = buddy?.GlobalPosition ?? Dungeon.Find(this)?.RespawnPoint ?? _spawnPoint;
        Health.Refill();
        _down = false;
        _knockback = Vector2.Zero;
        Sprite.RotationDegrees = 0;
        Sprite.Modulate = Colors.White;
        _invulnerable = 1.5f;
    }

    private void StayOnScreen()
    {
        if (CoopCamera.Find(this) is { } camera)
        {
            var allowed = camera.AllowedRect;
            GlobalPosition = GlobalPosition.Clamp(allowed.Position, allowed.End);
        }
    }

    private void UpdateLook(bool walking)
    {
        var view = View;
        var animation = $"{(walking ? "walk" : "idle")}_{view}";
        if (Sprite.Animation != animation)
        {
            Sprite.Play(animation);
        }

        Sprite.FlipH = view == "side" && Facing.X < 0;

        // Flicker (faint / solid) while invulnerable, but never disappear completely; abilities can fade the hero too.
        var faint = _invulnerable > 0 && (int)(_invulnerable * 15) % 2 == 1;
        var alpha = (faint ? 0.35f : 1f) * _abilities.Aggregate(1f, (a, ability) => a * ability.SpriteAlpha);
        Sprite.Modulate = Colors.White with { A = alpha };
        QueueRedraw();
    }

    private SpriteFrames BuildAnimations()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        foreach (var (view, width) in new[] { ("down", FrameWidth), ("up", FrameWidth), ("side", SideFrameWidth) })
        {
            var strip = GD.Load<Texture2D>($"{SpritePrefix}_{view}.png");
            SpriteSheet.AddFrames(frames, $"idle_{view}", strip, width, 1, new[] { 0 });
            SpriteSheet.AddFrames(frames, $"walk_{view}", strip, width, WalkFps, new[] { 1, 0, 2, 0 });
        }

        return frames;
    }
}
