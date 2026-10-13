using System.Linq;
using Godot;
using LizardWizard.Players;
using LizardWizard.Util;

namespace LizardWizard;

/// <summary>Root of a playable room: shows the start prompt until someone joins, and hooks up dev helpers.</summary>
public partial class Game : Node2D
{
    private Label? _startPrompt;
    private PlayerManager? _players;

    public override void _Ready()
    {
        _startPrompt = GetNodeOrNull<Label>("HUD/StartPrompt");
        _players = GetNodeOrNull<PlayerManager>("PlayerManager");

        Screenshot.AttachIfRequested(this);
        DevAutoplay.AttachIfRequested(this);
    }

    public override void _Process(double delta)
    {
        if (_startPrompt is not null && _players is not null)
        {
            _startPrompt.Visible = _players.Players.All(p => p is null);
        }
    }
}
