using System.Collections.Generic;
using System.Linq;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Combat;

namespace LizardWizard.Util;

/// <summary>One autoplay player: which keys it presses, plus simple walking and fighting.</summary>
internal sealed class AutoplayBot
{
    private bool _attackHeld;
    private Vector2 _lastPosition;
    private double _stuckFor;
    private double _detourLeft;
    private Key _detourKey;
    private int _detours;

    public AutoplayBot(int slot, Key up, Key left, Key down, Key right, Key primary, Key secondary, Key special, Key leave,
        Key item, Key previous, Key next, Key menu, Key dodge, bool isFox)
    {
        Dodge = dodge;
        Item = item;
        Previous = previous;
        Next = next;
        Menu = menu;
        Slot = slot;
        Up = up;
        Left = left;
        Down = down;
        Right = right;
        Primary = primary;
        Secondary = secondary;
        Special = special;
        Leave = leave;
        IsFox = isFox;
    }

    public int Slot { get; }
    public Key Up { get; }
    public Key Left { get; }
    public Key Down { get; }
    public Key Right { get; }
    public Key Primary { get; }
    public Key Secondary { get; }
    public Key Special { get; }
    public Key Leave { get; }
    public Key Item { get; }
    public Key Previous { get; }
    public Key Next { get; }
    public Key Menu { get; }
    public Key Dodge { get; }
    public bool IsFox { get; }

    public Key[] AllKeys => new[] { Up, Left, Down, Right, Primary, Secondary, Special, Leave, Item, Previous, Next, Menu, Dodge };

    /// <summary>Press the arrow keys that move the hero's feet toward <paramref name="target"/> (within <paramref name="slack"/> pixels).</summary>
    public void Steer(Hero hero, Vector2 target, HashSet<Key> wanted, float slack = 6f, bool feet = false)
    {
        var offset = target - (feet ? hero.GlobalPosition : hero.HurtCenter);
        if (offset.X > slack) wanted.Add(Right);
        if (offset.X < -slack) wanted.Add(Left);
        if (offset.Y > slack) wanted.Add(Down);
        if (offset.Y < -slack) wanted.Add(Up);
    }

    /// <summary>Hold the key for one direction (to walk through a door, or to turn and face something).</summary>
    public void Push(Vector2 direction, HashSet<Key> wanted)
    {
        if (direction.X > 0.5f) wanted.Add(Right);
        if (direction.X < -0.5f) wanted.Add(Left);
        if (direction.Y > 0.5f) wanted.Add(Down);
        if (direction.Y < -0.5f) wanted.Add(Up);
    }

    /// <summary>
    /// Walk to the nearest hostile enemy, turn to face it, attack every other frame.
    /// The lizard punches up close and tongues enemies from a few steps away to pull them in; the fox lobs
    /// bombs from a few steps away and bites if an enemy gets close. Both dodge sideways when a deer winds up a kick.
    /// </summary>
    public void Fight(Hero hero, IEnumerable<Node2D> enemies, HashSet<Key> wanted)
    {
        var target = enemies.Where(e => e is IEnemy { IsHostile: true } and IHittable { CanBeHit: true })
            .OrderBy(e => e.GlobalPosition.DistanceTo(hero.GlobalPosition)).FirstOrDefault() as IHittable;
        if (target is null || hero.IsDown)
        {
            return;
        }

        // Bigger enemies: stand off a little further.
        var reach = 13f + target.HurtRadius;
        var distance = target.HurtCenter.DistanceTo(hero.HurtCenter);

        if (target is Deer { IsWindingUp: true } && distance <= reach + 8f && !hero.IsDodging)
        {
            // Dodge sideways, not straight back: straight back can be through a door into the next room.
            var away = (hero.HurtCenter - target.HurtCenter).Normalized();
            Steer(hero, hero.HurtCenter + new Vector2(-away.Y, away.X) * 20f, wanted, slack: 1f);
            wanted.Add(Dodge);
            return;
        }
        // The fox's remote bombs: set them off (Y) once the enemy is next to one.
        if (IsFox && !_attackHeld && hero.GetParent().GetChildren().OfType<Effects.Bomb>()
                .Any(b => b.Thrower == hero && b.IsWaitingForRemote && b.GlobalPosition.DistanceTo(target.HurtCenter) < 30))
        {
            wanted.Add(Special);
            _attackHeld = true;
            return;
        }

        Key? attack = IsFox
            ? distance < reach ? Primary : distance is >= 34 and <= 58 ? Secondary : null
            : distance <= reach ? Primary : distance is >= 30 and <= 46 ? Secondary : null;

        if (attack is null || _attackHeld)
        {
            // Walk closer (or, on alternate frames, tap toward the enemy to face it).
            Steer(hero, target.HurtCenter, wanted);
            _attackHeld = false;
        }
        else
        {
            wanted.Add(attack.Value);
            _attackHeld = true;
        }
    }

    /// <summary>
    /// Call once per frame after deciding the keys: if the hero is trying to walk but hasn't moved for a moment
    /// (stuck on a chest or a rock), step sideways for a bit.
    /// </summary>
    public void Unstick(Hero hero, HashSet<Key> wanted, double delta)
    {
        if (_detourLeft > 0)
        {
            _detourLeft -= delta;
            wanted.Remove(_detourKey == Up ? Down : _detourKey == Down ? Up : _detourKey == Left ? Right : Left);
            wanted.Add(_detourKey);
            return;
        }

        var walking = wanted.Contains(Up) || wanted.Contains(Down) || wanted.Contains(Left) || wanted.Contains(Right);
        var moved = hero.GlobalPosition.DistanceTo(_lastPosition) > 0.2f;
        _lastPosition = hero.GlobalPosition;
        _stuckFor = walking && !moved && !hero.IsDown ? _stuckFor + delta : 0;
        if (_stuckFor > 0.3)
        {
            // Blocked going sideways? Step up or down. Blocked going up or down? Step left or right.
            var horizontal = wanted.Contains(Left) || wanted.Contains(Right);
            _detours++;
            _detourKey = horizontal ? (_detours % 2 == 0 ? Down : Up) : (_detours % 2 == 0 ? Right : Left);
            _detourLeft = 0.45;
            _stuckFor = 0;
        }
    }

    /// <summary>Tap the attack button every other frame (to flip a lever, throw bombs at rocks...).</summary>
    public void Tap(Key key, HashSet<Key> wanted)
    {
        if (!_attackHeld)
        {
            wanted.Add(key);
        }

        _attackHeld = !_attackHeld;
    }
}
