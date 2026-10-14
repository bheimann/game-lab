using System.Linq;
using Godot;
using LizardWizard.Players;
using LizardWizard.Saving;
using LizardWizard.Util;

namespace LizardWizard;

/// <summary>
/// Root of a playable room: shows the start prompt until someone joins, and hooks up dev helpers.
/// On the start screen, holding N starts a new game (the save is thrown away). Closing the window saves.
/// </summary>
public partial class Game : Node2D
{
    private const double NewGameHoldSeconds = 2;

    private Label? _startPrompt;
    private string _promptText = "";
    private PlayerManager? _players;
    private double _newGameHeld;

    public override void _Ready()
    {
        _startPrompt = GetNodeOrNull<Label>("HUD/StartPrompt");
        _players = GetNodeOrNull<PlayerManager>("PlayerManager");
        if (_startPrompt is not null)
        {
            _promptText = _startPrompt.Text;
            if (SaveGame.Exists)
            {
                _startPrompt.Text = $"{_promptText}\n(Saved game loaded. Hold N for a new game.)";
            }
        }

        Screenshot.AttachIfRequested(this);
        DevAutoplay.AttachIfRequested(this);
    }

    public override void _Process(double delta)
    {
        if (_startPrompt is null || _players is null)
        {
            return;
        }

        var nobodyYet = _players.Players.All(p => p is null);
        _startPrompt.Visible = nobodyYet;

        // New game: hold N on the start screen.
        _newGameHeld = nobodyYet && SaveGame.Exists && Input.IsPhysicalKeyPressed(Key.N) ? _newGameHeld + delta : 0;
        if (_newGameHeld >= NewGameHoldSeconds)
        {
            SaveGame.Delete();
            GetTree().ReloadCurrentScene();
        }
        else if (_newGameHeld > 0)
        {
            _startPrompt.Text = $"{_promptText}\n(Starting a new game... keep holding N)";
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            SaveGame.Save(this);
        }
    }
}
