using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for the grapple-only secret chests: <c>-- --room=Overworld --autoplay-grapple=&lt;folder&gt;</c> checks the
/// meadow's (<c>--room=Swamp</c> for Tailspin Swamp's). P1 joins as the peacock (Grappler), stands on the near shore, and the check
/// makes sure that: walking can't get across, the grapple pulls it over to the chest (which opens), a zip interrupted
/// over the water puts it back where it started, the grapple brings it back to the near shore, and walking round the
/// side (the hedge or rock wall) can't get in. Logs each result and saves screenshots into the folder.
/// </summary>
public partial class DevGrappleCheck : Node
{
    public const string Flag = "--autoplay-grapple=";

    private readonly HashSet<Key> _held = new();
    private string _folder = "";
    private double _time;
    private int _step;
    private double _stepStart;
    private Vector2 _from;
    private int _shotNumber;

    /// <summary>Where each secret is: the chest, a spot on the near shore, which way is across the water, and a spot
    /// beside the hedge / rock wall plus which way walks into it.</summary>
    private static readonly Dictionary<string, (string Chest, Vector2 Shore, Vector2 Across, Vector2 Side, Vector2 Into)> Secrets = new()
    {
        ["Overworld"] = ("SecretChest", new Vector2(56, 112), Vector2.Up, new Vector2(124, 40), Vector2.Left),
        ["Swamp"] = ("SecretChest1", new Vector2(840, 380), Vector2.Down, new Vector2(730, 470), Vector2.Right),
    };

    public static DevGrappleCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var wanted = new HashSet<Key>();
        var hero = PlayerManager.Find(this)!.Players[0];
        var room = Dungeon.Find(this)!.CurrentRoom;
        var since = _time - _stepStart;
        if (!Secrets.TryGetValue(room.Name, out var secret))
        {
            // The game starts in the village: say where to start the check, and stop.
            GD.Print($"[grapple] no grapple secret in {room.Name}: add --room=Overworld (the meadow) or --room=Swamp");
            SetPhysicsProcess(false);
            GetTree().Quit();
            return;
        }

        Vector2 At(Vector2 p) => room.GlobalPosition + p;
        var chest = room.GetNode<TreasureChest>(secret.Chest);

        switch (_step)
        {
            case 0:
                // Join with J, step right to the peacock (4th), pick it.
                if (Press(0.2) || Press(1.3)) wanted.Add(Key.J);
                if (Press(0.5) || Press(0.7) || Press(0.9)) wanted.Add(Key.D);
                if (hero is not null && since > 1.6)
                {
                    Log($"joined as {hero.DisplayName} ({hero.CurrentClass})");
                    hero.Teleport(At(secret.Shore));
                    hero.Face(secret.Across);
                    Next();
                }

                break;

            case 1:
                // Hold it there until the camera catches up (it keeps heroes on screen), then walk into the water,
                // which mustn't work.
                if (since < 0.6)
                {
                    hero!.Teleport(At(secret.Shore));
                    break;
                }

                Push(secret.Across, wanted);
                if (since > 1.6)
                {
                    Log($"walking into the water: {secret.Shore:0} -> {hero!.GlobalPosition - room.GlobalPosition:0}, chest opened={chest.IsOpened}");
                    Next();
                }

                break;

            case 2:
                if (since > 0.3)
                {
                    _from = hero!.GlobalPosition;
                    hero.Face(secret.Across);
                    Log($"grapple across: started={hero.GetNode<Ability>("Grapple").TryUse()}");
                    Shot(0.25, "_flying");
                    Next();
                }

                break;

            case 3:
                if (since > 1.2)
                {
                    var p = hero!.GlobalPosition - room.GlobalPosition;
                    Log($"after the grapple: at {p:0}, chest opened={chest.IsOpened}, over water={hero.OverPit(hero.GlobalPosition)}");
                    Shot(0, "_nook");
                    Next();
                }

                break;

            case 4:
                // Start back, and get hit halfway over the water: should land back where it started.
                if (since > 0.3)
                {
                    _from = hero!.GlobalPosition;
                    hero.Face(-secret.Across);
                    hero.GetNode<Ability>("Grapple").TryUse();
                    Next();
                }

                break;

            case 5:
                if (hero!.OverPit(hero.GlobalPosition))
                {
                    hero.GetNode<Ability>("Grapple").Cancel();
                    Log($"interrupted over the water: back at {hero.GlobalPosition - room.GlobalPosition:0} (started at {_from - room.GlobalPosition:0})");
                    Next();
                }
                else if (since > 2)
                {
                    Log("interrupted over the water: never got over the water!");
                    Next();
                }

                break;

            case 6:
                if (since > 0.8)
                {
                    hero!.Face(-secret.Across);
                    Log($"grapple back: started={hero.GetNode<Ability>("Grapple").TryUse()}");
                    Next();
                }

                break;

            case 7:
                if (since > 1.2)
                {
                    var p = hero!.GlobalPosition - room.GlobalPosition;
                    Log($"after the grapple back: at {p:0} (the shore spot was {secret.Shore:0}), over water={hero.OverPit(hero.GlobalPosition)}");
                    Shot(0, "_back");
                    Next();
                }

                break;

            case 8:
                // The hedge / rock wall: walking round the side mustn't get in either.
                if (since < 0.6)
                {
                    hero!.Teleport(At(secret.Side));
                    break;
                }

                Push(secret.Into, wanted);
                if (since > 1.8)
                {
                    Log($"walking into the side wall: {secret.Side:0} -> {hero!.GlobalPosition - room.GlobalPosition:0}");
                    Next();
                }

                break;

            case 9:
                Log("done");
                GetTree().CreateTimer(0.3).Timeout += () => GetTree().Quit();
                SetPhysicsProcess(false);
                break;
        }

        foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D, Key.J })
        {
            SetKey(key, wanted.Contains(key));
        }

        bool Press(double at) => since >= at && since < at + 0.1;
    }

    private static void Push(Vector2 direction, HashSet<Key> wanted)
    {
        if (direction.X > 0.5f) wanted.Add(Key.D);
        if (direction.X < -0.5f) wanted.Add(Key.A);
        if (direction.Y > 0.5f) wanted.Add(Key.S);
        if (direction.Y < -0.5f) wanted.Add(Key.W);
    }

    private void Next()
    {
        _step++;
        _stepStart = _time;
    }

    private void Log(string text) => GD.Print($"[grapple {_time:0.0}s] {text}");

    private void SetKey(Key key, bool down)
    {
        if (down == _held.Contains(key))
        {
            return;
        }

        if (down) _held.Add(key); else _held.Remove(key);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down });
    }

    private async void Shot(double after, string name)
    {
        var path = $"{_folder}/grapple_{_shotNumber++:00}{name}.png";
        if (after > 0)
        {
            await ToSignal(GetTree().CreateTimer(after), SceneTreeTimer.SignalName.Timeout);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
}
