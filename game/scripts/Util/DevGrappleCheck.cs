using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for the overworld's secret chest: <c>-- --autoplay-grapple=&lt;folder&gt;</c>. P1 joins as the peacock
/// (Grappler), stands south of the water, and the check makes sure that: walking can't get across, the grapple pulls
/// it over to the chest (which opens), a zip interrupted over the water puts it back where it started, and the
/// grapple brings it back to the near shore. Logs each result and saves screenshots into the folder.
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

    public static DevGrappleCheck Create(string folder) => new() { _folder = folder, ProcessPhysicsPriority = -20 };

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var wanted = new HashSet<Key>();
        var hero = PlayerManager.Find(this)!.Players[0];
        var room = Dungeon.Find(this)!.CurrentRoom;
        var since = _time - _stepStart;
        Vector2 At(float x, float y) => room.GlobalPosition + new Vector2(x, y);
        var chest = room.GetNode<TreasureChest>("SecretChest");

        switch (_step)
        {
            case 0:
                // Join with J, step right to the peacock (4th), pick it.
                if (Press(0.2) || Press(1.3)) wanted.Add(Key.J);
                if (Press(0.5) || Press(0.7) || Press(0.9)) wanted.Add(Key.D);
                if (hero is not null && since > 1.6)
                {
                    Log($"joined as {hero.DisplayName} ({hero.CurrentClass})");
                    hero.Teleport(At(56, 112));
                    hero.Face(Vector2.Up);
                    Next();
                }

                break;

            case 1:
                // Hold it there until the camera catches up (it keeps heroes on screen), then walk north into
                // the water, which mustn't work.
                if (since < 0.6)
                {
                    hero!.Teleport(At(56, 112));
                    break;
                }

                wanted.Add(Key.W);
                if (since > 1.6)
                {
                    Log($"walking north: y {At(0, 112).Y - room.GlobalPosition.Y:0} -> {hero!.GlobalPosition.Y - room.GlobalPosition.Y:0} (water ends at y=88), chest opened={chest.IsOpened}");
                    Next();
                }

                break;

            case 2:
                if (since > 0.3)
                {
                    _from = hero!.GlobalPosition;
                    hero.Face(Vector2.Up);
                    Log($"grapple north: started={hero.GetNode<Ability>("Grapple").TryUse()}");
                    Shot(0.25, "_flying");
                    Next();
                }

                break;

            case 3:
                if (since > 1.2)
                {
                    var p = hero!.GlobalPosition - room.GlobalPosition;
                    Log($"after the grapple: at {p:0} (nook is y < 56), chest opened={chest.IsOpened}, over water={hero.OverPit(hero.GlobalPosition)}");
                    Shot(0, "_nook");
                    Next();
                }

                break;

            case 4:
                // Start back south, and get hit halfway over the water: should land back where it started.
                if (since > 0.3)
                {
                    _from = hero!.GlobalPosition;
                    hero.Face(Vector2.Down);
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
                    hero!.Face(Vector2.Down);
                    Log($"grapple south: started={hero.GetNode<Ability>("Grapple").TryUse()}");
                    Next();
                }

                break;

            case 7:
                if (since > 1.2)
                {
                    var p = hero!.GlobalPosition - room.GlobalPosition;
                    Log($"after the grapple back: at {p:0} (near shore is y > 88), over water={hero.OverPit(hero.GlobalPosition)}");
                    Shot(0, "_back");
                    Next();
                }

                break;

            case 8:
                // The hedge: walking round the east side mustn't get in either.
                if (since < 0.1)
                {
                    hero!.Teleport(At(124, 40));
                }

                wanted.Add(Key.A);
                if (since > 1.2)
                {
                    Log($"walking west into the hedge: x 124 -> {hero!.GlobalPosition.X - room.GlobalPosition.X:0} (hedge at x=104)");
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
