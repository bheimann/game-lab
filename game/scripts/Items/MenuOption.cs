using Godot;

namespace LizardWizard.Items;

/// <summary>
/// One choice in a ring menu or quick-swap list: a bomb type, a colour, a piece of armor, an item.
/// With no <see cref="Icon"/> it's drawn as a dot in <see cref="Tint"/> (or an empty ring if the tint is clear).
/// </summary>
public sealed record MenuOption(string Name, string Description, Texture2D? Icon, Color Tint)
{
    /// <summary>Shown in the corner of the icon, e.g. how many herbs are left.</summary>
    public string? Count { get; init; }

    /// <summary>Greyed out and can't be picked (e.g. no herbs left).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Draw the icon centred on <paramref name="center"/>.</summary>
    public void Draw(CanvasItem canvas, Vector2 center, float scale, float alpha = 1f)
    {
        var dim = Enabled ? 1f : 0.35f;
        if (Icon is not null)
        {
            var size = Icon.GetSize() * scale;
            canvas.DrawTextureRect(Icon, new Rect2(center - size / 2, size), false, Tint with { A = Tint.A * alpha * dim });
        }
        else if (Tint.A > 0)
        {
            canvas.DrawCircle(center, 5f * scale, Tint with { A = alpha * dim });
            canvas.DrawArc(center, 5f * scale, 0, Mathf.Tau, 24, new Color(0.08f, 0.09f, 0.18f, alpha), 1.5f);
        }
        else
        {
            // "Nothing": an empty ring with a slash.
            var grey = new Color(0.7f, 0.7f, 0.78f, alpha);
            canvas.DrawArc(center, 5f * scale, 0, Mathf.Tau, 24, grey, 1.5f);
            canvas.DrawLine(center + new Vector2(-3.5f, 3.5f) * scale, center + new Vector2(3.5f, -3.5f) * scale, grey, 1.5f);
        }

        if (Count is not null)
        {
            var font = ThemeDB.FallbackFont;
            var at = center + new Vector2(3f, 6f) * scale;
            canvas.DrawStringOutline(font, at, Count, HorizontalAlignment.Left, -1, 8, 3, new Color(0.08f, 0.09f, 0.18f, alpha));
            canvas.DrawString(font, at, Count, HorizontalAlignment.Left, -1, 8, Colors.White with { A = alpha });
        }
    }
}

/// <summary>Something with a list of choices: shown as a ring in the menu, and LB / RB cycle through it.</summary>
public interface IHasOptions
{
    /// <summary>Ring title, e.g. "Bombs" or "Colours".</summary>
    string OptionsTitle { get; }

    MenuOption[] Options { get; }

    int Selected { get; }

    void Select(int index);
}
