using System.Linq;
using Godot;
using LizardWizard.Abilities;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Effects;
using LizardWizard.Players;

namespace LizardWizard.Util;

/// <summary>
/// Dev helper for nice screenshots to share: <c>-- --room=Village --screenshot-showcase=village:path.png</c>. Four heroes
/// join in the room (lizard, fox, moose and peacock or beetle), get set up for the scene, and a moment later the screen
/// is saved and the game quits. Scenes: village, throne, battle, lake, canyon, snowplane (Tundra), boss (start in the
/// matching room). Add <c>--showcase-heroes=2</c> for fewer heroes (to see the split-screen layouts).
/// (Starts with --screenshot, so the real save is never touched.)
/// </summary>
public partial class DevShowcase : Node
{
    public const string Flag = "--screenshot-showcase=";

    private string _scene = "";
    private string _path = "";
    private double _time;
    private int _stage;
    private Vehicle? _firing;

    public static DevShowcase Create(string arg)
    {
        var parts = arg.Split(':', 2);
        return new DevShowcase { _scene = parts[0], _path = parts.Length > 1 ? parts[1] : "showcase.png", ProcessPhysicsPriority = -20 };
    }

    public override void _PhysicsProcess(double delta)
    {
        _time += delta;
        var manager = PlayerManager.Find(this)!;
        var room = Dungeon.Find(this)!.CurrentRoom;
        Vector2 At(float x, float y) => room.GlobalPosition + new Vector2(x, y);

        if (_stage == 0 && _time > 0.2)
        {
            _stage = 1;
            var team = _scene is "lake" or "canyon" or "snowplane"
                ? new[] { (4, "Vehicle Master"), (0, "Illusionist"), (1, "Gun Master"), (3, "Thief") }
                : new[] { (0, "Illusionist"), (1, "Weapon Master"), (2, "Tinkerer"), (3, "Grappler") };
            var count = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--showcase-heroes=")) is { } heroesArg
                ? System.Math.Clamp(int.Parse(heroesArg["--showcase-heroes=".Length..]), 1, team.Length)
                : team.Length;
            for (var i = 0; i < count; i++)
            {
                manager.JoinForDev(i == 0 ? InputDevice.KeyboardLeft : InputDevice.Gamepad(90 + i), team[i].Item1, team[i].Item2);
            }
        }
        else if (_stage == 1 && _time > 0.5)
        {
            _stage = 2;
            Setup(manager.Players.OfType<Hero>().ToArray(), At);
        }
        else if (_stage == 2 && _firing is not null && IsInstanceValid(_firing))
        {
            _firing.Trick(); // keep the guns going until the picture
        }

        if (_stage == 2 && _time > ShotAt)
        {
            _stage = 3;
            Shoot();
        }
    }

    private double ShotAt => _scene switch
    {
        "battle" => 1.15,
        "throne" => 1.3,
        _ => 1.6,
    };

    private void Setup(Hero[] heroes, System.Func<float, float, Vector2> at)
    {
        void Place(params Vector2[] spots)
        {
            for (var i = 0; i < heroes.Length && i < spots.Length; i++)
            {
                heroes[i].Teleport(spots[i]);
            }

            foreach (var camera in CoopCamera.All(this))
            {
                camera.Snap(); // so nobody gets held back at the old edge of the screen
            }
        }

        // No area-name banner over the picture.
        Dungeon.Find(this)!.Hud.ShowBanner("", "", 0);

        void FaceAll(Vector2 direction)
        {
            foreach (var hero in heroes)
            {
                hero.Face(direction);
            }
        }

        switch (_scene)
        {
            case "village":
                Place(at(280, 222), at(306, 230), at(330, 222), at(356, 230));
                FaceAll(Vector2.Down);
                break;

            case "throne":
                Place(at(116, 116), at(136, 124), at(156, 116), at(136, 104));
                FaceAll(Vector2.Up);
                var king = Dungeon.Find(this)!.CurrentRoom.GetNode<Npc>("King");
                king.Say(king.Greeting);
                break;

            case "battle":
            {
                // Everyone attacks the nearest deer at once.
                var deer = Dungeon.Find(this)!.CurrentRoom.Enemies.OfType<Deer>().First();
                var d = deer.GlobalPosition;
                Place(d + new Vector2(-30, 6), d + new Vector2(34, 4), d + new Vector2(-16, 30), d + new Vector2(20, 34));
                heroes[0].Face(Vector2.Right);
                heroes[1].Face(Vector2.Left);
                heroes[2].Face(Vector2.Up);
                heroes[3].Face(Vector2.Up);
                heroes[0].Abilities.OfType<IllusionAbility>().FirstOrDefault()?.TryUse(); // clones
                heroes[1].Abilities.FirstOrDefault(a => a.Slot == AbilitySlot.Primary)?.TryUse();
                heroes[2].Abilities.FirstOrDefault(a => a.Slot == AbilitySlot.Primary)?.TryUse();
                heroes[3].Abilities.FirstOrDefault(a => a.Slot == AbilitySlot.Primary)?.TryUse();
                break;
            }

            case "lake":
            {
                // The beetle builds the boat out on the water and everyone hops on.
                heroes[0].Teleport(at(820, 560));
                var builder = heroes[0].Abilities.OfType<VehicleAbility>().First();
                builder.Select(System.Array.IndexOf(System.Enum.GetValues<VehicleKind>(), VehicleKind.Boat));
                builder.TryUse();
                foreach (var hero in heroes.Skip(1))
                {
                    builder.Vehicle?.Board(hero);
                }

                break;
            }

            case "canyon":
            {
                // Gliding over the big chasm.
                heroes[0].Teleport(at(600, 500));
                var builder = heroes[0].Abilities.OfType<VehicleAbility>().First();
                builder.Select(System.Array.IndexOf(System.Enum.GetValues<VehicleKind>(), VehicleKind.Glider));
                builder.TryUse();
                foreach (var hero in heroes.Skip(1))
                {
                    builder.Vehicle?.Board(hero);
                }

                break;
            }

            case "snowplane":
            {
                // The plane firing its machine gun over the tundra's snow.
                heroes[0].Teleport(at(300, 420));
                var builder = heroes[0].Abilities.OfType<VehicleAbility>().First();
                builder.Choose(VehicleKind.Plane);
                builder.TryUse();
                _firing = builder.Vehicle;
                heroes[1].Teleport(at(260, 470));
                heroes[2].Teleport(at(340, 470));
                break;
            }

            case "boss":
            {
                var boss = Dungeon.Find(this)!.CurrentRoom.Enemies.OfType<Node2D>().First();
                var b = boss.GlobalPosition;
                Place(b + new Vector2(-60, 40), b + new Vector2(60, 40), b + new Vector2(-30, 70), b + new Vector2(30, 70));
                FaceAll(Vector2.Up);
                break;
            }
        }
    }

    private async void Shoot()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(_path);
        GD.Print(error == Error.Ok ? $"Saved showcase to {_path}" : $"Showcase failed: {error}");
        GetTree().Quit();
    }
}
