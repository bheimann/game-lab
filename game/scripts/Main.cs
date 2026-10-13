using Godot;

namespace LizardWizard;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        GetNode<Label>("Title").Text = "Lizard Wizard\n(C# is working!)";
        GD.Print("Lizard Wizard started.");
    }
}
