using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;

namespace LizardWizard.Players;

/// <summary>A character you can pick when joining.</summary>
public sealed record CharacterOption(string Name, string ScenePath)
{
    private string[]? _classes;

    /// <summary>The classes this character can be (starting class first), read from its scene once.</summary>
    public IReadOnlyList<string> Classes
    {
        get
        {
            if (_classes is null)
            {
                var hero = GD.Load<PackedScene>(ScenePath).Instantiate<Hero>();
                _classes = Hero.ClassNamesOf(hero.StartingClass, hero.GetChildren().OfType<Abilities.Ability>()).ToArray();
                hero.Free();
            }

            return _classes;
        }
    }
}

/// <summary>One of the four player slots.</summary>
public sealed class PlayerSlot
{
    /// <summary>The device that claimed this slot, or null if it's free.</summary>
    public PlayerInput? Input { get; internal set; }

    /// <summary>The spawned character, or null while picking (or free).</summary>
    public Hero? Hero { get; internal set; }

    /// <summary>Index into <see cref="PlayerManager.Characters"/> of the character being picked / played.</summary>
    public int Choice { get; internal set; }

    /// <summary>Index into the picked character's <see cref="CharacterOption.Classes"/>.</summary>
    public int ClassChoice { get; internal set; }

    public CharacterOption Character => PlayerManager.Characters[Choice];

    public string Class => Character.Classes[ClassChoice];

    public bool IsFree => Input is null;

    public bool IsChoosing => Input is not null && Hero is null;

    /// <summary>Picked a character during a boss fight: joins as soon as the fight's over.</summary>
    public bool IsWaitingForBossFight { get; internal set; }
}

/// <summary>
/// Drop-in / drop-out co-op for up to 4 players. Every device (each half of the keyboard and each
/// controller) is watched. Pressing a button on a free device claims a slot; left / right picks a
/// character, up / down picks its class and the primary button confirms. Holding the leave button for a second frees the slot.
/// Nobody joins in the middle of a boss fight (it's on one shared screen): a player who confirms then waits, and joins
/// as soon as the fight's over.
/// </summary>
/// <remarks>
/// Needs a child node "Spawns" with up to 4 Marker2D children (P1..P4 start points).
/// Runs before the heroes each physics frame so their input is fresh.
/// </remarks>
public partial class PlayerManager : Node
{
    public const int MaxPlayers = 4;
    private const double LeaveAfterSeconds = 1.0;

    /// <summary>Player colours: blue, red, yellow, pink (from the Tiny Dungeon palette).</summary>
    public static readonly Color[] Colours =
    {
        new(0f, 0.58f, 0.91f),
        new(0.9f, 0.27f, 0.22f),
        new(0.94f, 0.71f, 0.25f),
        new(1f, 0.32f, 0.47f),
    };

    public static readonly CharacterOption[] Characters =
    {
        new("Lizard", "res://scenes/actors/lizard.tscn"),
        new("Fox", "res://scenes/actors/fox.tscn"),
        new("Moose", "res://scenes/actors/moose.tscn"),
        new("Peacock", "res://scenes/actors/peacock.tscn"),
        new("Beetle", "res://scenes/actors/beetle.tscn"),
    };

    private readonly Dictionary<InputDevice, PlayerInput> _devices = new();
    private readonly PlayerSlot[] _slots = Enumerable.Range(0, MaxPlayers).Select(_ => new PlayerSlot()).ToArray();

    /// <summary>Raised when someone joins, picks a character or leaves.</summary>
    public event Action? PlayersChanged;

    /// <summary>Where new heroes are added (should be the y-sorted actors node).</summary>
    [Export] public Node2D? ActorsParent { get; set; }

    public IReadOnlyList<PlayerSlot> Slots => _slots;

    /// <summary>The spawned heroes by slot (null for free or still-picking slots).</summary>
    public IReadOnlyList<Hero?> Players => _slots.Select(s => s.Hero).ToArray();

    public static PlayerManager? Find(Node anyNode) =>
        anyNode.GetTree().CurrentScene?.FindChild(nameof(PlayerManager), true, false) as PlayerManager;

