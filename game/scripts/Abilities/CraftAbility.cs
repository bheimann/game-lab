using System.Linq;
using Godot;
using LizardWizard.Effects;
using LizardWizard.Items;

namespace LizardWizard.Abilities;

/// <summary>
/// The moose Tinkerer crafts armor from monster parts (see <see cref="Armor.Recipe"/>): pick the piece in the ring menu
/// (Start), then press the button. Once made, the whole team has it in their armor ring.
/// </summary>
public partial class CraftAbility : Ability, IHasOptions
{
    private float _flashLeft;
    private int _selected;

    public string OptionsTitle => "Craft";

    private static Armor[] Recipes => Armor.Craftable.ToArray();

    public MenuOption[] Options => Recipes.Select(a => new MenuOption(
        a.Name,
        a.IsCrafted ? $"{a.Description} (made!)" : $"{a.Description}. Needs {a.RecipeText}",
        GD.Load<Texture2D>(a.IconPath), Colors.White) { Enabled = a.CanCraft || a.IsCrafted }).ToArray();

    public int Selected => _selected;

    public void Select(int index)
    {
        var count = Recipes.Length;
        _selected = (index % count + count) % count;
        var armor = Recipes[_selected];
        FloatingText.Spawn(this, Hero.HurtCenter + new Vector2(0, -14), armor.IsCrafted ? $"{armor.Name} (made)" : $"Craft {armor.Name}: {armor.RecipeText}", Colors.White, 1.4f, 7f);
    }

    protected override void Use()
    {
        var armor = Recipes[_selected];
        var textAt = Hero.HurtCenter + new Vector2(0, -16);
        if (armor.IsCrafted)
        {
            FloatingText.Spawn(this, textAt, $"Already made the {armor.Name}", new Color(0.8f, 0.8f, 0.8f), 1.2f, 7f);
        }
        else if (!armor.Craft())
        {
            FloatingText.Spawn(this, textAt, $"{armor.Name} needs {armor.RecipeText}", new Color(0.8f, 0.8f, 0.8f), 1.6f, 7f);
        }
        else
        {
            _flashLeft = 0.4f;
            FloatingText.Spawn(this, textAt, $"Crafted the {armor.Name}! Everyone can wear it", new Color(0.93f, 0.85f, 0.6f), 2f, 8f);
            return;
        }

        CooldownLeft = 0.2f;
    }

    protected override void Tick(float dt)
    {
        _flashLeft -= dt;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_flashLeft > 0)
        {
            // A hammer-strike flash.
            var at = Hero.AttackOrigin + Hero.Facing * 6f;
            DrawCircle(at, 6f * (1.4f - _flashLeft), new Color(1f, 0.9f, 0.6f, _flashLeft * 2f));
        }
    }
}
