using System.Collections.Generic;
using Godot;

namespace LizardWizard.Util;

/// <summary>
/// Finds a way around obstacles for a stuck enemy: an A* search on a small grid of cells around it, where a cell is
/// blocked if the enemy's feet wouldn't fit there (trees, rocks, walls, water, pits). Only used now and then (when
/// something's been stuck for a moment), so it just asks the physics engine about each cell it looks at.
/// </summary>
public static class GridPath
{
    private const float Cell = 12f;
    private const int Reach = 22; // cells either side of the start: a 45x45-cell search area

    private static readonly Vector2I[] Steps =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    };

    /// <summary>
    /// A list of points from near <paramref name="start"/> to as close to <paramref name="goal"/> as it can get, for a body
    /// of <paramref name="feet"/> size (centred on the feet, see Beast) that bumps into <paramref name="mask"/>.
    /// Empty if there's no way.
    /// </summary>
    public static List<Vector2> Find(CharacterBody2D body, Vector2 start, Vector2 goal, Vector2 feet, uint mask)
    {
        var space = body.GetWorld2D().DirectSpaceState;
        var shape = new RectangleShape2D { Size = feet };
        var blocked = new Dictionary<Vector2I, bool>();

        bool Free(Vector2I c)
        {
            if (blocked.TryGetValue(c, out var b))
            {
                return !b;
            }

            var query = new PhysicsShapeQueryParameters2D
            {
                Shape = shape,
                Transform = new Transform2D(0, ToWorld(c) + new Vector2(0, -feet.Y / 2)),
                CollisionMask = mask,
                Exclude = new Godot.Collections.Array<Rid> { body.GetRid() },
            };
            b = space.IntersectShape(query, 1).Count > 0;
            blocked[c] = b;
            return !b;
        }

        Vector2 ToWorld(Vector2I c) => start + new Vector2(c.X, c.Y) * Cell;

        // Can the feet slide all the way from one cell to the next? (A fence is thinner than a cell, so checking just the
        // cells would hop right over it.)
        bool CanStep(Vector2I from, Vector2I to)
        {
            var query = new PhysicsShapeQueryParameters2D
            {
                Shape = shape,
                Transform = new Transform2D(0, ToWorld(from) + new Vector2(0, -feet.Y / 2)),
                Motion = ToWorld(to) - ToWorld(from),
                CollisionMask = mask,
                Exclude = new Godot.Collections.Array<Rid> { body.GetRid() },
            };
            return space.CastMotion(query)[0] >= 1f;
        }

        // The goal, as a cell (clamped to the search area).
        var g = (goal - start) / Cell;
        var target = new Vector2I(Mathf.Clamp(Mathf.RoundToInt(g.X), -Reach, Reach), Mathf.Clamp(Mathf.RoundToInt(g.Y), -Reach, Reach));

        var open = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var cost = new Dictionary<Vector2I, float> { [Vector2I.Zero] = 0 };
        open.Enqueue(Vector2I.Zero, 0);
        var best = Vector2I.Zero;
        var bestDistance = float.MaxValue;

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            var distance = ((Vector2)(target - current)).Length();
            if (distance < bestDistance)
            {
                best = current;
                bestDistance = distance;
            }

            if (current == target)
            {
                break;
            }

            foreach (var step in Steps)
            {
                var next = current + step;
                if (Mathf.Abs(next.X) > Reach || Mathf.Abs(next.Y) > Reach || !Free(next) || !CanStep(current, next))
                {
                    continue;
                }

                // No cutting corners past an obstacle.
                if (step.X != 0 && step.Y != 0 && (!Free(current + new Vector2I(step.X, 0)) || !Free(current + new Vector2I(0, step.Y))))
                {
                    continue;
                }

                var newCost = cost[current] + (step.X != 0 && step.Y != 0 ? 1.414f : 1f);
                if (!cost.TryGetValue(next, out var old) || newCost < old)
                {
                    cost[next] = newCost;
                    cameFrom[next] = current;
                    open.Enqueue(next, newCost + ((Vector2)(target - next)).Length());
                }
            }
        }

        var path = new List<Vector2>();
        for (var c = best; c != Vector2I.Zero; c = cameFrom[c])
        {
            path.Add(ToWorld(c));
        }

        path.Reverse();
        return path;
    }
}