    public override void _Ready()
    {
        Name = nameof(PlayerManager);
        ProcessPhysicsPriority = -10;
        Input.JoyConnectionChanged += OnJoyConnectionChanged;
    }

    public override void _ExitTree()
    {
        Input.JoyConnectionChanged -= OnJoyConnectionChanged;
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (var device in CurrentDevices())
        {
            if (!_devices.TryGetValue(device, out var input))
            {
                input = new PlayerInput(device);
                _devices[device] = input;
            }

            input.Update(delta);

            var slot = _slots.FirstOrDefault(s => s.Input == input);
            if (slot is null)
            {
                if (input.JoinPressed)
                {
                    Claim(input);
                }
            }
            else if (input.LeaveHeldSeconds >= LeaveAfterSeconds)
            {
                Leave(Array.IndexOf(_slots, slot));
            }
            else if (slot.IsChoosing)
            {
                var index = Array.IndexOf(_slots, slot);
                if (input.NavigateX != 0)
                {
                    slot.Choice = (slot.Choice + input.NavigateX + Characters.Length) % Characters.Length;
                    slot.ClassChoice = SavedClassChoice(index, slot.Character);
                    PlayersChanged?.Invoke();
                }
                else if (input.NavigateY != 0)
                {
                    var classes = slot.Character.Classes.Count;
                    slot.ClassChoice = (slot.ClassChoice + input.NavigateY + classes) % classes;
                    PlayersChanged?.Invoke();
                }
                else if (input.PrimaryPressed && BossFightOn)
                {
                    slot.IsWaitingForBossFight = true;
                    PlayersChanged?.Invoke();
                }
                else if (input.PrimaryPressed)
                {
                    Spawn(index);
                }
            }
        }

        // Picked a character during a boss fight: in as soon as it's over.
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] is { IsChoosing: true, IsWaitingForBossFight: true } && !BossFightOn)
            {
                Spawn(i);
            }
        }
    }

    /// <summary>Give a free slot to this device; the player then picks a character.</summary>
    public PlayerSlot? Claim(PlayerInput input)
    {
        var slot = _slots.FirstOrDefault(s => s.IsFree);
        if (slot is null)
        {
            return null;
        }

        slot.Input = input;
        slot.ClassChoice = SavedClassChoice(Array.IndexOf(_slots, slot), slot.Character);
        GD.Print($"P{Array.IndexOf(_slots, slot) + 1} joined with the {input.Device} and is picking a character");
        PlayersChanged?.Invoke();
        return slot;
    }

    public void Leave(int index)
    {
        Saving.SaveGame.Save(this); // remember what they were playing
        var slot = _slots[index];
        slot.Hero?.LeaveGame();
        slot.Hero = null;
        slot.Input = null;
        slot.IsWaitingForBossFight = false;
        GD.Print($"P{index + 1} left");
        PlayersChanged?.Invoke();
    }

    /// <summary>Dev helpers (screenshots, checks): join with <paramref name="device"/> as character
    /// <paramref name="character"/> in class <paramref name="className"/> straight away. The hero gets that device's
    /// real input, so pressing its keys moves it.</summary>
    internal Hero? JoinForDev(InputDevice device, int character, string className)
    {
        if (!_devices.TryGetValue(device, out var input))
        {
            input = new PlayerInput(device);
            _devices[device] = input;
        }

        if (Claim(input) is not { } slot)
        {
            return null;
        }

        slot.Choice = character;
        slot.ClassChoice = System.Math.Max(0, slot.Character.Classes.ToList().IndexOf(className));
        if (BossFightOn)
        {
            slot.IsWaitingForBossFight = true; // (joins once the fight's over, like a real player)
            return null;
        }

        Spawn(System.Array.IndexOf(_slots, slot));
        return slot.Hero;
    }

    /// <summary>Someone's fighting a boss right now (nobody joins until it's over).</summary>
    public bool BossFightOn => Dungeon.Find(this) is { IsBossFightOn: true };

    private void Spawn(int index)
    {
        var slot = _slots[index];
        slot.IsWaitingForBossFight = false;
        var character = slot.Character;
        var hero = GD.Load<PackedScene>(character.ScenePath).Instantiate<Hero>();
        hero.Name = $"P{index + 1}";
        hero.Setup(index, slot.Input!, SpawnPoint(index), slot.Class);
        (ActorsParent ?? GetParent<Node2D>()).AddChild(hero);
        slot.Hero = hero;
        Restore(hero, index);
        GD.Print($"P{index + 1} is playing as the {character.Name} {hero.CurrentClass}");
        PlayersChanged?.Invoke();
    }

    /// <summary>Which class to offer first: the one this slot played last time with this character, else the starting class.</summary>
    private static int SavedClassChoice(int slot, CharacterOption character) =>
        Saving.SaveGame.PlayerFor(slot, character.Name) is { } saved && character.Classes.Contains(saved.Class)
            ? character.Classes.ToList().IndexOf(saved.Class)
            : 0;

    /// <summary>Same character as last time? Then the same armor, stolen move and fusions (and weapon, if it's the same class).</summary>
    private static void Restore(Hero hero, int slot)
    {
        if (Saving.SaveGame.PlayerFor(slot, hero.DisplayName) is not { } saved)
        {
            return;
        }

        if (saved.Class == hero.CurrentClass && saved.Weapon != "" && saved.Weapon != hero.CurrentWeapon)
        {
            hero.SetWeapon(saved.Weapon);
        }

        if (Items.Armor.All.FirstOrDefault(a => a.Name == saved.Armor && a.Fits(hero)) is { } armor)
        {
            hero.Equip(armor);
        }

        if (saved.StolenMove != "" && hero.GetChildren().OfType<Abilities.StolenMoveAbility>().FirstOrDefault() is { } stolen)
        {
            stolen.Learn(saved.StolenMove);
        }

        hero.RestoreFusions(saved.Fusions.Select(Items.Fusion.Parse).OfType<Items.Fusion>(),
            saved.HeldFusions.Where(h => System.Enum.TryParse<Items.FuseKind>(h.Key, out _))
                .ToDictionary(h => System.Enum.Parse<Items.FuseKind>(h.Key), h => h.Value));

        GD.Print($"P{slot + 1} picks up where they left off: {hero.DisplayName} {hero.CurrentClass} {hero.CurrentWeapon}, armor={hero.Armor?.Name ?? "none"}");
    }

    private Vector2 SpawnPoint(int index)
    {
        // In the world: Scalebrook Village, or with the team (see Dungeon.JoinPoint).
        if (Dungeon.Find(this) is { } dungeon)
        {
            return dungeon.JoinPoint() + new Vector2(index * 12 - 18, 0);
        }

        // A test room: next to a teammate who's already playing, otherwise at this slot's start point.
        var buddy = _slots.Select(s => s.Hero).FirstOrDefault(h => h is not null && !h.IsDown);
        if (buddy is not null)
        {
            return buddy.GlobalPosition;
        }

        var spawns = GetNodeOrNull("Spawns")?.GetChildren().OfType<Node2D>().ToArray() ?? Array.Empty<Node2D>();
        return spawns.Length == 0 ? Vector2.Zero : spawns[index % spawns.Length].GlobalPosition;
    }

    /// <summary>
    /// Dev runs (autoplay bots, checks, screenshots) play with the keyboard and pretend controllers only: Godot reads
    /// real controllers even when its window isn't focused, so someone holding one would join every dev run.
    /// </summary>
    private static readonly bool IgnoreRealControllers =
        OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--autoplay") || a.StartsWith("--screenshot"));

    private static IEnumerable<InputDevice> CurrentDevices()
    {
        yield return InputDevice.KeyboardLeft;
        yield return InputDevice.KeyboardRight;
        if (IgnoreRealControllers)
        {
            yield break;
        }

        foreach (var pad in Input.GetConnectedJoypads())
        {
            yield return InputDevice.Gamepad(pad);
        }
    }

    private void OnJoyConnectionChanged(long device, bool connected)
    {
        if (connected)
        {
            return;
        }

        // A controller was unplugged: its player leaves.
        var index = Array.FindIndex(_slots, s => s.Input?.Device == InputDevice.Gamepad((int)device));
        if (index >= 0)
        {
            Leave(index);
        }

        _devices.Remove(InputDevice.Gamepad((int)device));
    }
}
