using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;

namespace LizardWizard.Players;

/// <summary>A character you can pick when joining.</summary>
public sealed record CharacterOption(string Name, string ScenePath);

/// <summary>One of the four player slots.</summary>
public sealed class PlayerSlot
{
    /// <summary>The device that claimed this slot, or null if it's free.</summary>
    public PlayerInput? Input { get; internal set; }

    /// <summary>The spawned character, or null while picking (or free).</summary>
    public Hero? Hero { get; internal set; }

    /// <summary>Index into <see cref="PlayerManager.Characters"/> of the character being picked / played.</summary>
    public int Choice { get; internal set; }

    public bool IsFree => Input is null;

    public bool IsChoosing => Input is not null && Hero is null;
}

/// <summary>
/// Drop-in / drop-out co-op for up to 4 players. Every device (each half of the keyboard and each
/// controller) is watched. Pressing a button on a free device claims a slot; left / right picks a
/// character and the primary button confirms. Holding the leave button for a second frees the slot.
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
                if (input.NavigateX != 0)
                {
                    slot.Choice = (slot.Choice + input.NavigateX + Characters.Length) % Characters.Length;
                    PlayersChanged?.Invoke();
                }
                else if (input.PrimaryPressed)
                {
                    Spawn(Array.IndexOf(_slots, slot));
                }
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
        GD.Print($"P{index + 1} left");
        PlayersChanged?.Invoke();
    }

    private void Spawn(int index)
    {
        var slot = _slots[index];
        var character = Characters[slot.Choice];
        var hero = GD.Load<PackedScene>(character.ScenePath).Instantiate<Hero>();
        hero.Name = $"P{index + 1}";
        hero.Setup(index, slot.Input!, SpawnPoint(index));
        (ActorsParent ?? GetParent<Node2D>()).AddChild(hero);
        slot.Hero = hero;
        Restore(hero, index);
        GD.Print($"P{index + 1} is playing as the {character.Name}");
        PlayersChanged?.Invoke();
    }

    /// <summary>Same character as last time? Then the same class, weapon, armor and herbs.</summary>
    private static void Restore(Hero hero, int slot)
    {
        if (Saving.SaveGame.PlayerFor(slot, hero.DisplayName) is not { } saved)
        {
            return;
        }

        if (saved.Class != hero.CurrentClass && hero.ClassNames.Contains(saved.Class))
        {
            hero.Paint(saved.Class);
        }

        if (saved.Weapon != "" && saved.Weapon != hero.CurrentWeapon)
        {
            hero.SetWeapon(saved.Weapon);
        }

        if (Items.Armor.All.FirstOrDefault(a => a.Name == saved.Armor && a.Fits(hero)) is { } armor)
        {
            hero.Equip(armor);
        }

        hero.RestoreHerbs(saved.Herbs);
        GD.Print($"P{slot + 1} picks up where they left off: {hero.DisplayName} {hero.CurrentClass} {hero.CurrentWeapon}, armor={hero.Armor?.Name ?? "none"}, herbs={hero.Herbs}");
    }

    private Vector2 SpawnPoint(int index)
    {
        // Join next to a teammate who's already playing, otherwise at this slot's start point.
        var buddy = _slots.Select(s => s.Hero).FirstOrDefault(h => h is not null && !h.IsDown);
        if (buddy is not null)
        {
            return buddy.GlobalPosition;
        }

        if (Dungeon.Find(this) is { } dungeon)
        {
            return dungeon.RespawnPoint + new Vector2(index * 12 - 18, 0);
        }

        var spawns = GetNodeOrNull("Spawns")?.GetChildren().OfType<Node2D>().ToArray() ?? Array.Empty<Node2D>();
        return spawns.Length == 0 ? Vector2.Zero : spawns[index % spawns.Length].GlobalPosition;
    }

    private static IEnumerable<InputDevice> CurrentDevices()
    {
        yield return InputDevice.KeyboardLeft;
        yield return InputDevice.KeyboardRight;
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
