using System.Linq;
using Godot;
using LizardWizard.Actors;

namespace LizardWizard.Effects;

/// <summary>
/// A slime blob an enemy spat (the frog's): flies straight, hurts the first hero, summon or decoy it touches (for a
/// charmed frog, the first enemy), and splats on walls. Heroes can dodge through it.
/// </summary>
public partial class EnemyShot : Node2D
{
    private const float Speed = 140f;
    private const float Range = 170f;

    private static readonly Color Slime = new(0.55f, 0.85f, 0.3f);

    private Node2D _shooter = null!;
    private Vector2 _velocity;
    private float _travelled;
    private int _damage;
    private bool _forHeroes;

    /// <param name="charmed">A charmed enemy's shot hits the other enemies instead.</param>
    public static void Fire(Node2D shooter, Vector2 from, Vector2 direction, int damage, bool charmed)
    {
        var shot = new EnemyShot
        {
            _shooter = shooter,
            _velocity = direction.Normalized() * Speed,
            _damage = damage,
            _forHeroes = !charmed,
        };
        shooter.GetParent().AddChild(shot);
        shot.GlobalPosition = from;
    }

    public override void _PhysicsProcess(double delta)
    {
        var start = GlobalPosition;
        var end = start + _velocity * (float)delta;

        var wall = GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(start, end, collisionMask: 1));
        if (wall.Count > 0)
        {
            QueueFree();
            return;
        }

        var victims = _forHeroes
            ? GetTree().GetNodesInGroup(Hero.Group).OfType<IEnemyTarget>()
                .Concat(Enemies.Summons(GetTree())).Concat(Enemies.Lures(GetTree()))
            : GetTree().GetNodesInGroup(Enemies.Group).OfType<IEnemyTarget>().Where(e => !ReferenceEquals(e, _shooter));
        var hit = victims.FirstOrDefault(t => t.IsTargetable && Geometry2D.GetClosestPointToSegment(t.HurtCenter, start, end).DistanceTo(t.HurtCenter) <= t.HurtRadius);
        if (hit is not null)
        {
            hit.TakeHit(_damage, start);
            QueueFree();
            return;
        }

        GlobalPosition = end;
        _travelled += _velocity.Length() * (float)delta;
        if (_travelled >= Range)
        {
            QueueFree();
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 3.5f, new Color(0.08f, 0.09f, 0.18f));
        DrawCircle(Vector2.Zero, 2.6f, Slime);
        DrawCircle(new Vector2(-0.8f, -0.8f), 1f, Colors.White with { A = 0.7f });
    }
}
