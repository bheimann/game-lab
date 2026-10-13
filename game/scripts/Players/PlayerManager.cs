using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;

namespace LizardWizard.Players;

/// <summary>
/// Drop-in / drop-out co-op for up to 4 players. Every device (each half of the keyboard and each
/// controller) is watched; pressing a button on a free device spawns a lizard for it, and holding the
/// leave button for a second removes that player.
/// </summary>
/// <remarks>
/// Needs a child node "Spawns" with up to 4 Marker2D children (P1..P4 start points).
/// Runs before the lizards each physics frame so their input is fresh.
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

    private readonly Dictionary<InputDevice, PlayerInput> _devices = new();
    private readonly Lizard?[] _players = new Lizard?[MaxPlayers];

    /// <summary>Raised when someone joins or leaves.</summary>
    public event Action? PlayersChanged;

    [Export] public PackedScene LizardScene { get; set; } = GD.Load<PackedScene>("res://scenes/actors/lizard.tscn");

    /// <summary>Where new lizards are added (should be the y-sorted actors node).</summary>
    [Export] public Node2D? ActorsParent { get; set; }

    public IReadOnlyList<Lizard?> Players => _players;

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

            var slot = Array.FindIndex(_players, p => p is not null && p.Controls == input);
            if (slot < 0 && input.JoinPressed)
            {
                Join(input);
            }
            else if (slot >= 0 && input.LeaveHeldSeconds >= LeaveAfterSeconds)
            {
                Leave(slot);
            }
        }
    }

    public Lizard? Join(PlayerInput input)
    {
        var slot = Array.IndexOf(_players, null);
        if (slot < 0)
        {
            return null;
        }

        var lizard = LizardScene.Instantiate<Lizard>();
        lizard.Name = $"P{slot + 1}";
        lizard.Setup(slot, input, SpawnPoint(slot));
        (ActorsParent ?? GetParent<Node2D>()).AddChild(lizard);
        _players[slot] = lizard;
        GD.Print($"P{slot + 1} joined with the {input.Device}");
        PlayersChanged?.Invoke();
        return lizard;
    }

    public void Leave(int slot)
    {
        var lizard = _players[slot];
        if (lizard is null)
        {
            return;
        }

        _players[slot] = null;
        lizard.QueueFree();
        GD.Print($"P{slot + 1} left");
        PlayersChanged?.Invoke();
    }

    private Vector2 SpawnPoint(int slot)
    {
        var spawns = GetNodeOrNull("Spawns")?.GetChildren().OfType<Node2D>().ToArray() ?? Array.Empty<Node2D>();
        return spawns.Length == 0 ? Vector2.Zero : spawns[slot % spawns.Length].GlobalPosition;
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
        var slot = Array.FindIndex(_players, p => p is not null && p.Controls.Device == InputDevice.Gamepad((int)device));
        if (slot >= 0)
        {
            Leave(slot);
        }

        _devices.Remove(InputDevice.Gamepad((int)device));
    }
}
