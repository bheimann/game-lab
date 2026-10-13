using System;
using System.Collections.Generic;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Items;
using LizardWizard.Players;

namespace LizardWizard.UI;

/// <summary>One ring of the menu: a title, its choices, and what picking one does.</summary>
public sealed record MenuRing(string Title, MenuOption[] Options, int Selected, Action<int> Choose);

/// <summary>
/// A Secret of Mana style ring menu that opens around one player's hero. The game keeps running:
/// the hero can still walk (slowly if an enemy is after them) but can't attack while it's open.
/// Previous / next turn the ring, special switches rings, primary picks, secondary or menu closes.
/// It's drawn on a screen-space layer so the icons and text stay sharp at any camera zoom.
/// </summary>
public partial class RingMenu : Node2D
{
    private const string LayerName = "MenuLayer";
    private const float RingRadius = 44f;
    private const float IconScale = 2f;
    private const int FontSize = 8;
    private static readonly Color Outline = new(0.08f, 0.09f, 0.18f);

    private readonly Hero _hero;
    private readonly Func<List<MenuRing>> _buildRings;
    private List<MenuRing> _rings = new();
    private int _ring;
    private int _selected;
    private float _turn; // animated ring angle, in "slots"
    private float _open; // 0..1 opening animation

    public RingMenu(Hero hero, Func<List<MenuRing>> buildRings)
    {
        _hero = hero;
        _buildRings = buildRings;
        Visible = false;
    }

    public bool IsOpen { get; private set; }

    /// <summary>Make the menu for <paramref name="hero"/> and put it on the screen-space menu layer.</summary>
    public static RingMenu Create(Hero hero, Func<List<MenuRing>> buildRings)
    {
        var menu = new RingMenu(hero, buildRings);
        Layer(hero).AddChild(menu);
        return menu;
    }

    public void Open()
    {
        _rings = _buildRings();
        if (_rings.Count == 0)
        {
            return;
        }

        IsOpen = true;
        Visible = true;
        _open = 0;
        ShowRing(Math.Clamp(_ring, 0, _rings.Count - 1));
    }

    public void Close()
    {
        IsOpen = false;
        Visible = false;
    }

    /// <summary>Called by the hero each physics frame while the menu is open.</summary>
    public void HandleInput(PlayerInput input)
    {
        var count = _rings[_ring].Options.Length;
        if (input.NextPressed)
        {
            _selected = (_selected + 1) % count;
        }
        else if (input.PreviousPressed)
        {
            _selected = (_selected - 1 + count) % count;
        }
        else if (input.SpecialPressed)
        {
            ShowRing((_ring + 1) % _rings.Count);
        }
        else if (input.PrimaryPressed)
        {
            var ring = _rings[_ring];
            if (ring.Options[_selected].Enabled)
            {
                ring.Choose(_selected);
                Close();
            }
        }
        else if (input.SecondaryPressed || input.MenuPressed)
        {
            Close();
        }
    }

    public override void _Process(double delta)
    {
        if (!IsOpen)
        {
            return;
        }

        var dt = (float)delta;
        _open = Mathf.Min(1f, _open + dt * 8f);

        // Turn the shortest way round to the selected slot.
        var count = _rings[_ring].Options.Length;
        var diff = Mathf.PosMod(_selected - _turn + count / 2f, count) - count / 2f;
        _turn += diff * Mathf.Min(1f, dt * 14f);

        var screen = GetViewport().GetCanvasTransform() * (_hero.GlobalPosition + new Vector2(0, -12));
        var view = GetViewportRect().Size;
        var reach = RingRadius + 12f;
        Position = new Vector2(Mathf.Clamp(screen.X, reach + 40, view.X - reach - 40), Mathf.Clamp(screen.Y, reach + 18, view.Y - reach - 40));
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!IsOpen)
        {
            return;
        }

        var ring = _rings[_ring];
        var count = ring.Options.Length;
        var radius = RingRadius * (0.4f + 0.6f * _open);
        var colour = _hero.PlayerColour;

        // A dark band behind the icons (the middle stays clear so you can still see your hero).
        DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 64, Outline with { A = 0.5f * _open }, 22f);
        DrawArc(Vector2.Zero, radius + 11f, 0, Mathf.Tau, 64, colour with { A = _open }, 1.5f);

        // Icons around the ring, the selected one at the top.
        for (var i = 0; i < count; i++)
        {
            var angle = -Mathf.Pi / 2 + (i - _turn) * Mathf.Tau / count;
            var at = Vector2.Right.Rotated(angle) * radius;
            var selected = i == _selected;
            if (selected)
            {
                DrawCircle(at, 10f, colour with { A = 0.45f * _open });
                DrawArc(at, 10f, 0, Mathf.Tau, 24, Colors.White with { A = _open }, 1f);
            }

            ring.Options[i].Draw(this, at, selected ? IconScale : IconScale * 0.75f, (selected ? 1f : 0.7f) * _open);
        }

        // Title above, the selected choice and what it does below, button hints at the bottom.
        var option = ring.Options[_selected];
        var title = _rings.Count > 1 ? $"{ring.Title}  ({_ring + 1}/{_rings.Count})" : ring.Title;
        Text(title, new Vector2(0, -radius - 16f), colour, FontSize + 2);
        Text(option.Name, new Vector2(0, radius + 24f), Colors.White, FontSize + 2);
        Text(option.Description, new Vector2(0, radius + 34f), new Color(0.85f, 0.87f, 0.95f), FontSize);
        Text(Hints(), new Vector2(0, radius + 44f), new Color(0.7f, 0.72f, 0.82f), FontSize);
    }

    private void ShowRing(int index)
    {
        _ring = index;
        _selected = _rings[_ring].Selected;
        _turn = _selected;
    }

    private string Hints() => _hero.Controls.Device.Kind switch
    {
        InputDeviceKind.KeyboardLeft => "Q E turn   J pick   L next ring   K close",
        InputDeviceKind.KeyboardRight => "; ' turn   . pick   , next ring   / close",
        _ => "LB RB turn   A pick   Y next ring   X close",
    };

    private void Text(string text, Vector2 center, Color colour, int size)
    {
        var font = ThemeDB.FallbackFont;
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var at = center + new Vector2(-width / 2, 0);
        DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, size, 3, Outline with { A = _open });
        DrawString(font, at, text, HorizontalAlignment.Left, -1, size, colour with { A = _open });
    }

    private static CanvasLayer Layer(Node anyNode)
    {
        var root = anyNode.GetTree().CurrentScene;
        if (root.GetNodeOrNull<CanvasLayer>(LayerName) is { } existing)
        {
            return existing;
        }

        var layer = new CanvasLayer { Name = LayerName, Layer = 6 };
        root.AddChild(layer);
        return layer;
    }
}
